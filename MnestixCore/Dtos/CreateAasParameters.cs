using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos;

/// <summary>
/// Input parameters for creating an AAS with optional submodels.
/// Service-layer DTO independent of API contract.
/// </summary>
public class CreateAasParameters
{
    /// <summary>
    /// Optional list of blueprint IDs to generate submodels from.
    /// </summary>
    public IEnumerable<string>? BlueprintsIds { get; init; }

    /// <summary>
    /// Optional data JSON for populating the blueprint templates.
    /// </summary>
    public JObject? Data { get; init; }

    /// <summary>
    /// Optional language code for multi-language properties (e.g., "en", "de").
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Whether to include debug logs in the response.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Optional globalAssetId to use directly instead of generating one.
    /// </summary>
    public string? GlobalAssetId { get; init; }

    /// <summary>
    /// Optional AAS metadata configuration (assetKind, extensions, specificAssetIds, etc.).
    /// </summary>
    public AasCreationOptions? Metadata { get; init; }

    /// <summary>
    /// Optional list of existing submodel IDs to link to the AAS.
    /// </summary>
    public IEnumerable<string>? SubmodelIds { get; init; }

    /// <summary>
    /// Creates an instance from a CreateAasRequest (API request DTO).
    /// </summary>
    /// <param name="request">The API request body.</param>
    /// <returns>A new CreateAasParameters instance, or null if request is null.</returns>
    public static CreateAasParameters? FromRequest(CreateAasRequest? request)
    {
        if (request == null)
        {
            return null;
        }

        return new CreateAasParameters
        {
            BlueprintsIds = request.BlueprintsIds,
            Data = request.Data,
            Language = request.Language,
            Debug = request.Debug,
            GlobalAssetId = request.GlobalAssetId,
            SubmodelIds = request.SubmodelIds,
            Metadata = new AasCreationOptions
            {
                AssetKind = request.AssetKind ?? AssetKind.Instance,
                Extensions = request.Extensions,
                SpecificAssetIds = request.SpecificAssetIds,
                Administration = request.Administration,
                DefaultThumbnail = request.DefaultThumbnail,
                DerivedFrom = request.DerivedFrom
            }
        };
    }
}
