using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder;
using MnestixCore.TemplateBuilder.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasGenerator;

public class AasGeneratorPrimitivesTests
{
    private MnestixCore.AasGenerator.AasGenerator _aasGenerator = null!;
    private IDataMapper _dataToInstanceMapper = null!;
    private Mock<IRepoProxyClient> _repoProxyClientMock = null!;
    private Mock<IBlueprintProvider> _blueprintProviderMock = null!;
    private Mock<IAasIdGeneratorService> _idGeneratorMock = null!;
    private Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>> _loggerMock = null!;
    private const string NewSubmodelId = "TheNewSubmodelId";
    private const string TestSubmodelPath = "/submodels";
    private const string TestAasPath = "/aas";
    private const string TestBase64EncodedAasId = "dGVzdEFhc0lk";

    [SetUp]
    public void SetUp()
    {
        _dataToInstanceMapper = new DataMapper(new BlueprintValidator());
        _repoProxyClientMock = new Mock<IRepoProxyClient>();
        _blueprintProviderMock = new Mock<IBlueprintProvider>();
        _idGeneratorMock = new Mock<IAasIdGeneratorService>();
        _loggerMock = new Mock<ILogger<MnestixCore.AasGenerator.AasGenerator>>();

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = TestAasPath,
            SubmodelPath = TestSubmodelPath,
        };

        _aasGenerator = new MnestixCore.AasGenerator.AasGenerator(
            _dataToInstanceMapper,
            _repoProxyClientMock.Object,
            _blueprintProviderMock.Object,
            _idGeneratorMock.Object,
            Options.Create(repoProxyOptions),
            _loggerMock.Object);

        _idGeneratorMock.Setup(x => x.GenerateSubmodelIdsAsync(It.IsAny<uint>()))
            .ReturnsAsync(new List<string> { NewSubmodelId });
    }

    [Test]
    public async Task BuildSubmodelAsync_ValidBlueprint_ReturnsInstanceAndDoesNotWriteToRepo()
    {
        // ARRANGE
        var blueprint = DataIngestTestFileProvider.GetTemplateSubmodel("MandatoryAndOptionalField");
        var data = DataIngestTestFileProvider.GetData("MandatoryAndOptionalField");
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync(It.IsAny<string>())).ReturnsAsync(blueprint);

        // ACT
        var built = await _aasGenerator.BuildSubmodelAsync("urn:smtemplate:DemoTemplate", data, "en");

        // ASSERT
        built.Result.Success.Should().BeTrue();
        built.Instance.Should().NotBeNull();
        built.Result.GeneratedSubmodelId.Should().Be(NewSubmodelId);
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task BuildSubmodelAsync_InvalidBlueprint_ReturnsFailureAndDoesNotWriteToRepo()
    {
        // ARRANGE — InputOnlyOptionalField fails mandatory mapping
        var blueprint = DataIngestTestFileProvider.GetTemplateSubmodel("InputOnlyOptionalField");
        var data = DataIngestTestFileProvider.GetData("InputOnlyOptionalField");
        _blueprintProviderMock.Setup(x => x.GetBlueprintAsync(It.IsAny<string>())).ReturnsAsync(blueprint);

        // ACT
        var built = await _aasGenerator.BuildSubmodelAsync("urn:smtemplate:DemoTemplate", data, "en");

        // ASSERT
        built.Result.Success.Should().BeFalse();
        built.Instance.Should().BeNull();
        _repoProxyClientMock.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PostSubmodelAsync_PostsBodyToSubmodelPath_ReturnsId()
    {
        // ARRANGE
        var instance = new JObject { ["id"] = "submodel-xyz", ["modelType"] = "Submodel" };
        string? capturedPath = null;
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, _) => capturedPath = path)
            .ReturnsAsync("created");

        // ACT
        var id = await _aasGenerator.PostSubmodelAsync(instance);

        // ASSERT
        id.Should().Be("submodel-xyz");
        capturedPath.Should().Be(TestSubmodelPath);
        _repoProxyClientMock.Verify(x => x.PostAsync(TestSubmodelPath, It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task AttachSubmodelRefAsync_PostsRefToShell()
    {
        // ARRANGE
        string? capturedPath = null;
        _repoProxyClientMock
            .Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((path, _) => capturedPath = path)
            .ReturnsAsync("created");

        // ACT
        await _aasGenerator.AttachSubmodelRefAsync(TestBase64EncodedAasId, "submodel-xyz");

        // ASSERT
        capturedPath.Should().Be($"{TestAasPath}/{TestBase64EncodedAasId}/submodel-refs");
    }
}
