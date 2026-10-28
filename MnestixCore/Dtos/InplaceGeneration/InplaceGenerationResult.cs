using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos.InplaceGeneration;

/// <summary>
/// Outcome of building a single submodel in memory from a blueprint, including the generated submodel body.
/// No repository write happens for this result - the caller is responsible for persisting it.
/// </summary>
public class InplaceGenerationResult
{
    /// <summary>
    /// The blueprint id this result is referencing to
    /// </summary>
    public string BlueprintId { get; init; } = null!;

    /// <summary>
    /// Indicates whether building the submodel was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The id of the generated submodel if building was successful
    /// </summary>
    public string GeneratedSubmodelId { get; init; } = "";

    /// <summary>
    /// The generated submodel body, ready to be persisted by the caller. Null when <see cref="Success"/> is false.
    /// </summary>
    public JObject? Submodel { get; init; }

    /// <summary>
    /// Structured error information with a machine-readable error code and typed context. Set when Success is false.
    /// </summary>
    public AasGeneratorErrorDto? Error { get; init; }

    /// <summary>
    /// Generation process trace. Present when Success is false; present on success only when debug mode is enabled.
    /// </summary>
    public IList<string>? Logs { get; init; }
}
