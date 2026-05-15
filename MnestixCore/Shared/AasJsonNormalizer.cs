using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Shared;

/// <summary>
/// Normalizes a JSON payload for compatibility with BaSyx Go's stricter AAS v3 compliance.
/// Applies 7 rules: strip nulls, remove dataSpecification, strip kind from non-Submodel,
/// strip parent, normalize valueType, inject qualifier valueType, coerce Property.value to string.
/// </summary>
public static class AasJsonNormalizer
{
    // Canonical XSD value-type mapping (BaSyx Go requires lowercase)
    private static readonly Dictionary<string, string> ValueTypeCaseMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xs:string"] = "xs:string",
        ["xs:boolean"] = "xs:boolean",
        ["xs:integer"] = "xs:integer",
        ["xs:int"] = "xs:int",
        ["xs:long"] = "xs:long",
        ["xs:short"] = "xs:short",
        ["xs:decimal"] = "xs:decimal",
        ["xs:double"] = "xs:double",
        ["xs:float"] = "xs:float",
        ["xs:dateTime"] = "xs:dateTime",
        ["xs:date"] = "xs:date",
        ["xs:time"] = "xs:time",
        ["xs:anyURI"] = "xs:anyURI",
        ["xs:base64Binary"] = "xs:base64Binary",
        ["xs:hexBinary"] = "xs:hexBinary",
        ["xs:byte"] = "xs:byte",
        ["xs:unsignedByte"] = "xs:unsignedByte",
        ["xs:unsignedShort"] = "xs:unsignedShort",
        ["xs:unsignedInt"] = "xs:unsignedInt",
        ["xs:unsignedLong"] = "xs:unsignedLong",
        ["xs:positiveInteger"] = "xs:positiveInteger",
        ["xs:nonNegativeInteger"] = "xs:nonNegativeInteger",
        ["xs:negativeInteger"] = "xs:negativeInteger",
        ["xs:nonPositiveInteger"] = "xs:nonPositiveInteger",
        ["xs:duration"] = "xs:duration",
        ["xs:gDay"] = "xs:gDay",
        ["xs:gMonth"] = "xs:gMonth",
        ["xs:gMonthDay"] = "xs:gMonthDay",
        ["xs:gYear"] = "xs:gYear",
        ["xs:gYearMonth"] = "xs:gYearMonth",
    };

    public static JObject NormalizeJsonForRepository(JObject json)
    {
        NormalizeToken(json);
        return json;
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

                    // Rule 2: Remove deprecated dataSpecification property
                    if (prop.Name is "dataSpecification" or "hasDataSpecification")
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

                    // Strip v2 fields from Key objects
                    if (prop.Name is "local" or "idType" or "index")
                    {
                        propsToRemove.Add(prop.Name);
                        continue;
                    }

                    // Strip v2 ordered / allowDuplicates from SubmodelElementCollections
                    if (prop.Name is "ordered" or "allowDuplicates")
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
                // Only objects with modelType=Submodel may keep "kind".
                // Objects with a different modelType OR with no modelType at all must have it removed.
                var modelType = obj["modelType"]?.Value<string>();
                if (obj.ContainsKey("kind") && modelType != "Submodel")
                {
                    obj.Remove("kind");
                }

                // Rule 5: Normalize valueType to canonical XSD case
                if (obj["valueType"] is JToken vt && vt.Type == JTokenType.String)
                {
                    var raw = vt.Value<string>();
                    if (raw != null && ValueTypeCaseMap.TryGetValue(raw, out var canonical))
                    {
                        obj["valueType"] = canonical;
                    }
                }

                // Rule 6: Inject valueType on qualifiers missing it or with an empty valueType.
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

                // Rule 7: Coerce non-string Property.value to a JSON-formatted string.
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
