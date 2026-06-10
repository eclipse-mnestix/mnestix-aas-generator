using MnestixCore.Dtos;
using MnestixCore.Dtos.Enums;
using MnestixCore.IdGenerator;
using MnestixCore.IdGenerator.Interfaces;
using FluentAssertions;
using Moq;

namespace Core.Tests.IdGenerator;

public class IdGeneratorServiceTest
{
    [Test]
    public async Task
        GenerateAasIdsAsync_AssetIdGivenAndAllDynamicPartsConfiguredToUseIt_CorrectIdsWithGivenAssetIdReturned()
    {
        // ARRANGE
        var mnestixConfigurationProviderMock = new Mock<IMnestixConfigurationProvider>();
        mnestixConfigurationProviderMock
            .Setup(s => s.GetIdGenerationSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdGenerationSettingsConfiguredToUseAssetIdShort);

        var cut = new AasIdGeneratorService(mnestixConfigurationProviderMock.Object);
        var randomAssetIdShort = Guid.NewGuid().ToString();

        // ACT
        var aasIds = await cut.GenerateAasIdsAsync(randomAssetIdShort);

        // ASSERT
        aasIds.aasId.Should().Be(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdPrefix + randomAssetIdShort);
        aasIds.aasIdShort.Should()
            .Be(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdShortPrefix + randomAssetIdShort);
        aasIds.assetId.Should().Be(IdGenerationSettingsConfiguredToUseAssetIdShort.assetIdPrefix + randomAssetIdShort);
    }


    [Test]
    public async Task
        GenerateAasIdsAsync_NoAssetIdGivenAndAllDynamicPartsConfiguredToUseAssetIdShort_IdsWithSameRandomAssetIdReturned()
    {
        // ARRANGE
        var mnestixConfigurationProviderMock = new Mock<IMnestixConfigurationProvider>();
        mnestixConfigurationProviderMock
            .Setup(s => s.GetIdGenerationSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdGenerationSettingsConfiguredToUseAssetIdShort);

        var cut = new AasIdGeneratorService(mnestixConfigurationProviderMock.Object);

        // ACT
        var aasIds = await cut.GenerateAasIdsAsync();

        // ASSERT - the ids must end with the same generated id
        var aasId_Id = aasIds.aasId.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdPrefix, "");
        var aasIdShort_Id =
            aasIds.aasIdShort.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdShortPrefix, "");
        var assetId_Id = aasIds.assetId.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.assetIdPrefix, "");
        var assetIdShort_Id = aasIds.assetIdShort.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.assetIdShortPrefix, "");

        aasId_Id.Should().BeEquivalentTo(aasIdShort_Id);
        aasIdShort_Id.Should().BeEquivalentTo(assetId_Id);
        assetId_Id.Should().BeEquivalentTo(assetIdShort_Id);
    }

    [Test]
    public async Task
        GenerateAasIdsAsync_NoAssetIdGivenAndAllDynamicPartsConfiguredToUseGUID_AllIdsWithRandomGeneratedDifferentGuidsReturned()
    {
        // ARRANGE
        var expectedGuidLength = 32;
        var mnestixConfigurationProviderMock = new Mock<IMnestixConfigurationProvider>();
        mnestixConfigurationProviderMock
            .Setup(s => s.GetIdGenerationSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdGenerationSettingsConfiguredToUseGuids);

        var cut = new AasIdGeneratorService(mnestixConfigurationProviderMock.Object);

        // ACT
        var aasIds = await cut.GenerateAasIdsAsync();

        // ASSERT - the ids must not end with the same generated id
        var aasId_Id = aasIds.aasId.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdPrefix, "");
        var aasIdShort_Id =
            aasIds.aasIdShort.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.aasIdShortPrefix, "");
        var assetId_Id = aasIds.assetId.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.assetIdPrefix, "");
        var assetIdShort_Id = aasIds.assetIdShort.Replace(IdGenerationSettingsConfiguredToUseAssetIdShort.assetIdShortPrefix, "");

        aasId_Id.Length.Should().Be(expectedGuidLength);
        aasIdShort_Id.Length.Should().Be(expectedGuidLength);
        assetId_Id.Length.Should().Be(expectedGuidLength);
        assetIdShort_Id.Length.Should().Be(expectedGuidLength);

        aasId_Id.Should().NotBeEquivalentTo(aasIdShort_Id);
        aasIdShort_Id.Should().NotBeEquivalentTo(assetId_Id);
        assetId_Id.Should().NotBeEquivalentTo(assetIdShort_Id);
    }

    [Test]
    public async Task GenerateSubmodelIdsAsync_RequestTenSubmodelIds_TenSubmodelIdsWithCorrectPrefixReturned()
    {
        // ARRANGE
        uint count = 10;
        var mnestixConfigurationProviderMock = new Mock<IMnestixConfigurationProvider>();
        mnestixConfigurationProviderMock
            .Setup(s => s.GetIdGenerationSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdGenerationSettingsConfiguredToUseAssetIdShort);

        var cut = new AasIdGeneratorService(mnestixConfigurationProviderMock.Object);

        // ACT
        var submodelIds = await cut.GenerateSubmodelIdsAsync(count);

        // ASSERT
        submodelIds.Count.Should().Be(10);
        submodelIds
            .FindAll(s => s.StartsWith(IdGenerationSettingsConfiguredToUseAssetIdShort.subModelIdPrefix)).Count
            .Should()
            .Be(10);
    }

    private static IdGenerationSettings IdGenerationSettingsConfiguredToUseAssetIdShort
    {
        get
        {
            var idGenerationSettings = new IdGenerationSettings(
                "https://example.com/aas/",
                AasIdDynamicPart.AssetIdShort,
                "aas_",
                AasIdShortDynamicPart.AssetIdShort,
                "https://example.com/",
                AssetIdDynamicPart.AssetIdShort,
                "assetIdShortPrefix",
                AssetIdShortDynamicPart.GUID,
                "https://example.com/sm/",
                SubmodelIdDynamicPart.GUID
            );
            return idGenerationSettings;
        }
    }

    private static IdGenerationSettings IdGenerationSettingsConfiguredToUseGuids
    {
        get
        {
            var idGenerationSettings = new IdGenerationSettings(
                "https://example.com/aas/",
                AasIdDynamicPart.GUID,
                "aas_",
                AasIdShortDynamicPart.GUID,
                "https://example.com/",
                AssetIdDynamicPart.GUID,
                "assetIdShortPrefix",
                AssetIdShortDynamicPart.GUID,
                "https://example.com/sm/",
                SubmodelIdDynamicPart.GUID
            );
            return idGenerationSettings;
        }
    }
}