namespace MnestixCore.Dtos;

/// <summary>
/// Set of ids which is used to create a new AAS.
/// </summary>
/// <param name="assetId">The assetId, e.g.:https://domain.biz/xdtzq0F</param>
/// <param name="assetIdShort">The assetIdShort, e.g.: xdtzq0F</param>
/// <param name="aasId">The aasId, e.g.: aas_xdtzq0F</param>
/// <param name="aasIdShort">The aasIdShort, e.g.: https://domain.biz/aas/xdtzq0F</param>
public record AasIds(string assetId, string assetIdShort, string aasId, string aasIdShort);