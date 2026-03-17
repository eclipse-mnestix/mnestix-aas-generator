using MnestixCore.AasCreator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace Core.Tests.AasCreator;

public class AasCreatorTest
{
    private readonly Mock<IAasIdGeneratorService> _aasIdGeneratorService = new();
    private readonly Mock<IRepoProxyClient> _repoProxyClientMock = new();
    private readonly Mock<IAasGenerator> _aasGeneratorMock = new();
    private readonly IOptions<RepoProxyOptions> _repoProxyOptions = Options.Create(new RepoProxyOptions());

    [Test]
    public async Task CreateAas_NewAssetIdShortGiven_RepoClientCalledAndNewAasIdReturned()
    {
        // ARRANGE
        const string randomAssetIdShort = "assetId123";
        var aasIds = GetTestAasIds(randomAssetIdShort);

        const bool correspondingAasAlreadyExists = false;
        var aasSentToRepo = "";
        var expectedAasSentToRepo = TestFileProvider.GetExampleAasJson();

        _aasIdGeneratorService.Setup(a => a.GenerateAasIdsAsync(It.IsAny<string>())).ReturnsAsync(aasIds);
        _repoProxyClientMock.Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(new ValueTuple<bool, string>(correspondingAasAlreadyExists, ""));
        _repoProxyClientMock
            .Setup(r => r.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback((string _, string content) => aasSentToRepo = content)
            .ReturnsAsync("");
        var aasCreator =
            new AasCreatorService(_aasIdGeneratorService.Object, _repoProxyClientMock.Object, _repoProxyOptions, _aasGeneratorMock.Object);

        // ACT
        var result = await aasCreator.CreateAasAsync(randomAssetIdShort);

        // ASSERT
        result.status.Should().Be(AasCreationStatus.Created);
        result.aasIds.Should().Be(aasIds);
        aasSentToRepo.Should().Be(expectedAasSentToRepo);
    }

    [Test]
    public async Task CreateAas_PreexistingAssetIdShortGiven_RepoClientNotCalledAndErrorMessageReturned()
    {
        // ARRANGE
        var randomAssetIdShort = Guid.NewGuid().ToString();
        var aasIds = GetTestAasIds(randomAssetIdShort);

        const bool correspondingAasAlreadyExists = true;
        var callsToRepo = 0;

        _aasIdGeneratorService.Setup(a => a.GenerateAasIdsAsync(It.IsAny<string>())).ReturnsAsync(aasIds);
        _repoProxyClientMock.Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(new ValueTuple<bool, string>(correspondingAasAlreadyExists, ""));
        _repoProxyClientMock
            .Setup(r => r.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => callsToRepo++)
            .ReturnsAsync("");
        var aasCreator = new AasCreatorService(_aasIdGeneratorService.Object, _repoProxyClientMock.Object,
            _repoProxyOptions, _aasGeneratorMock.Object);

        // ACT
        var result = await aasCreator.CreateAasAsync(randomAssetIdShort);

        // ASSERT
        result.status.Should().Be(AasCreationStatus.AlreadyExists);
        result.aasIds.aasId.Should().Be("https://example.com/aas/" + randomAssetIdShort);
        callsToRepo.Should().Be(0);
    }

    private static AasIds GetTestAasIds(string assetIdShort)
    {
        var aasIds = new AasIds("https://example.com/" + assetIdShort,
            assetIdShort,
            "https://example.com/aas/" + assetIdShort,
            "aas_" + assetIdShort);
        return aasIds;
    }
}