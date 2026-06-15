namespace MnestixCore.Dtos;

/// <summary>
/// Error response returned when an AAS already exists and overwrite=false.
/// </summary>
public class CreateAasConflictResponse
{
    /// <summary>
    /// Human readable error description.
    /// </summary>
    public string Error { get; init; } = null!;

    /// <summary>
    /// Ids of submodels that were POSTed in this request but could not be rolled back.
    /// Empty when rollback fully succeeded.
    /// </summary>
    public IEnumerable<string> OrphanedSubmodelIds { get; init; } = Enumerable.Empty<string>();
}
