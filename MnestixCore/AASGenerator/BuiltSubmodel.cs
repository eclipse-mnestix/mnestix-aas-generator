using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator;

/// <summary>
/// Outcome of building a single submodel instance in memory from a blueprint, before any repository write.
/// </summary>
public class BuiltSubmodel
{
    /// <summary>
    /// Per-blueprint result carrying success state, generated id, blueprint id, error/debug info.
    /// </summary>
    public AasGeneratorResult Result { get; init; } = null!;

    /// <summary>
    /// The built submodel instance. Null when <see cref="AasGeneratorResult.Success"/> is false.
    /// </summary>
    public JObject? Instance { get; init; }
}
