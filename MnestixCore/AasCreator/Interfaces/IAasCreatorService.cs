using MnestixCore.Dtos;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasCreator.Interfaces;

public interface IAasCreatorService
{
    /// <summary>
    /// Create an AAS for the given <paramref name="assetIdShortParam" />.
    /// </summary>
    /// <param name="assetIdShortParam">Short identifier of the asset</param>
    /// <param name="globalAssetId">Optional globalAssetId to use directly instead of generating one.</param>
    /// <param name="options">Optional configuration for AAS metadata (assetKind, extensions, specificAssetIds, administration, defaultThumbnail, derivedFrom)</param>
    /// <returns><see cref="AasCreationResult"/></returns>
    public Task<AasCreationResult> CreateAasAsync(string assetIdShortParam, string? globalAssetId = null, AasCreationOptions? options = null);

    /// <summary>
    /// Create an AAS for the given <paramref name="assetIdShortParam" /> with optional submodels.
    /// If submodel parameters are provided, submodels will be generated first before creating the AAS.
    /// If submodel generation fails, the AAS will not be created.
    /// </summary>
    /// <param name="assetIdShortParam">Short identifier of the asset</param>
    /// <param name="input">Optional input parameters for AAS and submodel creation</param>
    /// <param name="overwrite">When true, an existing shell with the generated id is overwritten instead of returning a conflict.</param>
    /// <returns><see cref="AasCreationWithSubmodelsResult"/></returns>
    public Task<AasCreationWithSubmodelsResult> CreateAasWithSubmodelsAsync(
        string assetIdShortParam,
        CreateAasParameters? input = null,
        bool overwrite = false);
}