using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.RequiredShellsAssertion;
using Moq;

namespace Core.Tests.RequiredShellsAssertionTests;

public class RequiredShellsAssertionTests
{
    [Test]
    public async Task AssertRequiredShellsAsync_TemplateBlacklistedAndApiConfigured_SkipsRepositoryLookup()
    {
        // ARRANGE
        var repoProxyClientMock = new Mock<IRepoProxyClient>(MockBehavior.Strict);
        var configurationOptions = new ConfigurationOptions
        {
            SubmodelTemplatesApiUrl = "https://templates.mnestix.example",
            SubmodelBlueprintsApiUrl = string.Empty
        };

        var shellAssertionService = CreateShellAssertionService(
            repoProxyClientMock.Object,
            "DefaultTemplate",
            configurationOptions);

        // ACT
        await shellAssertionService.AssertRequiredShellsAsync();

        // ASSERT
        repoProxyClientMock.Verify(client => client.GetAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AssertRequiredShellsAsync_BlueprintBlacklistedAndApiConfigured_SkipsRepositoryLookup()
    {
        // ARRANGE
        var repoProxyClientMock = new Mock<IRepoProxyClient>(MockBehavior.Strict);
        var configurationOptions = new ConfigurationOptions
        {
            SubmodelTemplatesApiUrl = string.Empty,
            SubmodelBlueprintsApiUrl = "https://blueprints.mnestix.example"
        };

        var shellAssertionService = CreateShellAssertionService(
            repoProxyClientMock.Object,
            "CustomTemplate",
            configurationOptions);

        // ACT
        await shellAssertionService.AssertRequiredShellsAsync();

        // ASSERT
        repoProxyClientMock.Verify(client => client.GetAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AssertRequiredShellsAsync_TemplateBlacklistedButApiMissing_ContinuesRepositoryLookup()
    {
        // ARRANGE
        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock
            .Setup(client => client.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((true, string.Empty));

        var configurationOptions = new ConfigurationOptions
        {
            SubmodelTemplatesApiUrl = string.Empty,
            SubmodelBlueprintsApiUrl = string.Empty
        };

        var shellAssertionService = CreateShellAssertionService(
            repoProxyClientMock.Object,
            "DefaultTemplate",
            configurationOptions);

        // ACT
        await shellAssertionService.AssertRequiredShellsAsync();

        // ASSERT
        repoProxyClientMock.Verify(client => client.GetAsync(It.IsAny<string>()), Times.Once);
    }

    private static RequiredShellsAssertion CreateShellAssertionService(
        IRepoProxyClient repoProxyClient,
        string requiredShellName,
        ConfigurationOptions configurationOptions)
    {
        var requiredShell = new RequiredShells
        {
            Name = requiredShellName,
            Base64EncodedAasId = "encodedAasId"
        };

        return new RequiredShellsAssertion(
            Mock.Of<ILogger<RequiredShellsAssertion>>(),
            Options.Create(new List<RequiredShells> { requiredShell }),
            repoProxyClient,
            Options.Create(new RepoProxyOptions
            {
                AasPath = "aas",
                SubmodelPath = "submodels"
            }),
            Options.Create(configurationOptions));
    }
}