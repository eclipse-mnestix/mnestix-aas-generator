using MnestixCore.Dtos;

namespace MnestixCore.AasCreator;

/// <summary>
/// Result of the creation of a new AAS.
/// </summary>
/// <param name="aasId">The set of ids of the AAS to create.</param>
/// <param name="status">Indicates whether the creation of the AAS was successful.</param>
/// <param name="errorMessage">
/// An error message that can be set in case of unknown errors.
/// </param>
public record AasCreationResult(AasIds aasIds, AasCreationStatus status, string? errorMessage = null);