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
            .ReturnsAsync(string.Empty);

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

    [TestCase("Mnestix")]
    [TestCase("lni0729")]
    public async Task AssertRequiredShellsAsync_ExampleAasDisabled_SkipsExampleShellButKeepsConfiguration(string exampleShellName)
    {
        // ARRANGE
        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock
            .Setup(client => client.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        var exampleShell = new RequiredShells { Name = exampleShellName, Base64EncodedAasId = "exampleAasId" };
        var configurationShell = new RequiredShells { Name = "Configuration", Base64EncodedAasId = "configurationAasId", SkipIfAlreadyExists = true };

        var shellAssertionService = CreateShellAssertionService(
            repoProxyClientMock.Object,
            new ConfigurationOptions(),
            exampleShell, configurationShell);

        // ACT
        await shellAssertionService.AssertRequiredShellsAsync(addExampleAas: false);

        // ASSERT
        repoProxyClientMock.Verify(client => client.GetAsync(It.Is<string>(s => s.Contains("exampleAasId"))), Times.Never);
        repoProxyClientMock.Verify(client => client.GetAsync(It.Is<string>(s => s.Contains("configurationAasId"))), Times.Once);
    }

    [Test]
    public async Task AssertRequiredShellsAsync_ExampleAasEnabled_ChecksExampleAndConfigurationShell()
    {
        // ARRANGE
        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock
            .Setup(client => client.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(string.Empty);

        var exampleShell = new RequiredShells { Name = "Mnestix", Base64EncodedAasId = "exampleAasId" };
        var configurationShell = new RequiredShells { Name = "Configuration", Base64EncodedAasId = "configurationAasId", SkipIfAlreadyExists = true };

        var shellAssertionService = CreateShellAssertionService(
            repoProxyClientMock.Object,
            new ConfigurationOptions(),
            exampleShell, configurationShell);

        // ACT
        await shellAssertionService.AssertRequiredShellsAsync(addExampleAas: true);

        // ASSERT
        repoProxyClientMock.Verify(client => client.GetAsync(It.Is<string>(s => s.Contains("exampleAasId"))), Times.Once);
        repoProxyClientMock.Verify(client => client.GetAsync(It.Is<string>(s => s.Contains("configurationAasId"))), Times.Once);
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

        return CreateShellAssertionService(repoProxyClient, configurationOptions, requiredShell);
    }

    private static RequiredShellsAssertion CreateShellAssertionService(
        IRepoProxyClient repoProxyClient,
        ConfigurationOptions configurationOptions,
        params RequiredShells[] requiredShells)
    {
        return new RequiredShellsAssertion(
            Mock.Of<ILogger<RequiredShellsAssertion>>(),
            Options.Create(requiredShells.ToList()),
            repoProxyClient,
            Options.Create(new RepoProxyOptions
            {
                AasPath = "aas",
                SubmodelPath = "submodels"
            }),
            Options.Create(configurationOptions));
    }
}