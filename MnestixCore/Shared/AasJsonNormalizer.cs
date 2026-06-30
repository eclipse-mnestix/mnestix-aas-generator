using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Shared;

/// <summary>
/// Normalizes a JSON payload for compatibility with BaSyx Go's stricter AAS v3 compliance.
/// Applies 10 rules:
/// 1. Strip null-valued properties.
/// 2. Remove deprecated v2 fields: "hasDataSpecification" (always) and "dataSpecification" (only when array).
/// 3. Strip kind from any object that is not a Submodel (including objects without modelType).
/// 4. Strip parent back-references.
/// 5. Strip AAS v2 Key fields: local, idType, index.
/// 6. Strip AAS v2 collection fields: ordered, allowDuplicates.
/// 7. Normalize valueType to canonical XSD casing.
/// 8. Inject xs:string valueType on qualifiers that are missing it or have an empty value.
/// 9. Coerce non-string Property.value (integer, float, boolean) to a JSON-formatted string.
/// 10. Remove empty "qualifiers" arrays (BaSyx requires at least one item or the field to be absent).
/// </summary>
public static class AasJsonNormalizer
{
    /// <summary>
    /// Normalizes any AAS JSON object for compatibility with BaSyx Go's stricter v3 schema.
    /// Accepts any top-level AAS element: AssetAdministrationShell, Submodel, SubmodelElement,
    /// ConceptDescription, or any nested structure thereof.
    /// Returns a deep-cloned copy with all normalization rules applied recursively;
    /// the original <paramref name="json"/> is not mutated.
    /// </summary>
    /// <param name="json">
    /// A JSON object representing any AAS v3 element (shell, submodel, submodel element, etc.).
    /// </param>
    /// <returns>A normalized deep clone of <paramref name="json"/>.</returns>
    public static JObject NormalizeJsonForRepository(JObject json)
    {
        JObject normalized = (JObject)json.DeepClone();
        NormalizeToken(normalized);
        return normalized;
    }

    private static void NormalizeToken(JToken token)
    {
        switch (token)
        {
            case JObject obj:
                var propsToRemove = new List<string>();

                foreach (var prop in obj.Properties().ToList())
                {
                    // Rule 1: Strip null-valued properties
                    if (prop.Value.Type == JTokenType.Null)
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 2: Remove deprecated v2 fields.
                    // - "hasDataSpecification" is always a v2 leftover → remove unconditionally.
                    // - "dataSpecification" as an array on an AAS element (v2 pattern) → remove.
                    //   But "dataSpecification" as an object inside EmbeddedDataSpecification is
                    //   valid and required in AAS v3 → keep it.
                    if (prop.Name == "hasDataSpecification")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    if (prop.Name == "dataSpecification" && prop.Value.Type == JTokenType.Array)
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 4: Strip parent back-references
                    if (prop.Name == "parent")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 5: Strip AAS v2 Key fields
                    if (prop.Name is "local" or "idType" or "index")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 6: Strip AAS v2 collection fields
                    if (prop.Name is "ordered" or "allowDuplicates")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Rule 10: Remove empty "qualifiers" arrays
                    if (prop.Name == "qualifiers" && prop.Value is JArray { Count: 0 })
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }
                }

                foreach (var name in propsToRemove)
                {
                    obj.Remove(name);
                }

                // Rule 3: Strip kind from non-Submodel elements
                // Only objects with modelType="Submodel" may keep "kind".
                // Objects with a different modelType OR with no modelType at all must have it removed.
                var modelTypeToken = obj["modelType"];
                var modelType = modelTypeToken switch
                {
                    JValue jv => jv.Value<string>(),
                    JObject jo => jo["name"]?.Value<string>(), // AAS v2 format: {"name": "..."}
                    _ => null
                };
                if (obj.ContainsKey("kind") && modelType != "Submodel")
                {
                    obj.Remove("kind");
                }

                // Rule 7: Normalize valueType to canonical XSD casing
                if (obj["valueType"] is JToken vt && vt.Type == JTokenType.String)
                {
                    var raw = vt.Value<string>();
                    if (DataTypeDefXsd.TryGetCanonical(raw, out var canonical))
                    {
                        obj["valueType"] = canonical;
                    }
                }

                // Rule 8: Inject xs:string valueType on qualifiers missing it or with an empty value.
                // Detect qualifier objects by their parent property name ("qualifiers") rather than
                // by modelType == null, because AAS v3 qualifiers carry "modelType": "Qualifier".
                // Also treat an empty-string valueType as missing.
                var isInsideQualifiersArray =
                    (obj.Parent as JArray)?.Parent is JProperty { Name: "qualifiers" };
                if (isInsideQualifiersArray && obj["type"] != null)
                {
                    var existingValueType = obj["valueType"]?.Value<string>();
                    if (string.IsNullOrEmpty(existingValueType))
                    {
                        obj["valueType"] = "xs:string";
                    }
                }

                // Rule 9: Coerce non-string Property.value to a JSON-formatted string.
                // Use WriteTo(JsonWriter) instead of ToString() so booleans produce "true"/"false"
                // (JSON casing) rather than "True"/"False" (C# casing).
                if (modelType == "Property" && obj["value"] is JToken val)
                {
                    if (val.Type is JTokenType.Integer or JTokenType.Float or JTokenType.Boolean)
                    {
                        obj["value"] = ToJsonString(val);
                    }
                }

                // Recurse into remaining properties
                foreach (var prop in obj.Properties().ToList())
                {
                    NormalizeToken(prop.Value);
                }
                break;

            case JArray arr:
                foreach (var item in arr.ToList())
                {
                    NormalizeToken(item);
                }
                break;
        }
    }

    /// <summary>
    /// Serializes a <see cref="JToken"/> to its JSON text representation.
    /// Unlike <see cref="JToken.ToString()"/>, this correctly produces JSON-cased values
    /// (e.g. <c>true</c>/<c>false</c> for booleans instead of <c>True</c>/<c>False</c>).
    /// </summary>
    private static string ToJsonString(JToken token)
    {
        using var sw = new StringWriter();
        using var writer = new JsonTextWriter(sw);
        token.WriteTo(writer);
        return sw.ToString();
    }
}
