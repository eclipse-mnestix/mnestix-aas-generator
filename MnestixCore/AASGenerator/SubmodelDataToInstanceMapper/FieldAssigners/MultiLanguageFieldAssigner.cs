using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns MultiLanguageProperty from a JSON object with language keys.
/// Writes to element["value"] as a lang array.
/// </summary>
public sealed class MultiLanguageFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "multiLanguage";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (resolvedValue is not JObject langObject)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"SMT/MappingInfo/multiLanguage expects a JSON object with language keys, but got {resolvedValue.Type}", ctx);
        }

        if (!langObject.HasValues)
        {
            element["value"] = new JArray();
            return;
        }

        var langArray = new JArray();
        foreach (var prop in langObject.Properties())
        {
            if (prop.Value.Type == JTokenType.Null || string.IsNullOrEmpty(prop.Value.ToString()))
            {
                continue;
            }

            langArray.Add(new JObject
            {
                ["text"] = prop.Value.ToString(),
                ["language"] = prop.Name
            });
        }

        if (langArray.Count == 0)
        {
            element["value"] = new JArray();
            return;
        }

        element["value"] = langArray;
    }
}
