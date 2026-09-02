using System.Net;
using System.Text.Json;
using Core.Tests.TestFiles;
using FluentAssertions;
using MnestixCore.IdGenerator;
using Moq;
using RestSharp;

namespace Web.Tests.IntegrationTests;

public class IdGeneratorEndpointTests : IntegrationTestsBase
{
    private const string IdGenerationSettingsResource =
        "/submodels/aHR0cHM6Ly9yZXBvZG9tYWludXJsLmNvbS9zbS9CNDYxQzZFRDMyMjE0OTMzQjhCNkNFNTY5QzhGMEEwMy8xLzA";

    [Test]
    public async Task GenerateSubmodelIds_CountAboveLimit_ReturnsBadRequest()
    {
        // ARRANGE - the limit is enforced by [Range] before the action runs, so no repo call is needed.
        var limit = (int)AasIdGeneratorService.MaxSubmodelIdCount;

        // ACT
        var response = await Client!.GetAsync($"/api/v2/IdGenerator/submodelIds/{limit + 1}");

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GenerateSubmodelIds_ValidCount_ReturnsRequestedNumberOfIds()
    {
        // ARRANGE
        var settings = TestFileProvider.GetIdGeneratorSettingsSubmodelWithValues();
        var mockedRestClient = new Mock<IRestClient>();
        mockedRestClient.Setup(x => x.ExecuteAsync(It.Is<RestRequest>(r =>
                r.Resource == IdGenerationSettingsResource && r.Method == Method.Get), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                Content = settings,
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            });

        HttpClientMock.Setup(x => x.GetConfiguredClientAsync(It.IsAny<string>()))
            .ReturnsAsync(() => mockedRestClient.Object as IRestClient);

        // ACT
        var response = await Client!.GetAsync("/api/v2/IdGenerator/submodelIds/5");
        var content = await response.Content.ReadAsStringAsync();

        // ASSERT
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = JsonSerializer.Deserialize<List<string>>(content);
        ids.Should().HaveCount(5);
    }
}