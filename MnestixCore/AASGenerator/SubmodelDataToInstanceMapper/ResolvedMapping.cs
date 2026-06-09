using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines;

/// <summary>
/// Pairs a validated mapping descriptor with its resolved data value (JSONata evaluation result).
/// A null ResolvedValue means the expression did not match any data.
/// </summary>
internal sealed class ResolvedMapping
{
    public required MappingDescriptor Descriptor { get; init; }

    /// <summary>The result of evaluating the JSONata expression. Null if path was not found.</summary>
    public JToken? ResolvedValue { get; init; }
}
