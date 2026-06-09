using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns displayName as a language-aware array entry (find-or-add by language).
/// </summary>
internal sealed class DisplayNameFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "displayName";

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (language == null)
        {
            var idShort = element["idShort"]?.Value<string>() ?? "(unknown)";
            ctx.LogWarning($"Element '{idShort}': skipping 'displayName' assignment because no language is specified");
            return;
        }

        var displayNameArray = element[this.FieldName] as JArray;
        if (displayNameArray == null)
        {
            displayNameArray = new JArray();
            element[this.FieldName] = displayNameArray;
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
