using Core.Tests.TestFiles;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using Web.Tests.IntegrationTests.Shared;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace Web.Tests.IntegrationTests;

public class TemplatesEndpointTests : IntegrationTestsBase
{
    private const string CustomSubmodelsAasIdFallback = "https://mnestix.com/aas/B9961AFAC3324809AFC5E48D26D55992_3";
    private const string DefaultSubmodelsAasIdFallback = "https://mnestix.com/aas/F11BF9F696A3454EBA0AA4503783F142_4";
    [Test]
    public async Task CreateCustomSubmodel_WhenCalledWithCorrectSubmodelTemplate_ShouldCreateSubmodelInRepository()
    {
        // ARRANGE
        var customTemplate = TestFileProvider.GetBlueprintSubmodelNameplate();
        var submodels = new List<JObject>();
        var content = new StringContent(customTemplate, Encoding.UTF8, "application/json");

        string configurationSettingId = _configuration["Configuration:CustomSubmodelsAasId"] ?? CustomSubmodelsAasIdFallback;
        var customSubmodelsAasId = EncodeTo64(configurationSettingId);

        var mockedRestClient = new MockRestClientBuilder(submodels: submodels)
            .WithPostSubmodel()
            .WithPostSubmodelRefs(customSubmodelsAasId)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("api/template/createCustomSubmodel", content);

        // ASSERT
        submodels.Should().HaveCount(1);
    }

    [Test]
    public async Task UpdateCustomSubmodel_WhenCalledWithUpdatedSubmodelTemplate_ShouldUpdateExistingSubmodelInRepository()
    {
        // ARRANGE
        var customTemplate = JObject.Parse(TestFileProvider.GetBlueprintSubmodelNameplate());
        var submodels = new List<JObject>()
        {
            customTemplate
        };

        var customSubmodelId = customTemplate["id"]?.ToString();
        var testId = Uri.EscapeDataString(customSubmodelId);
        var initialCustomSubmodelIdShort = customTemplate["idShort"]?.ToString();

        var updatedCustomTemplate = (JObject)customTemplate.DeepClone();
        updatedCustomTemplate["idShort"] = "new-Nameplate";
        var updatedCustomSubmodelIdShort = updatedCustomTemplate["idShort"]?.ToString();

        var content = new StringContent(updatedCustomTemplate.ToString(), Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(submodels: submodels)
            .WithPutSubmodel("aHR0cHM6JTJGJTJGd2dycC5iaXolMkZzbSUyRndneCUyRk5hbWVwbGF0ZUZvckdhbGF4aWVEJTJGMSUyRjAlMkZOYW1lcGxhdGVGb3JHYWxheGllRA")
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync($"api/template/updateCustomSubmodel/{testId}", content);

        // ASSERT
        submodels.Should().HaveCount(1);
        submodels[0]?["idShort"]?.ToString().Should().Be(updatedCustomSubmodelIdShort);
    }

    [Test]
    public async Task AddDefaultSubmodel_WhenCalled_ShouldAddSubmodelToRepository()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:DefaultSubmodelsAasId"] ?? DefaultSubmodelsAasIdFallback;
        var defaultSubmodelsAasId = EncodeTo64(configurationSettingId);

        var defaultTemplate = TestFileProvider.GetTemplateSubmodelNameplate();
        var defaultSubmodelId = JObject.Parse(defaultTemplate)["id"]?.ToString();

        var submodels = new List<JObject>();

        var content = new StringContent(defaultTemplate.ToString(), Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(submodels: submodels)
            .WithPostSubmodel()
            .WithPostSubmodelRefs(defaultSubmodelsAasId)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        await PostContentAndEnsureSuccessStatusCodeAsync($"api/template/addDefaultSubmodel", content);

        // ASSERT
        submodels.Should().HaveCount(1);
        submodels[0]?["id"]?.ToString().Should().Be(defaultSubmodelId);
    }

    [Test]
    public async Task GetAllCustomSubmodels_WhenCalled_ShouldCallCorrectEndpoint()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:CustomSubmodelsAasId"] ?? CustomSubmodelsAasIdFallback;
        var customSubmodelsAasId = EncodeTo64(configurationSettingId);
        
        var customSubmodelReference = TestFileProvider.GetBlueprintSubmodelNameplateReference();
        var customSubmodel = JObject.Parse(TestFileProvider.GetBlueprintSubmodelNameplate());
        var customSubmodelId = EncodeTo64(customSubmodel["id"]?.ToString() ?? "");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetSubmodelRefs(customSubmodelsAasId, customSubmodelReference)
            .WithGetSubmodel(customSubmodelId, customSubmodel.ToString());

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync("api/template/allCustomSubmodels");

        // ASSERT
        responseContent.Should().NotBeNull();
        responseContent.Should().Contain("\"id\":\"https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD\"");
        responseContent.Should().Contain("\"idShort\":\"Nameplate\"");

        mockedRestClient.Mock().ShouldHaveCalledGet($"/submodels/{customSubmodelId}");
    }

    [Test]
    public async Task GetCustomSubmodel_WhenCalled_ShouldCallCorrectEndpointAndReturnCorrectSubmodel()
    {
        // ARRANGE
        
        var customSubmodelReference = TestFileProvider.GetBlueprintSubmodelNameplateReference();
        var customSubmodel = JObject.Parse(TestFileProvider.GetBlueprintSubmodelNameplate());
        var customSubmodelId = EncodeTo64(customSubmodel["id"]?.ToString() ?? "");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetSubmodel(customSubmodelId, customSubmodel.ToString())
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync($"api/template/customSubmodel/{customSubmodelId}");

        // ASSERT
        responseContent.Should().NotBeNull();
        responseContent.Should().Contain("\"id\":\"https://wgrp.biz/sm/wgx/NameplateForGalaxieD/1/0/NameplateForGalaxieD\"");
        responseContent.Should().Contain("\"idShort\":\"Nameplate\"");
    }

    [Test]
    public async Task GetAllDefaultSubmodels_WhenCalled_ShouldCallCorrectEndpoint()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:DefaultSubmodelsAasId"] ?? DefaultSubmodelsAasIdFallback;
        var defaultSubmodelsAasId = EncodeTo64(configurationSettingId);

        var defaultTemplate = JObject.Parse(TestFileProvider.GetTemplateSubmodelNameplate());
        var defaultSubmodelReference = TestFileProvider.GetTemplateSubmodelNameplateReference();
        var defaultSubmodelId = EncodeTo64(defaultTemplate["id"]?.ToString() ?? "");

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetSubmodelRefs(defaultSubmodelsAasId, defaultSubmodelReference)
            .WithGetSubmodel(defaultSubmodelId, defaultTemplate.ToString());

        Func<IRestClient> restClientFactory = () => mockedRestClient.Build();
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync($"api/template/allDefaultSubmodels");

        // ASSERT
        responseContent.Should().NotBeNull();
        mockedRestClient.Mock().ShouldHaveCalledGet($"/submodels/{defaultSubmodelId}");
    }

    [Test]
    public async Task DeleteCustomSubmodel_ReturnsNoContent_WhenSuccessful()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:CustomSubmodelsAasId"] ?? CustomSubmodelsAasIdFallback;
        var customSubmodelsAasId = EncodeTo64(configurationSettingId);
        var testSubmodelId = "validBase64SubmodelId";

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodel(testSubmodelId)
            .WithDeleteSubmodelRefs(customSubmodelsAasId, testSubmodelId)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"/api/template/{testSubmodelId}", 204);
    }

    [Test]
    public async Task DeleteCustomSubmodel_ReturnsNotFound_WhenTemplateNotFound()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:CustomSubmodelsAasId"] ?? CustomSubmodelsAasIdFallback;
        var customSubmodelsAasId = EncodeTo64(configurationSettingId);
        var testSubmodelId = "validBase64SubmodelId";

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodel(testSubmodelId, HttpStatusCode.BadRequest)
            .WithDeleteSubmodelRefs(customSubmodelsAasId, testSubmodelId, HttpStatusCode.NotFound)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"/api/template/{testSubmodelId}", 404);
    }

    [Test]
    public async Task DeleteCustomSubmodel_ReturnsBadRequest_WhenErrorOccours()
    {
        // ARRANGE
        string configurationSettingId = _configuration["Configuration:CustomSubmodelsAasId"] ?? CustomSubmodelsAasIdFallback;
        var customSubmodelsAasId = EncodeTo64(configurationSettingId);
        var testSubmodelId = "validBase64SubmodelId";

        var mockedRestClient = new MockRestClientBuilder()
            .WithDeleteSubmodel(testSubmodelId, HttpStatusCode.BadRequest)
            .WithDeleteSubmodelRefs(customSubmodelsAasId, testSubmodelId, HttpStatusCode.BadRequest)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT && ASSERT
        await DeleteAndEnsureSuccessStatusCodeAsync($"/api/template/{testSubmodelId}", 400);
    }
}