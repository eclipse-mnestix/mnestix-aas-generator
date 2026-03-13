using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using Web.Tests.IntegrationTests.Shared;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace Web.Tests.IntegrationTests;

public class BlueprintsEndpointTests : IntegrationTestsBase
{
    private const string BlueprintsAasIdFallback = "https://mnestix.com/aas/B9961AFAC3324809AFC5E48D26D55992_3";
    private const string BlueprintsBaseRoute = "api/v2/Blueprints";

    [Test]
    public async Task GetAllBlueprints_WhenCalled_ShouldReturnBlueprintsFromRepository()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:BlueprintsAasId"] ?? BlueprintsAasIdFallback;
        var blueprintsAasId = EncodeTo64(configurationSettingId);

        var blueprintReference = TestFileProvider.GetBlueprintSubmodelNameplateReference();
        var blueprint = TestFileProvider.GetBlueprintSubmodelNameplate();
        var blueprintId = JObject.Parse(blueprint)["id"]?.ToString() ?? string.Empty;
        var encodedBlueprintId = EncodeTo64(blueprintId);

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetSubmodelRefs(blueprintsAasId, blueprintReference)
            .WithGetSubmodel(encodedBlueprintId, blueprint);

        IRestClient restClientFactory() => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync(BlueprintsBaseRoute);

        // ASSERT
        responseContent.Should().NotBeNull();
        responseContent.Should().Contain("\"idShort\":\"Nameplate\"");
        mockedRestClient.Mock().ShouldHaveCalledGet($"/submodels/{encodedBlueprintId}");
    }

    [Test]
    public async Task GetBlueprintById_WhenExists_ShouldReturnBlueprint()
    {
        // ARRANGE
        var blueprint = TestFileProvider.GetBlueprintSubmodelNameplate();
        var blueprintId = JObject.Parse(blueprint)["id"]?.ToString() ?? string.Empty;
        var encodedBlueprintId = EncodeTo64(blueprintId);

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetSubmodel(encodedBlueprintId, blueprint);

        IRestClient restClientFactory() => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync($"{BlueprintsBaseRoute}/{Uri.EscapeDataString(encodedBlueprintId)}");

        // ASSERT
        responseContent.Should().Contain("\"id\":\"https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD\"");
    }

    [Test]
    public async Task CreateBlueprint_WhenPayloadValid_ShouldPersistBlueprint()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:BlueprintsAasId"] ?? BlueprintsAasIdFallback;
        var blueprintsAasId = EncodeTo64(configurationSettingId);

        var blueprintJson = TestFileProvider.GetExampleBlueprintJson();
        var content = new StringContent(blueprintJson, Encoding.UTF8, "application/json");

        var persistedBlueprints = new List<JObject>();
        var mockedRestClient = new MockRestClientBuilder(submodels: persistedBlueprints)
            .WithPostSubmodel()
            .WithPostSubmodelRefs(blueprintsAasId);

        IRestClient restClientFactory() => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync(BlueprintsBaseRoute, content);

        // ASSERT
        persistedBlueprints.Should().HaveCount(1);
        var persistedBlueprint = persistedBlueprints.Single();
        persistedBlueprint["kind"]?.ToString().Should().Be("Instance");
        persistedBlueprint["id"]?.ToString().Should().Contain("_Template_");

        var qualifiers = (JArray?)persistedBlueprint["qualifiers"];
        qualifiers.Should().NotBeNull();
        qualifiers!.Any(q => q?["type"]?.ToString() == "displayName").Should().BeTrue();
    }

    [Test]
    public async Task UpdateBlueprint_WhenPayloadValid_ShouldUpdateBlueprint()
    {
        // ARRANGE
        var originalBlueprint = JObject.Parse(TestFileProvider.GetExampleBlueprintJson());
        var submodelId = originalBlueprint["id"]?.ToString() ?? string.Empty;
        var encodedSubmodelId = EncodeTo64(submodelId);

        var persistedBlueprints = new List<JObject>
        {
            (JObject)originalBlueprint.DeepClone()
        };

        var updatedBlueprint = (JObject)originalBlueprint.DeepClone();
        updatedBlueprint["idShort"] = "UpdatedBlueprint";

        var content = new StringContent(updatedBlueprint.ToString(), Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(submodels: persistedBlueprints)
            .WithPutSubmodel(encodedSubmodelId);

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync($"{BlueprintsBaseRoute}/{Uri.EscapeDataString(submodelId)}", content, StatusCodes.Status204NoContent);

        // ASSERT
        persistedBlueprints.Should().HaveCount(1);
        persistedBlueprints[0]["idShort"]?.ToString().Should().Be("UpdatedBlueprint");
    }

    [Test]
    public async Task DeleteBlueprint_ReturnsNoContent_WhenSuccessful()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:BlueprintsAasId"] ?? BlueprintsAasIdFallback;
        var blueprintsAasId = EncodeTo64(configurationSettingId);
        var encodedBlueprintId = EncodeTo64("https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD");

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodel(encodedBlueprintId)
            .WithDeleteSubmodelRefs(blueprintsAasId, encodedBlueprintId);

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"{BlueprintsBaseRoute}/{Uri.EscapeDataString(encodedBlueprintId)}", StatusCodes.Status204NoContent);
    }

    [Test]
    public async Task DeleteBlueprint_ReturnsNotFound_WhenBlueprintMissing()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:BlueprintsAasId"] ?? BlueprintsAasIdFallback;
        var blueprintsAasId = EncodeTo64(configurationSettingId);
        var encodedBlueprintId = EncodeTo64("https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD");

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodelRefs(blueprintsAasId, encodedBlueprintId, HttpStatusCode.NotFound)
            .WithDeleteSubmodel(encodedBlueprintId);

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"{BlueprintsBaseRoute}/{Uri.EscapeDataString(encodedBlueprintId)}", StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task DeleteBlueprint_ReturnsBadRequest_WhenDeletionFails()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:BlueprintsAasId"] ?? BlueprintsAasIdFallback;
        var blueprintAasId = EncodeTo64(configurationSettingId);
        var encodedBlueprintId = EncodeTo64("https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD");

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodelRefs(blueprintAasId, encodedBlueprintId, HttpStatusCode.BadRequest)
            .WithDeleteSubmodel(encodedBlueprintId, HttpStatusCode.BadRequest);

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"{BlueprintsBaseRoute}/{Uri.EscapeDataString(encodedBlueprintId)}", StatusCodes.Status400BadRequest);
    }
}