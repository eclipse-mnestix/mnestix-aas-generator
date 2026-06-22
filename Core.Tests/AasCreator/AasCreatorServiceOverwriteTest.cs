using MnestixCore.AasCreator;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using FluentAssertions;
using System.Net;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;

namespace Core.Tests.AasCreator;

public class AasCreatorServiceOverwriteTest
{
    private Mock<IAasIdGeneratorService> _idGen = null!;
    private Mock<IRepoProxyClient> _repo = null!;
    private Mock<IAasGenerator> _generator = null!;
    private IOptions<RepoProxyOptions> _options = null!;

    private const string AssetIdShort = "asset123";
    private string _base64AasId = null!;

    [SetUp]
    public void SetUp()
    {
        _idGen = new Mock<IAasIdGeneratorService>();
        _repo = new Mock<IRepoProxyClient>();
        _generator = new Mock<IAasGenerator>();
        _options = Options.Create(new RepoProxyOptions { AasPath = "shells", SubmodelPath = "submodels" });

        var aasIds = GetTestAasIds(AssetIdShort);
        _base64AasId = MnestixCore.Shared.Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);
        _idGen.Setup(x => x.GenerateAasIdsAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(aasIds);
        _repo.Setup(x => x.GetAasRepositoryUrl()).Returns("https://repo.example.com");
    }

    private AasCreatorService CreateService() =>
        new(_idGen.Object, _repo.Object, _options, _generator.Object);

    private static AasIds GetTestAasIds(string assetIdShort) =>
        new("https://example.com/" + assetIdShort,
            assetIdShort,
            "https://example.com/aas/" + assetIdShort,
            "aas_" + assetIdShort);

    private void SetupBuild(string blueprintId, string submodelId, bool success = true)
    {
        var instance = new JObject { ["id"] = submodelId, ["modelType"] = "Submodel" };
        _generator.Setup(g => g.BuildSubmodelAsync(blueprintId, It.IsAny<JObject>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>()))
            .ReturnsAsync(new BuiltSubmodel
            {
                Instance = success ? instance : null,
                Result = new AasGeneratorResult { Success = success, BlueprintId = blueprintId, GeneratedSubmodelId = success ? submodelId : "" }
            });
    }

    [Test]
    public async Task CreateAasWithSubmodels_NoBlueprints_PostsShellOnce_ReturnsCreated()
    {
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>())).ReturnsAsync("");
        var service = CreateService();

        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort);

        result.status.Should().Be(AasCreationStatus.Created);
        _repo.Verify(x => x.PostAsync("shells", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task CreateAasWithSubmodels_ValidBlueprints_PostsSubmodelsBeforeShell_ShellCarriesRefs()
    {
        SetupBuild("bp1", "sm1");
        SetupBuild("bp2", "sm2");
        var callOrder = new List<string>();
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>()))
            .ReturnsAsync((JObject j) => j["id"]!.ToString())
            .Callback<JObject>(j => callOrder.Add("submodel:" + j["id"]));
        string? shellJson = null;
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>()))
            .Callback<string, string>((_, body) => { shellJson = body; callOrder.Add("shell"); })
            .ReturnsAsync("");

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1", "bp2" }, new JObject(), "en");

        result.status.Should().Be(AasCreationStatus.Created);
        callOrder.Should().ContainInOrder("submodel:sm1", "submodel:sm2", "shell");
        callOrder.Last().Should().Be("shell");
        // shell must carry refs to both submodels (baked into shell, not attached separately)
        shellJson.Should().NotBeNull();
        var refValues = JObject.Parse(shellJson!)["submodels"]!
            .SelectTokens("$..keys[*].value")
            .Select(v => v.ToString());
        refValues.Should().Contain(new[] { "sm1", "sm2" });
    }

    [Test]
    public async Task CreateAasWithSubmodels_ValidationFails_NoRepoWrites()
    {
        SetupBuild("bp1", "", success: false);
        var service = CreateService();

        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en");

        result.status.Should().Be(AasCreationStatus.GenerationFailed);
        _repo.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _generator.Verify(g => g.PostSubmodelAsync(It.IsAny<JObject>()), Times.Never);
    }

    [Test]
    public async Task CreateAasWithSubmodels_SubmodelPostFailsOnSecond_DeletesFirst_ShellNeverPosted()
    {
        SetupBuild("bp1", "sm1");
        SetupBuild("bp2", "sm2");
        _generator.Setup(g => g.PostSubmodelAsync(It.Is<JObject>(j => j["id"]!.ToString() == "sm1")))
            .ReturnsAsync("sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.Is<JObject>(j => j["id"]!.ToString() == "sm2")))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "boom"));

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1", "bp2" }, new JObject(), "en");

        result.status.Should().Be(AasCreationStatus.UnknownError);
        _repo.Verify(x => x.DeleteAsync("submodels/" + MnestixCore.Shared.Base64StringDeAndEncoder.EncodeTo64("sm1")), Times.Once);
        _repo.Verify(x => x.PostAsync("shells", It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CreateAasWithSubmodels_OverwriteFalse_ShellConflict_RollsBackSubmodels_ReturnsConflict()
    {
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>())).ReturnsAsync((JObject j) => j["id"]!.ToString());
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "conflict", HttpStatusCode.Conflict, "exists"));
        _repo.Setup(x => x.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en", overwrite: false);

        result.status.Should().Be(AasCreationStatus.Conflict);
        result.orphanedSubmodelIds.Should().BeEmpty();
        _repo.Verify(x => x.DeleteAsync("submodels/" + MnestixCore.Shared.Base64StringDeAndEncoder.EncodeTo64("sm1")), Times.Once);
        // never delete the shell
        _repo.Verify(x => x.DeleteAsync(It.Is<string>(p => p.StartsWith("shells/"))), Times.Never);
    }

    [Test]
    public async Task CreateAasWithSubmodels_OverwriteFalse_Conflict_RollbackDeleteFails_ReportsOrphan()
    {
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>())).ReturnsAsync((JObject j) => j["id"]!.ToString());
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "conflict", HttpStatusCode.Conflict, "exists"));
        _repo.Setup(x => x.DeleteAsync(It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotDelete, "nope"));

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en", overwrite: false);

        result.status.Should().Be(AasCreationStatus.Conflict);
        result.orphanedSubmodelIds.Should().Contain("sm1");
    }

    [Test]
    public async Task CreateAasWithSubmodels_OverwriteTrue_NoExisting_ReturnsCreated_NoPreviousAas()
    {
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>())).ReturnsAsync((JObject j) => j["id"]!.ToString());
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>())).ReturnsAsync("");

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en", overwrite: true);

        result.status.Should().Be(AasCreationStatus.Created);
        result.previousAas.Should().BeNull();
        _repo.Verify(x => x.PutAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task CreateAasWithSubmodels_OverwriteTrue_ShellConflict_GetsOldThenPuts_ReturnsOverwrittenWithPreviousAas()
    {
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>())).ReturnsAsync((JObject j) => j["id"]!.ToString());
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "conflict", HttpStatusCode.Conflict, "exists"));
        _repo.Setup(x => x.GetAsync("shells/" + _base64AasId)).ReturnsAsync("{\"id\":\"old-shell\",\"idShort\":\"old\"}");
        _repo.Setup(x => x.PutAsync("shells/" + _base64AasId, It.IsAny<string>())).ReturnsAsync("");

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en", overwrite: true);

        result.status.Should().Be(AasCreationStatus.Overwritten);
        result.previousAas.Should().NotBeNull();
        result.previousAas!["id"]!.ToString().Should().Be("old-shell");
        _repo.Verify(x => x.PutAsync("shells/" + _base64AasId, It.IsAny<string>()), Times.Once);
        _repo.Verify(x => x.DeleteAsync(It.Is<string>(p => p.StartsWith("shells/"))), Times.Never);
    }

    [Test]
    public async Task CreateAasWithSubmodels_OverwriteTrue_Conflict_PutFails_RollsBackSubmodels()
    {
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>())).ReturnsAsync((JObject j) => j["id"]!.ToString());
        _repo.Setup(x => x.PostAsync("shells", It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "conflict", HttpStatusCode.Conflict, "exists"));
        _repo.Setup(x => x.GetAsync(It.IsAny<string>())).ReturnsAsync("{\"id\":\"old-shell\"}");
        _repo.Setup(x => x.PutAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "put failed"));
        _repo.Setup(x => x.DeleteAsync(It.IsAny<string>())).ReturnsAsync(true);

        var service = CreateService();
        var result = await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en", overwrite: true);

        result.status.Should().Be(AasCreationStatus.UnknownError);
        result.orphanedSubmodelIds.Should().BeEmpty();
        _repo.Verify(x => x.DeleteAsync("submodels/" + MnestixCore.Shared.Base64StringDeAndEncoder.EncodeTo64("sm1")), Times.Once);
    }

    [Test]
    public async Task CreateAasWithSubmodels_SubmodelFailure_NeverDeletesShell()
    {
        // Regression guard for the original destructive-rollback bug.
        SetupBuild("bp1", "sm1");
        _generator.Setup(g => g.PostSubmodelAsync(It.IsAny<JObject>()))
            .ThrowsAsync(new RepoProxyException(ErrorCodes.CouldNotPostShell, "boom"));

        var service = CreateService();
        await service.CreateAasWithSubmodelsAsync(AssetIdShort, new[] { "bp1" }, new JObject(), "en");

        _repo.Verify(x => x.DeleteAsync(It.Is<string>(p => p.StartsWith("shells/"))), Times.Never);
    }
}
