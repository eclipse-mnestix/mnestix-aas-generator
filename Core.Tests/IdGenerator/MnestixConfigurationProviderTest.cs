using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Dtos.Enums;
using MnestixCore.IdGenerator;
using MnestixCore.RepoProxyClient.Interfaces;
using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Core.Tests.IdGenerator;
public class MnestixConfigurationProviderTest
{
    private ILogger<MnestixConfigurationProvider> _loggerMock;

    public MnestixConfigurationProviderTest()
    {
        _loggerMock = new Mock<ILogger<MnestixConfigurationProvider>>().Object;
    }

    [Test]
    public async Task
        GetIdGenerationSettingsAsync_SubmodelDynamicPartValuesSetButNoPrefixes_ReturnSettingsObjectWithEmptyStringsForPrefixes()
    {
        // ARRANGE
        var aasProviderMock = new Mock<IRepoProxyClient>();
        var submodelWithDynamicPartValues = TestFileProvider.GetIdGeneratorSettingsSubmodelWithDynamicPartValues();
        aasProviderMock.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submodelWithDynamicPartValues);
        
        var repoProxyOptionsMock = new Mock<IOptions<RepoProxyOptions>>();
        repoProxyOptionsMock.Setup(s => s.Value).Returns(new RepoProxyOptions());

        var configurationOptionsMock = new Mock<IOptions<ConfigurationOptions>>();
        configurationOptionsMock.Setup(s => s.Value).Returns(new ConfigurationOptions());
        var cut = new MnestixConfigurationProvider(aasProviderMock.Object, repoProxyOptionsMock.Object, configurationOptionsMock.Object, _loggerMock);

        // ACT
        var result = await cut.GetIdGenerationSettingsAsync();

        // ASSERT
        result.aasIdPrefix.Should().BeEmpty();
        result.aasIdDynamicPart.Should().Be(AasIdDynamicPart.GUID);
        result.aasIdShortPrefix.Should().BeEmpty();
        result.aasIdShortDynamicPart.Should().Be(AasIdShortDynamicPart.GUID);
        result.assetIdPrefix.Should().BeEmpty();
        result.assetIdDynamicPart.Should().Be(AssetIdDynamicPart.GUID);
        result.assetIdShortPrefix.Should().BeEmpty();
        result.assetIdShortDynamicPart.Should().Be(AssetIdShortDynamicPart.GUID);
        result.subModelIdPrefix.Should().BeEmpty();
        result.subModelIdDynamicPart.Should().Be(SubmodelIdDynamicPart.GUID);
    }

    [Test]
    public async Task GetIdGenerationSettingsAsync_SubmodelContainsValues_ReturnSettingsObjectWithCorrectValues()
    {
        // ARRANGE
        var aasProviderMock = new Mock<IRepoProxyClient>();
        var submodelWithEmptyValues = TestFileProvider.GetIdGeneratorSettingsSubmodelWithValues();
        aasProviderMock.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submodelWithEmptyValues);
        
        var repoProxyOptionsMock = new Mock<IOptions<RepoProxyOptions>>();
        repoProxyOptionsMock.Setup(s => s.Value).Returns(new RepoProxyOptions());

        var configurationOptionsMock = new Mock<IOptions<ConfigurationOptions>>();
        configurationOptionsMock.Setup(s => s.Value).Returns(new ConfigurationOptions());
        var cut = new MnestixConfigurationProvider(aasProviderMock.Object, repoProxyOptionsMock.Object, configurationOptionsMock.Object, _loggerMock);

        // ACT
        var result = await cut.GetIdGenerationSettingsAsync();

        // ASSERT
        result.aasIdPrefix.Should().Be("https://example.com/aas/");
        result.aasIdDynamicPart.Should().Be(AasIdDynamicPart.AssetIdShort);
        result.aasIdShortPrefix.Should().Be("aas_");
        result.aasIdShortDynamicPart.Should().Be(AasIdShortDynamicPart.AssetIdShort);
        result.assetIdPrefix.Should().Be("assetIdPrefix");
        result.assetIdDynamicPart.Should().Be(AssetIdDynamicPart.AssetIdShort);
        result.assetIdShortPrefix.Should().Be("assetIdShortPrefix");
        result.assetIdShortDynamicPart.Should().Be(AssetIdShortDynamicPart.GUID);
        result.subModelIdPrefix.Should().Be("submodelIdPrefix");
        result.subModelIdDynamicPart.Should().Be(SubmodelIdDynamicPart.GUID);
    }

    [Test]
    public async Task GetIdGenerationSettingsAsync_SubmodelContainsNoValues_ReturnSettingsObjectWithDefaultValues()
    {
        // ARRANGE
        var aasProviderMock = new Mock<IRepoProxyClient>();
        var submodelWithEmptyValues = TestFileProvider.GetIdGeneratorSettingsSubmodelWithoutValues();
        aasProviderMock.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submodelWithEmptyValues);
        
        var repoProxyOptionsMock = new Mock<IOptions<RepoProxyOptions>>();
        repoProxyOptionsMock.Setup(s => s.Value).Returns(new RepoProxyOptions());

        var configurationOptionsMock = new Mock<IOptions<ConfigurationOptions>>();
        configurationOptionsMock.Setup(s => s.Value).Returns(new ConfigurationOptions());
        var cut = new MnestixConfigurationProvider(aasProviderMock.Object, repoProxyOptionsMock.Object, configurationOptionsMock.Object, _loggerMock);

        // ACT
        var result = await cut.GetIdGenerationSettingsAsync();

        // ASSERT
        result.aasIdPrefix.Should().Be(string.Empty);
        result.aasIdDynamicPart.Should().Be(AasIdDynamicPart.GUID);
        result.aasIdShortPrefix.Should().Be(string.Empty);
        result.aasIdShortDynamicPart.Should().Be(AasIdShortDynamicPart.GUID);
        result.assetIdPrefix.Should().Be(string.Empty);
        result.assetIdDynamicPart.Should().Be(AssetIdDynamicPart.GUID);
        result.assetIdShortPrefix.Should().Be(string.Empty);
        result.assetIdShortDynamicPart.Should().Be(AssetIdShortDynamicPart.GUID);
        result.subModelIdPrefix.Should().Be(string.Empty);
        result.subModelIdDynamicPart.Should().Be(SubmodelIdDynamicPart.GUID);
    }
}