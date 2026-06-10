using MnestixCore.AasGenerator;
using MnestixCore.Dtos;

namespace MnestixCore.AasCreator;

/// <summary>
/// Result of the creation of a new AAS with optional submodels.
/// </summary>
/// <param name="aasIds">The set of ids of the AAS to create.</param>
/// <param name="status">Indicates whether the creation of the AAS was successful.</param>
/// <param name="submodelResults">Results from submodel generation. Empty if no submodels were requested.</param>
/// <param name="aasRepoUrl">Repository Url where the AAS got created</param>
/// <param name="errorMessage">
/// An error message that can be set in case of unknown errors.
/// </param>
public record AasCreationWithSubmodelsResult(
    AasIds aasIds, 
    AasCreationStatus status, 
    IEnumerable<AasGeneratorResult> submodelResults,
    string? aasRepoUrl = null,
    string? errorMessage = null);
