using System.Text.RegularExpressions;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Jsonata.Net.Native;
using Jsonata.Net.Native.JsonNet;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

public sealed class MapDataToInstanceAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    private const string MappingInfoPrefix = "SMT/MappingInfo";

    private static readonly string[] AllowedFields = ["value", "idShort", "globalAssetId", "entityType", "displayName", "first", "second"];

    private static readonly Dictionary<string, HashSet<string>> FieldApplicableModelTypes = new()
    {
        ["value"] = new HashSet<string> { "Property", "Blob", "MultiLanguageProperty", "File" },
        ["idShort"] = new HashSet<string>(), // empty = all model types
        ["globalAssetId"] = new HashSet<string> { "Entity" },
        ["entityType"] = new HashSet<string> { "Entity" },
        ["displayName"] = new HashSet<string>(), // empty = all model types
        ["first"] = new HashSet<string> { "RelationshipElement", "AnnotatedRelationshipElement" },
        ["second"] = new HashSet<string> { "RelationshipElement", "AnnotatedRelationshipElement" },
    };

    private static readonly Dictionary<string, Func<string, bool>> ValueTypeValidators = new()
    {
        ["xs:string"] = _ => true,
        ["xs:boolean"] = v => bool.TryParse(v, out _) || v is "0" or "1",
        ["xs:integer"] = v => long.TryParse(v, out _),
        ["xs:int"] = v => int.TryParse(v, out _),
        ["xs:long"] = v => long.TryParse(v, out _),
        ["xs:short"] = v => short.TryParse(v, out _),
        ["xs:decimal"] = v => decimal.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _),
        ["xs:double"] = v => double.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _),
        ["xs:float"] = v => float.TryParse(v, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _),
        ["xs:dateTime"] = v => DateTime.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
        ["xs:date"] = v => DateOnly.TryParse(v, System.Globalization.CultureInfo.InvariantCulture, out _),
        ["xs:anyURI"] = _ => true,
    };

    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started MapDataToInstanceStep");
        MapDataToInstance(ctx);
        ctx.Log($"Finished MapDataToInstanceStep");
        return Task.FromResult(ctx);
    }

    private static string ParseFieldName(string qualifierType, DataMappingContext ctx)
    {
        var segments = qualifierType.Split('/');
        // Valid forms: "SMT/MappingInfo" (2 segments) or "SMT/MappingInfo/<FieldName>" (exactly 3 segments)
        if (segments.Length > 3)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Malformed qualifier type '{qualifierType}'. Expected 'SMT/MappingInfo' or 'SMT/MappingInfo/<FieldName>'.", ctx);
        }
        return segments.Length == 3 ? segments[2] : "value";
    }

    private static void ValidateFieldName(string fieldName, DataMappingContext ctx)
    {
        if (!AllowedFields.Contains(fieldName))
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Unsupported MappingInfo field '{fieldName}'. Allowed: {string.Join(", ", AllowedFields)}", ctx);
        }
    }

    private static void ValidateFieldApplicability(string fieldName, string modelType, DataMappingContext ctx)
    {
        var applicableTypes = FieldApplicableModelTypes[fieldName];
        // Empty set means applicable to all model types
        if (applicableTypes.Count > 0 && !applicableTypes.Contains(modelType))
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Field '{fieldName}' is not applicable to model type '{modelType}'", ctx);
        }
    }

    private static void ValidateDuplicateFields(List<(JToken qualifier, string fieldName)> qualifiersWithFields, DataMappingContext ctx)
    {
        var seen = new HashSet<string>();
        foreach (var (qualifier, fieldName) in qualifiersWithFields)
        {
            if (!seen.Add(fieldName))
            {
                var elementIdShort = qualifier.Parent?.Parent?.Parent?["idShort"]?.Value<string>() ?? "unknown";
                ctx.Qualifier = qualifier;
                throw new SubmodelDataToInstanceMapperException(
                    $"Duplicate mapping for field '{fieldName}' on element '{elementIdShort}'", ctx);
            }
        }
    }

    private static string SanitizeIdShort(string value, DataMappingContext ctx)
    {
        var sanitized = Regex.Replace(value, @"[^a-zA-Z0-9_]", "_");
        // AAS Metamodel v3 requires idShort to match [a-zA-Z][a-zA-Z0-9_]*
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]))
        {
            sanitized = "i" + sanitized;
        }
        if (sanitized != value)
        {
            ctx.LogWarning($"idShort value '{value}' was sanitized to '{sanitized}'");
        }
        return sanitized;
    }

    private static void ValidateValueType(JToken element, JToken dataValue, DataMappingContext ctx)
    {
        var valueType = element["valueType"]?.Value<string>();
        if (string.IsNullOrEmpty(valueType)) return;

        var stringValue = dataValue.ToString();
        if (ValueTypeValidators.TryGetValue(valueType, out var validator))
        {
            if (!validator(stringValue))
            {
                throw new SubmodelDataToInstanceMapperException(
                    $"Mapped value '{stringValue}' does not conform to valueType '{valueType}'", ctx);
            }
        }
        else
        {
            ctx.LogWarning($"Unknown valueType '{valueType}' — skipping validation");
        }
    }

    private static JArray ConvertToMultiLanguageProperty(string text, string language)
    {
        // Currently only one language is supported
        // See docs/rules-engine.md -> MultiLanguageProperty for more information
        return new JArray
        {
            new JObject
            {
                {"text", text},
                {"language", language}
            }
        };
    }

    private static void AssignValueField(JToken element, JToken dataFromMappingPath, string modelType, string language, DataMappingContext ctx)
    {
        ValidateValueType(element, dataFromMappingPath, ctx);

        var blueprintValue = element["value"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching value field of the selected SME", ctx);

        if (modelType == "MultiLanguageProperty")
        {
            if (dataFromMappingPath.Type is not (JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean or JTokenType.Null))
            {
                throw new SubmodelDataToInstanceMapperException(
                    $"MultiLanguageProperty expects a string, number, boolean, or null value, but got {dataFromMappingPath.Type}", ctx);
            }
            blueprintValue.Replace(ConvertToMultiLanguageProperty(dataFromMappingPath.ToString(), language));
            return;
        }

        blueprintValue.Replace(dataFromMappingPath);
    }

    private static void AssignField(JToken element, string fieldName, JToken dataFromMappingPath, string modelType, string language, DataMappingContext ctx)
    {
        switch (fieldName)
        {
            case "value":
                AssignValueField(element, dataFromMappingPath, modelType, language, ctx);
                break;
            case "idShort":
                var sanitized = SanitizeIdShort(dataFromMappingPath.ToString(), ctx);
                element["idShort"] = sanitized;
                break;
            case "globalAssetId":
                element["globalAssetId"] = dataFromMappingPath.ToString();
                break;
            case "entityType":
                element["entityType"] = dataFromMappingPath.ToString();
                break;
            case "displayName":
                var displayNameArray = element["displayName"] as JArray;
                if (displayNameArray == null)
                {
                    displayNameArray = new JArray();
                    element["displayName"] = displayNameArray;
                }
                var langEntry = displayNameArray.FirstOrDefault(e => e["language"]?.Value<string>() == language);
                if (langEntry != null)
                {
                    langEntry["text"] = dataFromMappingPath.ToString();
                }
                else
                {
                    displayNameArray.Add(new JObject { ["language"] = language, ["text"] = dataFromMappingPath.ToString() });
                }
                break;
            case "first":
                if (dataFromMappingPath is not JObject firstObj)
                    throw new SubmodelDataToInstanceMapperException(
                        $"Field 'first' requires a JSON object (AAS Reference), but got {dataFromMappingPath.Type}", ctx);
                element["first"] = firstObj;
                break;
            case "second":
                if (dataFromMappingPath is not JObject secondObj)
                    throw new SubmodelDataToInstanceMapperException(
                        $"Field 'second' requires a JSON object (AAS Reference), but got {dataFromMappingPath.Type}", ctx);
                element["second"] = secondObj;
                break;
        }
    }

    private static JToken? SelectTokenFromDataJson(JToken dataJson, string mappingPath, DataMappingContext ctx)
    {
        try
        {
            // JSONATA returns Undefined (JToken with Type=Undefined) for missing paths instead of null
            var query = new JsonataQuery(mappingPath);
            var result = query.EvalNewtonsoft(dataJson);

            // Convert JSONATA Undefined to null for backward compatibility
            if (result?.Type == JTokenType.Undefined)
            {
                return null;
            }

            return result;
        }
        catch (Exception e) when (!(e is SubmodelDataToInstanceMapperException))
        {

            throw new SubmodelDataToInstanceMapperException($"Error while evaluating JSONATA expression '{mappingPath}': " + e.Message, e, ctx);

        }
    }

    private static void CheckIfValueKeyExists(JToken element)
    {
        /*
        As per the v3 standard, "value" in MultiLanguageProperty has Cardinality "0..1,"
        indicating potential absence in blueprint data. We handle this by creating
        an empty value for the key "value" during mapping, ensuring smooth mapping without exceptions.
        */
        if (element["value"] != null) return;
        element["value"] = new JArray();
    }

    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }

    private static void MapDataToInstance(DataMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;
        var language = ctx.Language;

        // T002: Match qualifiers whose type is exactly "SMT/MappingInfo" or starts with "SMT/MappingInfo/"
        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[*]")
            .Where(q => q["type"]?.Value<string>() is string t &&
                        (t == MappingInfoPrefix || t.StartsWith(MappingInfoPrefix + "/", StringComparison.Ordinal)))
            .ToList();

        // Group qualifiers by their parent element to detect duplicates
        var qualifiersByElement = qualifiers
            .GroupBy(q => q.Parent?.Parent?.Parent) // qualifier -> JArray -> JProperty "qualifiers" -> element JObject
            .Where(g => g.Key != null);

        foreach (var elementGroup in qualifiersByElement)
        {
            var element = elementGroup.Key!;
            var modelTypeToken = element["modelType"] ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of selected SME", ctx);
            var modelType = modelTypeToken.Value<string>()!;

            // Parse and validate all qualifiers for this element
            var qualifiersWithFields = new List<(JToken qualifier, string fieldName)>();
            foreach (var qualifier in elementGroup)
            {
                var qualifierType = qualifier["type"]?.Value<string>() ?? "";
                var fieldName = ParseFieldName(qualifierType, ctx);

                ctx.Qualifier = qualifier;
                ValidateFieldName(fieldName, ctx);
                ValidateFieldApplicability(fieldName, modelType, ctx);

                qualifiersWithFields.Add((qualifier, fieldName));
            }

            // T007: Duplicate field detection
            ValidateDuplicateFields(qualifiersWithFields, ctx);

            // Process each qualifier
            foreach (var (qualifier, fieldName) in qualifiersWithFields)
            {
                ctx.Qualifier = qualifier;

                if (fieldName == "value" && modelType == "MultiLanguageProperty")
                {
                    CheckIfValueKeyExists(element);
                }

                var mappingPath = qualifier["value"]?.Value<string>() ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);
                var isMandatory = GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
                var dataFromMappingPath = SelectTokenFromDataJson(data, mappingPath, ctx);

                // If no data is found and the mapping is mandatory an error will be thrown
                if (dataFromMappingPath == null)
                {
                    if (isMandatory)
                    {
                        throw new SubmodelDataToInstanceMapperException($"Mandatory mapping '{mappingPath}' not found.", ctx);
                    }
                    else
                    {
                        ctx.LogWarning($"Optional mapping '{mappingPath}' not found in data, skipping.");
                        continue;
                    }
                }

                AssignField(element, fieldName, dataFromMappingPath, modelType, language, ctx);
                ctx.LogInfo($"Successfully mapped value '{dataFromMappingPath}' from path '{mappingPath}' to field '{fieldName}'");
            }
        }
    }
}
