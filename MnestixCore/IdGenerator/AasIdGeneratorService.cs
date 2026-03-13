using MnestixCore.Dtos;
using MnestixCore.Dtos.Enums;
using MnestixCore.IdGenerator.Interfaces;

namespace MnestixCore.IdGenerator;

public class AasIdGeneratorService : IAasIdGeneratorService
{
    private readonly IMnestixConfigurationProvider _mnestixConfigurationProvider;

    public AasIdGeneratorService(IMnestixConfigurationProvider mnestixConfigurationProvider)
    {
        _mnestixConfigurationProvider = mnestixConfigurationProvider;
    }

    /// <inheritdoc />
    public async Task<AasIds> GenerateAasIdsAsync(string? assetIdShortParam = null)
    {
        var idGenerationSettings = await _mnestixConfigurationProvider.GetIdGenerationSettingsAsync();

        var assetIdShort = GenerateAssetIdShort(assetIdShortParam, idGenerationSettings);
        var assetId = GenerateAssetId(assetIdShort, idGenerationSettings);
        var aasIdShort = GenerateAasIdShort(assetIdShort, idGenerationSettings);
        var aasId = GenerateAasId(assetIdShort, aasIdShort, idGenerationSettings);

        return new AasIds(assetId, assetIdShort, aasId, aasIdShort);
    }

    /// <inheritdoc />
    public async Task<List<string>> GenerateSubmodelIdsAsync(uint count = 1)
    {
        var idGenerationSettings = await _mnestixConfigurationProvider.GetIdGenerationSettingsAsync();

        var submodelIds = new List<string>();

        for (var i = 1; i <= count; i++)
        {
            submodelIds.Add(idGenerationSettings.subModelIdPrefix +
                            StandardConformGuidGenerator.GenerateStandardConformGuid());
        }

        return submodelIds;
    }

    private static string GenerateAssetId(string assetIdShort, IdGenerationSettings idGenerationSettings)
    {
        if (idGenerationSettings.assetIdDynamicPart == AssetIdDynamicPart.AssetIdShort)
        {
            return idGenerationSettings.assetIdPrefix + assetIdShort;
        }

        return idGenerationSettings.assetIdPrefix + StandardConformGuidGenerator.GenerateStandardConformGuid();
    }

    private static string GenerateAssetIdShort(string? assetIdShort, IdGenerationSettings idGenerationSettings)
    {
        return !string.IsNullOrEmpty(assetIdShort) ? assetIdShort : StandardConformGuidGenerator.GenerateStandardConformGuid();
    }

    private static string GenerateAasIdShort(string? assetIdShort, IdGenerationSettings idGenerationSettings)
    {
        if (idGenerationSettings.aasIdShortDynamicPart == AasIdShortDynamicPart.AssetIdShort)
        {
            return idGenerationSettings.aasIdShortPrefix + assetIdShort;
        }

        return idGenerationSettings.aasIdShortPrefix + StandardConformGuidGenerator.GenerateStandardConformGuid();

    }

    private static string GenerateAasId(string? assetIdShort, string aasIdShort, IdGenerationSettings idGenerationSettings)
    {
        return idGenerationSettings.aasIdDynamicPart switch
        {
            AasIdDynamicPart.AssetIdShort => idGenerationSettings.aasIdPrefix + assetIdShort,
            AasIdDynamicPart.AASidShort => idGenerationSettings.aasIdPrefix + aasIdShort,
            _ => idGenerationSettings.aasIdPrefix + StandardConformGuidGenerator.GenerateStandardConformGuid()
        };
    }
}