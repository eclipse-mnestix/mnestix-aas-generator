using MnestixCore.Dtos;

namespace MnestixCore.IdGenerator.Interfaces;

public interface IAasIdGeneratorService
{
    /// <summary>
    /// Generates a set of ids which is used to create a new AAS.
    /// </summary>
    /// <param name="assetIdShortParam">Optional parameter which holds the assetIdShort which can be used as part of the generated ids.</param>
    /// <param name="globalAssetId">Optional globalAssetId to use directly instead of generating one.</param>
    /// <returns>Task which holds <see cref="assetIdShortParam"/></returns>
    public Task<AasIds> GenerateAasIdsAsync(string? assetIdShortParam = null, string? globalAssetId = null);

    /// <summary>
    /// Generates ids for submodels which are used to create new submodels in AAS.
    /// </summary>
    /// <param name="count">Amount of requested submodel ids</param>
    /// <returns>Task which holds list of ids</returns>
    public Task<List<string>> GenerateSubmodelIdsAsync(uint count = 1);
}