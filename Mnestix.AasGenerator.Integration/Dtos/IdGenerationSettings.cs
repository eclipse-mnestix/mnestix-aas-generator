using MnestixCore.Dtos.Enums;

namespace MnestixCore.Dtos;

/// <summary>
/// The settings for id generation which is used for creating new AAS or submodels.
/// </summary>
/// <param name="aasIdPrefix">e.g.: https://example.com/aas/</param>
/// <param name="aasIdDynamicPart">e.g.: GUID or AssetIdShort or AASidShort</param>
/// <param name="aasIdShortPrefix">e.g.: aas_</param>
/// <param name="aasIdShortDynamicPart">e.g.: GUID or AssetIdShort</param>
/// <param name="assetIdPrefix">e.g.: https://example.com/</param>
/// <param name="assetIdDynamicPart">e.g.: GUID or AssetIdShort</param>
/// <param name="assetIdShortPrefix"></param>
/// <param name="assetIdShortDynamicPart">e.g.: GUID</param>
/// <param name="subModelIdPrefix">e.g.: https://example.com/sm/</param>
/// <param name="subModelIdDynamicPart">e.g.: GUID</param>
public record IdGenerationSettings(
    string aasIdPrefix,
    AasIdDynamicPart aasIdDynamicPart,
    string aasIdShortPrefix,
    AasIdShortDynamicPart aasIdShortDynamicPart,
    string assetIdPrefix,
    AssetIdDynamicPart assetIdDynamicPart,
    string assetIdShortPrefix,
    AssetIdShortDynamicPart assetIdShortDynamicPart,
    string subModelIdPrefix,
    SubmodelIdDynamicPart subModelIdDynamicPart);