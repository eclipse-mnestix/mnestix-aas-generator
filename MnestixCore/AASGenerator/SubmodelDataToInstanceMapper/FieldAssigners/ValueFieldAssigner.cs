using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the "value" field with model-type-aware logic:
/// - MultiLanguageProperty: converts scalar to lang array (requires language parameter)
/// - Property/Blob/File: validates scalar type and valueType conformance
/// - Others: assigns directly
/// </summary>
public sealed class ValueFieldAssigner : FieldAssignerBase
{
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

    public override string FieldName => "value";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (modelType == "MultiLanguageProperty")
        {
            AssignMultiLanguagePropertyValue(element, resolvedValue, language, ctx);
            return;
        }

        // Per AAS JSON schema, Property, Blob, and File value must be a string (scalar).
        if (modelType is "Property" or "Blob" or "File" && resolvedValue.Type is JTokenType.Object or JTokenType.Array)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"'{modelType}' value must be a scalar (string, number, boolean), but the mapping expression returned {resolvedValue.Type}", ctx);
        }

        ValidateValueType(element, resolvedValue, ctx);

        WarnIfOverridingDefault(element, "value", ctx);
        element["value"] = resolvedValue.DeepClone();
    }

    private static void AssignMultiLanguagePropertyValue(JToken element, JToken resolvedValue, string? language, DataMappingContext ctx)
    {
        if (string.IsNullOrEmpty(language))
        {
            throw new SubmodelDataToInstanceMapperException(
                "MultiLanguageProperty with SMT/MappingInfo/value requires a 'language' parameter in the request. Use SMT/MappingInfo/multiLanguage to provide language codes in the data instead.", ctx);
        }

        if (resolvedValue.Type is not (JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean or JTokenType.Null))
        {
            throw new SubmodelDataToInstanceMapperException(
                $"MultiLanguageProperty expects a string, number, boolean, or null value, but got {resolvedValue.Type}", ctx);
        }

        WarnIfOverridingDefault(element, "value", ctx);
        element["value"] = new JArray
        {
            new JObject
            {
                { "text", resolvedValue.ToString() },
                { "language", language }
            }
        };
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
}
