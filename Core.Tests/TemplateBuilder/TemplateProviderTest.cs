using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Text;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared.Interfaces;
using MnestixCore.TemplateBuilder;

namespace Core.Tests.TemplateBuilder;

public class TemplateProviderTest
{
    [Test]
    public async Task GetTemplateSubmodel_AllTemplatesCouldBeReturned_TemplatesReturned()
    {
        // ARRANGE
        const string templatePath = "shells/dGVzdA/submodel-refs";
        const string AasPath = "shells";
        const string submodelPath = "/path/to/submodel";
        const string submodelId = "Nameplate_Template_cb984326-34f1-4964-85ca-c4ccb77e588c";

        const string submodelId64Encoded = "TmFtZXBsYXRlX1RlbXBsYXRlX2NiOTg0MzI2LTM0ZjEtNDk2NC04NWNhLWM0Y2NiNzdlNTg4Yw";

        const string pathToCall = templatePath;
        const string pathToCallSubmodel = submodelPath + "/" + submodelId64Encoded;

        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        var reference = TestFileProvider.GetBlueprintSubmodelNameplateReference();
        var template = TestFileProvider.GetTemplateSubmodelNameplate();

        repoProxyClientMock.Setup(s => s.GetAsync(pathToCall))
            .ReturnsAsync((true, reference));

        repoProxyClientMock.Setup(s => s.GetAsync(pathToCallSubmodel))
            .ReturnsAsync((true, template));

        var submodelHandlerMock = new Mock<ISubmodelHandler>();
        submodelHandlerMock.Setup(sh => sh.GetSubmodelsIdsFromSubmodelsRefs(It.IsAny<JObject>()))
            .Returns([submodelId]);

        var templateSubmodelProvider =
            new TemplateProvider(
                repoProxyClientMock.Object,
                new OptionsWrapper<RepoProxyOptions>(
                    new RepoProxyOptions { SubmodelPath = submodelPath, AasPath = AasPath }),
                new OptionsWrapper<ConfigurationOptions>(
                    new ConfigurationOptions { TemplatesAasId = "test" }),
                submodelHandlerMock.Object,
                NullLogger<TemplateProvider>.Instance);

        // ACT
        var submodels = await templateSubmodelProvider.GetAllTemplateSubmodelsAsync();

        // ASSERT
        submodels.Should().HaveCount(1);
        repoProxyClientMock.Verify(s => s.GetAsync(pathToCall), Times.Exactly(1));
        repoProxyClientMock.Verify(s => s.GetAsync(pathToCallSubmodel), Times.Exactly(1));
    }

    [Test]
    public async Task GetAlltemplateSubmodelsAsync_WhenTemplatesApiReturnsSuccess_ReturnsResultArray()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result": [{"id": "template-1"}]}""", Encoding.UTF8, "application/json")
        });

        var result = await provider.GetAllTemplateSubmodelsAsync();

        result.Should().HaveCount(1);
        result[0]?.Value<string>("id").Should().Be("template-1");
    }

    [Test]
    public async Task GetAlltemplateSubmodelsAsync_WhenTemplatesApiReturnsError_ThrowsInvalidOperationException()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error": "boom"}""", Encoding.UTF8, "application/json")
        });

        var act = async () => await provider.GetAllTemplateSubmodelsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to fetch submodel templates from the repository. Status code: 500.");
    }

    [Test]
    public async Task GetAlltemplateSubmodelsAsync_WhenTemplatesApiReturnsEmptyContent_ThrowsInvalidOperationException()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("   ", Encoding.UTF8, "application/json")
        });

        var act = async () => await provider.GetAllTemplateSubmodelsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Submodel templates endpoint returned an empty response.");
    }

    [Test]
    public async Task GetAlltemplateSubmodelsAsync_WhenTemplatesApiReturnsUnexpectedPayload_ThrowsInvalidOperationException()
    {
        var provider = CreateProvider(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result": {"id": "template-1"}}""", Encoding.UTF8, "application/json")
        });

        var act = async () => await provider.GetAllTemplateSubmodelsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unexpected response format from submodel templates endpoint.");
    }

    private static TemplateProvider CreateProvider(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var repoProxyClient = Mock.Of<IRepoProxyClient>();
        var submodelHandler = Mock.Of<ISubmodelHandler>();

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new StubHttpMessageHandler(responseFactory)
            };

            return new RestClient(options);
        });

        return new TemplateProvider(
            repoProxyClient,
            new OptionsWrapper<RepoProxyOptions>(new RepoProxyOptions { SubmodelPath = "/submodel", AasPath = "/aas" }),
            new OptionsWrapper<ConfigurationOptions>(new ConfigurationOptions
            {
                TemplatesAasId = "test",
                SubmodelTemplatesApiUrl = "http://localhost"
            }),
            submodelHandler,
            NullLogger<TemplateProvider>.Instance,
            restClientFactory);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory = responseFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}