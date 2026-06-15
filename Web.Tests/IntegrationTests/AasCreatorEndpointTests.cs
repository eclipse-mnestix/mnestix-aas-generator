using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using Web.Tests.IntegrationTests.Shared;

namespace Web.Tests.IntegrationTests;

public class AasCreatorEndpointTests : IntegrationTestsBase
{

    [Test]
    public async Task CreateAas_WithoutRequestBody_ShouldReturnCreated()
    {
        // ARRANGE
        var mockedRestClient = new MockRestClientBuilder()
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFz", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func <IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAas", null, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAas\"");
        responseContent.Should().Contain("\"submodelResults\":[]");
    }

    [Test]
    public async Task CreateAas_WithSubmodels_ShouldReturnCreatedWithSubmodelResultsAndShellRefs()
    {
        // ARRANGE
        var blueprintSubmodel = TestFileProvider.GetExampleBlueprintJson();
        var blueprintIdBase64 = "TmFtZXBsYXRlX1RlbXBsYXRlXzViZjBkZjk4LWUxNDMtNDdiMS04ZDNlLTQyMTgwYjQwODg2Yg";
        var submodels = new List<JObject>();
        var aasList = new List<JObject>();

        var serialNumberTest = "123456789";
        var manufacturerNameTest = "Test Manufacturer";

        var json = $@"
            {{
              ""language"": ""de"",
              ""data"": {{
                ""SerialNumber"": ""{serialNumberTest}"",
                ""ManufacturerName"": ""{manufacturerNameTest}""
              }},
              ""blueprintsIds"": [
                ""Nameplate_Template_5bf0df98-e143-47b1-8d3e-42180b40886b""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList, submodels: submodels)
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFzV2l0aFN1Ym1vZGVscw", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .WithGetSubmodel(blueprintIdBase64, blueprintSubmodel, HttpStatusCode.OK)
            .WithPostSubmodel()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasWithSubmodels", content, StatusCodes.Status201Created);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAasWithSubmodels\"");
        responseContent.Should().Contain("\"submodelResults\":");
        aasList.Should().HaveCount(1);
        submodels.Should().HaveCount(1);

        // submodel must be persisted before the shell, and the shell must carry the ref
        var persistedSubmodelId = submodels[0]["id"]?.ToString();
        persistedSubmodelId.Should().NotBeNullOrEmpty();
        var shellRefs = aasList[0]["submodels"] as JArray;
        shellRefs.Should().NotBeNull();
        shellRefs!.ToString().Should().Contain(persistedSubmodelId!);

        var addedSubmodel = submodels[0];
        var elements = addedSubmodel["submodelElements"] as JArray;
        var elementDict = elements?
            .OfType<JObject>()
            .ToDictionary(e => e["idShort"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);

        elementDict.Should().ContainKey("SerialNumber");
        elementDict!["SerialNumber"]["value"]?.ToString().Should().Be(serialNumberTest);
    }

    [Test]
    public async Task CreateAas_OverwriteTrue_ExistingShell_ShouldReturnOkWithPreviousAas()
    {
        // ARRANGE — POST shell returns 409, then GET old + PUT new
        var aasBase64 = "aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvb3ZlcndyaXRlTWU";
        var oldShell = "{\"id\":\"old-shell-id\",\"idShort\":\"old\"}";
        var aasList = new List<JObject>();

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAasConflict()
            .WithGetAas(aasBase64, oldShell, HttpStatusCode.OK)
            .WithPutAas(aasBase64)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/overwriteMe?overwrite=true", null, StatusCodes.Status200OK);

        // ASSERT
        responseContent.Should().Contain("\"previousAas\":");
        responseContent.Should().Contain("old-shell-id");
        aasList.Should().HaveCount(1); // the PUT body
    }

    [Test]
    public async Task CreateAas_OverwriteFalse_ExistingShell_ShouldReturnConflict()
    {
        // ARRANGE — POST shell returns 409 and overwrite=false
        var mockedRestClient = new MockRestClientBuilder()
            .WithGetIdSettings()
            .WithPostAasConflict()
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/conflictMe", null, StatusCodes.Status409Conflict);

        // ASSERT
        responseContent.Should().Contain("overwrite=true");
        responseContent.Should().Contain("orphanedSubmodelIds");
    }

    [Test]
    public async Task CreateAas_WithInvalidBlueprint_ShouldReturnBadRequest_AndNotCreateShell()
    {
        // ARRANGE
        var blueprintIdBase64 = "aW52YWxpZEJsdWVwcmludElk"; // invalidBlueprintId
        var aasList = new List<JObject>();

        var json = $@"
            {{
              ""language"": ""de"",
              ""data"": {{}},
              ""blueprintsIds"": [
                ""invalidBlueprintId""
              ]
            }}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var mockedRestClient = new MockRestClientBuilder(aas: aasList)
            .WithGetIdSettings()
            .WithPostAas()
            .WithGetSubmodel(blueprintIdBase64, "", HttpStatusCode.NotFound, false)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT — build fails in memory, so the shell is never POSTed
        await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasFail", content, StatusCodes.Status400BadRequest);

        aasList.Should().BeEmpty();
    }
}
