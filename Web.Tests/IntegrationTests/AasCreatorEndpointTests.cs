using Core.Tests.TestFiles;
using FluentAssertions;
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
    public async Task CreateAas_WithoutRequestBody_ShouldReturnOK()
    {
        // ARRANGE
        var idGenerationSettings = TestFileProvider.GetIdGeneratorSettingsSubmodelWithValues();

        var mockedRestClient = new MockRestClientBuilder()
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFz", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .Build();

        Func <IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);
        
        // ACT - send empty body or null
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAas", null);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAas\"");
        responseContent.Should().Contain("\"submodelResults\":[]");
    }

    [Test]
    public async Task CreateAas_WithSubmodels_ShouldReturnOKWithSubmodelResults()
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
            .WithPostSubmodelRefs("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFzV2l0aFN1Ym1vZGVscw")
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT
        var responseContent = await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasWithSubmodels", content);

        // ASSERT
        responseContent.Should().Contain("\"assetId\":\"assetIdPrefixcreateAasWithSubmodels\"");
        responseContent.Should().Contain("\"submodelResults\":");
        aasList.Should().HaveCount(1);
        submodels.Should().HaveCount(1);
        
        var addedSubmodel = submodels[0];
        var elements = addedSubmodel["submodelElements"] as JArray;
        var elementDict = elements?
            .OfType<JObject>()
            .ToDictionary(e => e["idShort"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase);
        
        elementDict.Should().ContainKey("SerialNumber");
        elementDict!["SerialNumber"]["value"]?.ToString().Should().Be(serialNumberTest);
    }

    [Test]
    public async Task CreateAas_WithInvalidBlueprint_ShouldReturnBadRequest()
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
            .WithGetAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFzRmFpbA", "", HttpStatusCode.NotFound, false)
            .WithGetIdSettings()
            .WithPostAas()
            .WithDeleteAas("aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvY3JlYXRlQWFzRmFpbA")
            .WithGetSubmodel(blueprintIdBase64, "", HttpStatusCode.NotFound, false)
            .Build();

        Func<IRestClient> restClientFactory = () => mockedRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);

        // ACT & ASSERT
        // This should return 400 BadRequest because the blueprint cannot be fetched
        var act = async () => await PostContentAndEnsureSuccessStatusCodeAsync("/api/AasCreator/createAasFail", content);
        await act.Should().ThrowAsync<Exception>(); // Will throw because status code is not success
        
        // AAS should have been created but then deleted due to failure
        aasList.Should().HaveCount(1); // AAS was created before submodel failure
    }
}