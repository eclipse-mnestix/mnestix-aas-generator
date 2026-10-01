using Jsonata.Net.Native;

namespace MnestixCore.TemplateBuilder;

/// <summary>
/// Inspects a parsed JSONata expression for constructs that are forbidden in stored
/// blueprint expressions because they cannot be governed by the evaluation timeout.
/// </summary>
public interface IJsonataConstructInspector
{
    /// <summary>
    /// Returns a description of the first forbidden construct found in the expression,
    /// or null when the expression is safe.
    /// </summary>
    string? FindForbiddenConstruct(JsonataQuery query);
}
