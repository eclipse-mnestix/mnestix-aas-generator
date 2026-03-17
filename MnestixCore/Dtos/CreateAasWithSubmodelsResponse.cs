using MnestixCore.AasGenerator;

namespace MnestixCore.Dtos;

/// <summary>
/// Response for creating an AAS with optional submodels.
/// Contains AAS creation info and submodel generation results if submodels were requested.
/// </summary>
public class CreateAasResponse
{
    /// <summary>
    /// The assetId, e.g.:https://domain.biz/xdtzq0F
    /// </summary>
    public string AssetId { get; init; } = null!;
    
    /// <summary>
    /// The assetId encoded in base64
    /// </summary>
    public string Base64EncodedAssetId { get; init; } = null!;
    
    /// <summary>
    /// The aasId, e.g.: aas_xdtzq0F
    /// </summary>
    public string AasId { get; init; } = null!;
    
    /// <summary>
    /// The aasId encoded in base64
    /// </summary>
    public string Base64EncodedAasId { get; init; } = null!;

    /// <summary>
    /// Repository URL where the AAS gets stored.
    /// </summary>
    public string AasRepoUrl { get; init; } = null!;
    
    /// <summary>
    /// Results from submodel generation. Empty if no submodels were requested.
    /// </summary>
    public IEnumerable<AasGeneratorResult> SubmodelResults { get; init; } = Enumerable.Empty<AasGeneratorResult>();
}
