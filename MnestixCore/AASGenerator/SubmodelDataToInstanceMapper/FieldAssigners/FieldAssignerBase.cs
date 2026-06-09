using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.FieldAssigners;

/// <summary>
/// Base class for field assigners. The default behavior assigns the resolved value
/// as a string to element[FieldName]. Derived classes override for complex behaviors.
/// </summary>
internal abstract class FieldAssignerBase
{
    public abstract string FieldName { get; }

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
