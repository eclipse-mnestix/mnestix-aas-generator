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
    /// Assigns the resolved data value to the target element.
    /// Default: element[FieldName] = resolvedValue.ToString().
    /// </summary>
    public virtual void Assign(JToken element, JToken resolvedValue, string modelType, string? language, DataMappingContext ctx)
    {
        element[FieldName] = resolvedValue.ToString();
    }
}
