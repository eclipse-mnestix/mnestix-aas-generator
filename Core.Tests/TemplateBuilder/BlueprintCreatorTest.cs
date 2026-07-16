using System.Net;
using System.Text;
using Core.Tests.TestFiles;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Core.Tests.TemplateBuilder;

public class BlueprintCreatorTest
{
    [Test]
    public async Task CreateNewBlueprintInAas_WhenBlueprintsApiConfigured_PersistsViaBlueprintsEndpoint()
    {
        // ARRANGE
        const string blueprintsAasId = "http://test.sm.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = blueprintsAasId,
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var template = TestFileProvider.GetTemplateSubmodelNameplate();
        var repoProxyClientMock = new Mock<IRepoProxyClient>();

        var blueprintCallTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            url.Should().Be(blueprintsEndpoint);

            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new BlueprintStubHttpMessageHandler(async request =>
                {
                    var content = request.Content is null
                        ? null
                        : await request.Content.ReadAsStringAsync();

                    blueprintCallTcs.TrySetResult(content);

                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                })
            };

            return new RestClient(options);
        });

        var blueprintsCreator = new BlueprintCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<BlueprintCreator>>().Object,
            TimeProvider.System,
            restClientFactory);

        var toEncodeAsBytes = Encoding.ASCII.GetBytes(blueprintsAasId);

        // ACT
        var submodelIdentifier =
            await blueprintsCreator.CreateNewSubmodelInBlueprintAasAsync(template);

        // ASSERT
        var blueprintPayload = await blueprintCallTcs.Task;
        blueprintPayload.Should().NotBeNull();
        blueprintPayload.Should().Contain(submodelIdentifier);

        repoProxyClientMock.Verify(
            s => s.PostAsync(repoProxySubmodelPath, It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task CreateBlueprintInAas_CallsTemplateProviderToGetTemplate_PutsModifiedBlueprint()
    {
        // ARRANGE
        const string blueprintsAasId = "http://test.sm.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = blueprintsAasId
        };

        var template = TestFileProvider.GetTemplateSubmodelNameplate();
        var repoProxyClientMock = new Mock<IRepoProxyClient>();

        var blueprintCreator = new BlueprintCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<BlueprintCreator>>().Object,
            TimeProvider.System);

        var toEncodeAsBytes = Encoding.ASCII.GetBytes(blueprintsAasId);
        var aasBase64 = WebEncoders.Base64UrlEncode(toEncodeAsBytes);

        // ACT
        var submodelIdentifier =
            await blueprintCreator.CreateNewSubmodelInBlueprintAasAsync(template);

        // ASSERT
        repoProxyClientMock.Verify(
            s => s.PostAsync($"{repoProxyAasPath}/{aasBase64}/submodel-refs", It.IsAny<string>()),
            Times.Once);

        repoProxyClientMock.Verify(
            s => s.PostAsync(repoProxySubmodelPath, It.IsAny<string>()),
            Times.Once);

        submodelIdentifier.Should().NotBeEmpty();
    }

    [Test]
    public async Task UpdateBlueprintInAas_WhenBlueprintsApiConfigured_PersistsViaBlueprintsEndpoint()
    {
        // ARRANGE
        const string blueprintsAasId = "http://test.sm.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = blueprintsAasId,
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var repoProxyClientMock = new Mock<IRepoProxyClient>();

        var blueprintCallTcs = new TaskCompletionSource<(string Method, Uri? Uri, string? Body)>(TaskCreationOptions.RunContinuationsAsynchronously);

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            url.Should().Be(blueprintsEndpoint);

            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new BlueprintStubHttpMessageHandler(async request =>
                {
                    var content = request.Content is null
                        ? null
                        : await request.Content.ReadAsStringAsync();

                    blueprintCallTcs.TrySetResult((request.Method.Method, request.RequestUri, content));

                    return new HttpResponseMessage(HttpStatusCode.NoContent)
                    {
                        Content = new StringContent(string.Empty)
                    };
                })
            };

            return new RestClient(options);
        });

        var blueprintCreator = new BlueprintCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<BlueprintCreator>>().Object,
            TimeProvider.System,
            restClientFactory);

        var updatedSubmodel = TestFileProvider.GetBlueprintSubmodelNameplate();
        var updatedSubmodelJson = JObject.Parse(updatedSubmodel);
        var submodelId = updatedSubmodelJson["id"]?.ToString();
        submodelId.Should().NotBeNull();

        var submodelRouteId = Uri.EscapeDataString(submodelId!);

        // ACT
        await blueprintCreator.UpdateSubmodelInBlueprintAasAsync(updatedSubmodel, submodelRouteId);

        // ASSERT
        var blueprintCall = await blueprintCallTcs.Task;
        blueprintCall.Method.Should().Be("PUT");
        blueprintCall.Uri.Should().NotBeNull();
        var expectedSuffix = "/" + WebEncoders.Base64UrlEncode(Encoding.ASCII.GetBytes(submodelRouteId));
        blueprintCall.Uri!.AbsoluteUri.Should().EndWith(expectedSuffix);
        blueprintCall.Body.Should().Be(updatedSubmodel);

        repoProxyClientMock.Verify(
            s => s.PutAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task DeleteBlueprintInAas_WhenBlueprintsApiConfigured_RemovesFromBlueprintsEndpointAndReferences()
    {
        // ARRANGE
        const string blueprintsAasId = "http://test.sm.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = blueprintsAasId,
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var repoProxyClientMock = new Mock<IRepoProxyClient>();
        repoProxyClientMock
            .Setup(s => s.DeleteAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var blueprintCallTcs = new TaskCompletionSource<(string Method, Uri? Uri)>(TaskCreationOptions.RunContinuationsAsynchronously);

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            url.Should().Be(blueprintsEndpoint);

            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new BlueprintStubHttpMessageHandler(request =>
                {
                    blueprintCallTcs.TrySetResult((request.Method.Method, request.RequestUri));
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)
                    {
                        Content = new StringContent(string.Empty)
                    });
                })
            };

            return new RestClient(options);
        });

        var blueprintCreator = new BlueprintCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<BlueprintCreator>>().Object,
            TimeProvider.System,
            restClientFactory);

        const string originalSubmodelId = "https://blueprints.example.com/submodels/123";
        var submodelRouteId = Uri.EscapeDataString(originalSubmodelId);
        var submodelIdBase64 = WebEncoders.Base64UrlEncode(Encoding.ASCII.GetBytes(submodelRouteId));
        var blueprintsAasIdBase64 = WebEncoders.Base64UrlEncode(Encoding.ASCII.GetBytes(blueprintsAasId));

        var expectedSubmodelPath = $"{repoProxySubmodelPath}/{submodelIdBase64}";

        // ACT
        await blueprintCreator.DeleteSubmodelInBlueprintAasAsync(submodelIdBase64);

        // ASSERT
        var blueprintCall = await blueprintCallTcs.Task;
        blueprintCall.Method.Should().Be("DELETE");
        blueprintCall.Uri.Should().NotBeNull();
        blueprintCall.Uri!.AbsoluteUri.Should().EndWith("/" + submodelIdBase64);

        repoProxyClientMock.Verify(
            s => s.DeleteAsync(expectedSubmodelPath),
            Times.Never);
    }

    [Test]
    public async Task CreateNewBlueprintInAas_SetsDisplayNameQualifierFromTimeProvider()
    {
        // ARRANGE
        const string blueprintsAasId = "http://test.sm.id";
        const string repoProxyAasPath = "/testpath/for/AasPath";
        const string repoProxySubmodelPath = "/testpath/for/SubmodelPath";
        const string blueprintsEndpoint = "https://blueprints.example.com/api/submodels";

        // Fixed point in time so the emitted displayName qualifier is deterministic.
        // FakeTimeProvider's local time zone defaults to UTC, so GetLocalNow() equals this value.
        var fixedTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fixedTime);

        var repoProxyOptions = new RepoProxyOptions
        {
            AasPath = repoProxyAasPath,
            SubmodelPath = repoProxySubmodelPath
        };

        var configurationOptions = new ConfigurationOptions
        {
            BlueprintsAasId = blueprintsAasId,
            SubmodelBlueprintsApiUrl = blueprintsEndpoint
        };

        var template = TestFileProvider.GetTemplateSubmodelNameplate();
        var repoProxyClientMock = new Mock<IRepoProxyClient>();

        var blueprintCallTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var restClientFactory = new Func<string, RestClient>(url =>
        {
            var options = new RestClientOptions(url)
            {
                ConfigureMessageHandler = _ => new BlueprintStubHttpMessageHandler(async request =>
                {
                    var content = request.Content is null
                        ? null
                        : await request.Content.ReadAsStringAsync();

                    blueprintCallTcs.TrySetResult(content);

                    return new HttpResponseMessage(HttpStatusCode.Created)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                })
            };

            return new RestClient(options);
        });

        var blueprintCreator = new BlueprintCreator(
            repoProxyClientMock.Object,
            new OptionsWrapper<ConfigurationOptions>(configurationOptions),
            new OptionsWrapper<RepoProxyOptions>(repoProxyOptions),
            new Mock<ILogger<BlueprintCreator>>().Object,
            timeProvider,
            restClientFactory);

        // ACT
        await blueprintCreator.CreateNewSubmodelInBlueprintAasAsync(template);

        // ASSERT
        var blueprintPayload = await blueprintCallTcs.Task;
        blueprintPayload.Should().NotBeNull();

        var persistedBlueprint = JObject.Parse(blueprintPayload!);
        var displayNameQualifier = persistedBlueprint["qualifiers"]!
            .Single(q => (string?)q["type"] == "displayName");

        displayNameQualifier["value"].Should().NotBeNull();
        ((string?)displayNameQualifier["value"]).Should().Be("Nameplate_2026-01-02T03:04:05");
    }

    private sealed class BlueprintStubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responseFactory = responseFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _responseFactory(request);
        }
    }
}