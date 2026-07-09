using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Base class for field assigners. The default behavior assigns the resolved value
/// as a string to element[FieldName]. Derived classes override for complex behaviors.
/// </summary>
public abstract class FieldAssignerBase
{
    public abstract string FieldName { get; }

    /// <summary>
    /// When true, the field is always optional regardless of the element's cardinality
    /// (e.g. displayName: a property without a mapped display name must still be generated).
    /// </summary>
    public virtual bool IsAlwaysOptional => false;

    /// <summary>
    /// Determines whether a resolved value should be treated as missing, so the mapping is
    /// omitted (when optional) or fails (when mandatory). Default: never. Language-map fields
    /// override this to treat an empty / all-empty object as missing.
    /// </summary>
    public virtual bool IsResolvedValueMissing(JToken resolvedValue) => false;

    /// <summary>
    /// Assigns the resolved data value to the target element.
    /// Default: element[FieldName] = resolvedValue.ToString().
    /// </summary>
    public virtual void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        WarnIfOverridingDefault(element, FieldName, ctx);
        element[FieldName] = resolvedValue.ToString();
    }

    /// <summary>
    /// Returns true when the value is a language-keyed map with no usable entries
    /// (empty object, or every entry is null/empty), i.e. nothing to assign.
    /// </summary>
    protected static bool IsEmptyLanguageMap(JToken value) =>
        value is JObject obj &&
        (!obj.HasValues || obj.Properties().All(p =>
            p.Value.Type == JTokenType.Null || string.IsNullOrEmpty(p.Value.ToString())));

    /// <summary>
    /// Logs a warning when the target field already has a non-null, non-empty value
    /// that will be overwritten by mapped data. This helps users notice when template
    /// defaults are silently replaced.
    /// </summary>
    protected static void WarnIfOverridingDefault(JToken element, string fieldName, DataMappingContext ctx)
    {
        var existing = element[fieldName];
        if (existing == null) return;

        var isEmpty = existing.Type switch
        {
            JTokenType.Null => true,
            JTokenType.String => string.IsNullOrEmpty(existing.Value<string>()),
            JTokenType.Array => !((JArray)existing).HasValues,
            _ => false
        };

        if (isEmpty) return;

        var idShort = element["idShort"]?.Value<string>() ?? "(unknown)";
        ctx.LogWarning($"Element '{idShort}': template default for '{fieldName}' was overridden by mapped data");
    }
}
