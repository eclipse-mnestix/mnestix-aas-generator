using System.Net;
using System.Text;
using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared.Interfaces;
using MnestixCore.TemplateBuilder;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Core.Tests.TemplateBuilder;

public class BlueprintProviderTest
{
    private const string BlueprintPath = "shells/dGVzdA/submodel-refs";
    private const string AasPath = "shells";
    private const string SubmodelPath = "/blueprintPath/";
    private const string SubmodelId = "Nameplate_Template_cb984326-34f1-4964-85ca-c4ccb77e588c";

    private readonly string _blueprint = TestFileProvider.GetBlueprintSubmodelNameplate();
    private readonly string _reference = TestFileProvider.GetBlueprintSubmodelNameplateReference();

    [Test]
    public async Task GetAllBlueprintsAsync_RepoReturnsSubmodels_SubmodelHandlerIsCalled()
    {
        // ARRANGE

        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock.Setup(s => s.GetAsync(BlueprintPath)).ReturnsAsync((true, _reference));

        var submodelHandlerMock =  new Mock<ISubmodelHandler>();
        submodelHandlerMock.Setup(sh => sh.GetSubmodelsIdsFromSubmodelsRefs(It.IsAny<JObject>()))
            .Returns([SubmodelId]);

        var configurationOptionsMock = new Mock<IOptions<ConfigurationOptions>>();
        configurationOptionsMock.Setup(s => s.Value).Returns(new ConfigurationOptions { BlueprintsAasId = "test" });

        var blueprintProvider = new BlueprintProvider(
            repoProxyClientMock.Object,
            configurationOptionsMock.Object,
            new OptionsWrapper<RepoProxyOptions>(new RepoProxyOptions
            {
                AasPath = AasPath,
                SubmodelPath = SubmodelPath
            }),
            submodelHandlerMock.Object,
            Mock.Of<ILogger<BlueprintProvider>>());

        // ACT 
        await blueprintProvider.GetAllBlueprintsAsync();

        // ASSERT
        repoProxyClientMock.Verify(s => s.GetAsync(BlueprintPath), Times.Once);
        submodelHandlerMock.Verify(sh => sh.GetSubmodelsIdsFromSubmodelsRefs(It.IsAny<JObject>()), Times.Once);
    }

    [Test]
    public async Task GetBlueprintAsync_RepoReturnsSubmodel_SubmodelHandlerIsCalled()
    {
        // ARRANGE
        var submodelIdentifier = Guid.NewGuid().ToString();

        var pathToCall = SubmodelPath + "/" + submodelIdentifier;
    
        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock.Setup(s => s.GetAsync(pathToCall)).ReturnsAsync((true, _blueprint));
    
        var submodelHandlerMock =  new Mock<ISubmodelHandler>();

        var configurationOptionsMock = new Mock<IOptions<ConfigurationOptions>>();
        configurationOptionsMock.Setup(s => s.Value).Returns(new ConfigurationOptions());

        var blueprintProvider = new BlueprintProvider(
            repoProxyClientMock.Object,
            configurationOptionsMock.Object,
            new OptionsWrapper<RepoProxyOptions>(new RepoProxyOptions { SubmodelPath = SubmodelPath, AasPath = AasPath }),
            submodelHandlerMock.Object,
            Mock.Of<ILogger<BlueprintProvider>>());
    
        // ACT 
        await blueprintProvider.GetBlueprintAsync(submodelIdentifier);
    
        // ASSERT
        repoProxyClientMock.Verify(s => s.GetAsync(pathToCall), Times.Once);
    }

    [Test]
    public async Task GetAllBlueprintsAsync_WhenBlueprintsApiConfigured_ReturnsBlueprintsFromEndpoint()
    {
        // ARRANGE
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";
        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = "test",
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        var submodelHandlerMock = new Mock<ISubmodelHandler>();

        var responseContent = """{"result": [{"id": "template-1"}]}""";

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            url.Should().Be(blueprintsEndpoint);

            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new StubHttpMessageHandler(request =>
                {
                    request.Method.Should().Be(HttpMethod.Get);
                    request.RequestUri.Should().Be(new Uri(blueprintsEndpoint));

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
                    });
                })
            };

            return new RestClient(options);
        });

        var provider = new BlueprintProvider(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(new RepoProxyOptions { AasPath = AasPath, SubmodelPath = SubmodelPath }),
            submodelHandlerMock.Object,
            Mock.Of<ILogger<BlueprintProvider>>(),
            restClientFactory);

        // ACT
        var result = await provider.GetAllBlueprintsAsync();

        // ASSERT
    result.Should().HaveCount(1);
    result[0]?.Value<string>("id").Should().Be("template-1");
        repoProxyClientMock.Verify(s => s.GetAsync(It.IsAny<string>()), Times.Never);
        submodelHandlerMock.Verify(sh => sh.GetSubmodelsIdsFromSubmodelsRefs(It.IsAny<JObject>()), Times.Never);
    }

    [Test]
    public async Task GetBlueprintsAsync_WhenBlueprintsApiConfigured_ReturnsBlueprintFromEndpoint()
    {
        // ARRANGE
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";
        const string templateId = "TmFtZXBsYXRlX1RlbXBsYXRlX2lk";

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = "test",
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        var submodelHandlerMock = new Mock<ISubmodelHandler>();

        const string templatePayload = """{"id": "template-1", "idShort": "Nameplate"}""";

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            url.Should().Be(blueprintsEndpoint);

            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new StubHttpMessageHandler(request =>
                {
                    request.Method.Should().Be(HttpMethod.Get);
                    request.RequestUri.Should().Be(new Uri($"{blueprintsEndpoint}/{templateId}"));

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(templatePayload, Encoding.UTF8, "application/json")
                    });
                })
            };

            return new RestClient(options);
        });

        var provider = new BlueprintProvider(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(new RepoProxyOptions { AasPath = AasPath, SubmodelPath = SubmodelPath }),
            submodelHandlerMock.Object,
            Mock.Of<ILogger<BlueprintProvider>>(),
            restClientFactory);

        // ACT
        var result = await provider.GetBlueprintAsync(templateId);

        // ASSERT
        result.Value<string>("id").Should().Be("template-1");
        repoProxyClientMock.Verify(s => s.GetAsync(It.IsAny<string>()), Times.Never);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory = responseFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseFactory(request);
        }
    }
}