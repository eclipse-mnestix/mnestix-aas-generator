using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns displayName as a language-aware array entry (find-or-add by language).
/// </summary>
public sealed class DisplayNameFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "displayName";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        var displayNameArray = element["displayName"] as JArray;
        if (displayNameArray == null)
        {
            displayNameArray = new JArray();
            element["displayName"] = displayNameArray;
        }

        var langEntry = displayNameArray.FirstOrDefault(e => e["language"]?.Value<string>() == language);
        if (langEntry != null)
        {
            var existingText = langEntry["text"]?.Value<string>();
            if (!string.IsNullOrEmpty(existingText))
            {
                var idShort = element["idShort"]?.Value<string>() ?? "(unknown)";
                ctx.LogWarning($"Element '{idShort}': template default for 'displayName' was overridden by mapped data");
            }
            langEntry["text"] = resolvedValue.ToString();
        }
        else
        {
            displayNameArray.Add(new JObject
            {
                ["language"] = language,
                ["text"] = resolvedValue.ToString()
            });
        }
    }
}
