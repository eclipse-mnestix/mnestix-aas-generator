using Newtonsoft.Json.Linq;
using MnestixCore.TemplateBuilder;

namespace Mnestix.AasGenerator;

/// <summary>
/// A single blueprint paired with the submodel id to assign to the generated instance.
/// </summary>
/// <param name="BlueprintId">Identifier of the source blueprint (used only for correlating results).</param>
/// <param name="Blueprint">The blueprint document the submodel instance is generated from.</param>
/// <param name="SubmodelId">The id assigned to the produced submodel instance (supplied by the caller).</param>
public sealed record SubmodelGenerationRequest(string BlueprintId, JObject Blueprint, string SubmodelId);

/// <summary>
/// Structured error information for a failed submodel generation.
/// </summary>
public sealed record GenerationErrorInfo
{
    public IList<string>? Logs { get; init; }
    public string? Qualifier { get; init; }
    public string? QualifierPath { get; init; }
}

/// <summary>
/// Debug information (workflow logs) for a submodel generation.
/// </summary>
public sealed record GenerationDebugInfo
{
    public IList<string>? Logs { get; init; }
}

/// <summary>
/// Result of generating a single submodel from a blueprint.
/// Carries the produced submodel object on success; Core does not persist it.
/// </summary>
public sealed record SubmodelGenerationResult
{
    public string BlueprintId { get; init; } = null!;
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string GeneratedSubmodelId { get; init; } = "";
    public JObject? Submodel { get; init; }
    public GenerationErrorInfo? ErrorInfo { get; init; }
    public GenerationDebugInfo? DebugInfo { get; init; }
    public IReadOnlyList<BlueprintValidationError>? ValidationErrors { get; init; }
}

/// <summary>
/// The full output of a composite generation: the AAS shell JSON plus the
/// per-blueprint submodel results (each carrying the produced submodel object
/// or its failure info).
/// </summary>
public sealed record AasGenerationResult(
    string AasJson,
    IReadOnlyList<SubmodelGenerationResult> SubmodelResults)
{
    /// <summary>True when every requested submodel was generated successfully.</summary>
    public bool Success => SubmodelResults.All(r => r.Success);

    /// <summary>The successfully produced submodel objects.</summary>
    public IEnumerable<JObject> Submodels =>
        SubmodelResults.Where(r => r.Success && r.Submodel is not null).Select(r => r.Submodel!);
}
