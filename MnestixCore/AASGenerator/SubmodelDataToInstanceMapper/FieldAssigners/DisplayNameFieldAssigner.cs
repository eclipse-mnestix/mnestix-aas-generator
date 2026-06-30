using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Assigns the displayName Referable attribute.
/// Two input shapes are supported:
/// <list type="bullet">
/// <item>A language-keyed map (JObject), e.g. <c>{ "en": "Voltage", "de": "Spannung" }</c>,
/// is converted (mirroring <see cref="MultiLanguageFieldAssigner"/>) into a list of
/// language strings that <b>replaces</b> the displayName array.</item>
/// <item>A scalar value is written as a single language entry using the generation
/// language (find-or-add by language), preserving other languages' template defaults.</item>
/// </list>
/// </summary>
public sealed class DisplayNameFieldAssigner : FieldAssignerBase
{
    public override string FieldName => "displayName";

    public override bool IsAlwaysOptional => true;

    public override bool IsResolvedValueMissing(JToken resolvedValue) => IsEmptyLanguageMap(resolvedValue);

    public override void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        if (resolvedValue is JObject langObject)
        {
            AssignFromLanguageMap(element, langObject, ctx);
            return;
        }

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

    /// <summary>
    /// Builds a language-string list from a language-keyed map and replaces the displayName
    /// array. Mirrors <see cref="MultiLanguageFieldAssigner"/>: null/empty-string entries are
    /// skipped. If no valid entries remain, the attribute is left untouched (omitted) rather
    /// than written as an empty array.
    /// </summary>
    private void AssignFromLanguageMap(JToken element, JObject langObject, DataMappingContext ctx)
    {
        var langArray = new JArray();
        foreach (var prop in langObject.Properties())
        {
            if (prop.Value.Type == JTokenType.Null || string.IsNullOrEmpty(prop.Value.ToString()))
            {
                continue;
            }

            langArray.Add(new JObject
            {
                ["language"] = prop.Name,
                ["text"] = prop.Value.ToString()
            });
        }

        if (langArray.Count == 0)
        {
            return;
        }

        WarnIfOverridingDefault(element, this.FieldName, ctx);
        element[this.FieldName] = langArray;
    }
}
