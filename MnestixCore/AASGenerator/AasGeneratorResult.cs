using MnestixCore.Errors;

namespace MnestixCore.AasGenerator;

public class AasGeneratorResult
{
    /// <summary>
    /// The blueprint id this result is referencing to
    /// </summary>
    public string BlueprintId { get; init; } = null!;
    /// <summary>
    /// Indicates whether the creation of the submodel was successful
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// The id of the new submodel if the creation was successful
    /// </summary>
    public string GeneratedSubmodelId { get; set; } = "";
    /// <summary>
    /// Structured error information with a machine-readable error code and typed context. Set when Success is false.
    /// </summary>
    public AasGeneratorErrorDto? Error { get; init; }
    /// <summary>
    /// Generation process trace. Present when Success is false and the generator was started; null if the request
    /// was rejected before generation began (e.g. invalid AAS ID). Present on success only when debug mode is enabled.
    /// </summary>
    public IList<string>? Logs { get; init; }
}
