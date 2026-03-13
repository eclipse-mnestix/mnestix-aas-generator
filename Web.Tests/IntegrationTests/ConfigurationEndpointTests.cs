using System.Net;
using Core.Tests.TestFiles;
using FluentAssertions;
using Moq;
using RestSharp;

namespace Web.Tests.IntegrationTests;

public class ConfigurationEndpointTests : IntegrationTestsBase
{
    private const string IdGenerationSettingsPath = "api/configuration";

    [Test]
    public async Task GetIdGenerationSettings_SubmodelWithIdGenerationSettingsReturned()
    {
        // ARRANGE
        var idGenerationSettings = TestFileProvider.GetIdGeneratorSettingsSubmodelWithValues();
        var mockedRestClient = new Mock<IRestClient>();
        
        mockedRestClient.Setup(x => x.ExecuteAsync(It.Is<RestRequest>(r => 
                r.Resource == "/submodels/aHR0cHM6Ly9yZXBvZG9tYWludXJsLmNvbS9zbS9CNDYxQzZFRDMyMjE0OTMzQjhCNkNFNTY5QzhGMEEwMy8xLzA" && r.Method == Method.Get), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestResponse { StatusCode = HttpStatusCode.OK, Content = idGenerationSettings,  ResponseStatus = ResponseStatus.Completed, IsSuccessStatusCode = true });

        Func<IRestClient> restClientFactory = () => mockedRestClient.Object as IRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);
        
        // ACT
        var responseContent = await GetResponseContentAndEnsureStatusCodeAsync(IdGenerationSettingsPath);

        // ASSERT
        responseContent.Should().Contain("\"idShort\":\"IdGenerationSettings\"");
    }
    
    [Test]
    public async Task PatchSingleIdGenerationSetting_SubmodelRepositoryPatchIsCalled()
    {
        // ARRANGE
        var mockedRestClient = new Mock<IRestClient>();
        
        mockedRestClient.Setup(x => x.ExecuteAsync(It.Is<RestRequest>(r => 
                r.Resource == "/submodels/aHR0cHM6Ly9yZXBvZG9tYWludXJsLmNvbS9zbS9CNDYxQzZFRDMyMjE0OTMzQjhCNkNFNTY5QzhGMEEwMy8xLzA/submodel-elements/assetId/$value" && r.Method == Method.Patch), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestResponse { StatusCode = HttpStatusCode.OK, ResponseStatus = ResponseStatus.Completed, IsSuccessStatusCode = true });

        Func<IRestClient> restClientFactory = () => mockedRestClient.Object as IRestClient;
        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>())).ReturnsAsync(restClientFactory);
        
        // ACT
        await PatchContentAndEnsureSuccessStatusCodeAsync($"{IdGenerationSettingsPath}?idShortPath=assetId&value=newAssetId", null);

        // ASSERT
        mockedRestClient.Verify(x => x.ExecuteAsync(It.Is<RestRequest>(r =>
                r.Resource == "/submodels/aHR0cHM6Ly9yZXBvZG9tYWludXJsLmNvbS9zbS9CNDYxQzZFRDMyMjE0OTMzQjhCNkNFNTY5QzhGMEEwMy8xLzA/submodel-elements/assetId/$value" && r.Method == Method.Patch), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}