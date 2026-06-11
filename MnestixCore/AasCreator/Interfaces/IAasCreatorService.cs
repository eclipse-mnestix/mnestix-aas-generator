using Newtonsoft.Json.Linq;

namespace MnestixCore.AasCreator.Interfaces;

public interface IAasCreatorService
{
    /// <summary>
    /// Create an AAS for the given <paramref name="assetIdShortParam" />.
    /// </summary>
    /// <param name="assetIdShortParam">Short identifier of the asset</param>
    /// <param name="globalAssetId">Optional globalAssetId to use directly instead of generating one.</param>
    /// <returns><see cref="AasCreationResult"/></returns>
    public Task<AasCreationResult> CreateAasAsync(string assetIdShortParam, string? globalAssetId = null);

    /// <summary>
    /// Create an AAS for the given <paramref name="assetIdShortParam" /> with optional submodels.
    /// If submodel parameters are provided, submodels will be generated first before creating the AAS.
    /// If submodel generation fails, the AAS will not be created.
    /// </summary>
    /// <param name="assetIdShortParam">Short identifier of the asset</param>
    /// <param name="blueprintsIds">Optional list of blueprint IDs to generate submodels</param>
    /// <param name="data">Optional data JSON for populating the blueprints</param>
    /// <param name="language">Optional language for multi-language properties</param>
    /// <param name="debug">Optional flag to include debug logs in the response</param>
    /// <param name="globalAssetId">Optional globalAssetId to use directly instead of generating one.</param>
    /// <returns><see cref="AasCreationWithSubmodelsResult"/></returns>
    public Task<AasCreationWithSubmodelsResult> CreateAasWithSubmodelsAsync(
        string assetIdShortParam,
        IEnumerable<string>? blueprintsIds = null,
        JObject? data = null,
        string? language = null,
        bool debug = false,
        string? globalAssetId = null);
}