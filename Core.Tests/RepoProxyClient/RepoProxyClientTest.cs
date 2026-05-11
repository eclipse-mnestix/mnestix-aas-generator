using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.RestClientProvider.Interfaces;
using MnestixCore.Shared;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Core.Tests.RepoProxyClient;

public class RepoProxyClientTest
{
    private Mock<IHttpClientProvider> _httpClientProviderMock = null!;
    private Mock<IRestClient> _restClientMock = null!;
    private MnestixCore.RepoProxyClient.RepoProxyClient _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _httpClientProviderMock = new Mock<IHttpClientProvider>();
        _restClientMock = new Mock<IRestClient>();

        _httpClientProviderMock
            .Setup(p => p.GetConfiguredClientAsync(It.IsAny<string>()))
            .ReturnsAsync(_restClientMock.Object);

        var repoProxyOptions = Options.Create(new RepoProxyOptions
        {
            AasPath = "shells",
            SubmodelPath = "submodels"
        });
        var securityOptions = Options.Create(new CustomerEndpointsSecurityOptions
        {
            ApiKey = "test-api-key"
        });
        var loggerMock = new Mock<ILogger<BaseUrlProvider>>();
        var baseUrlProvider = new BaseUrlProvider(loggerMock.Object);
        baseUrlProvider.SetBaseUrl("https://localhost/");

        _sut = new MnestixCore.RepoProxyClient.RepoProxyClient(
            repoProxyOptions, securityOptions, baseUrlProvider, _httpClientProviderMock.Object);
    }

    [Test]
    public async Task GetAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));

        // Act
        var act = () => _sut.GetAsync("some/path");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotGet);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PostAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));

        // Act
        var act = () => _sut.PostAsync("some/path", "{\"key\":\"value\"}");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPostShell);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PutAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));

        // Act
        var act = () => _sut.PutAsync("some/path", "{\"key\":\"value\"}");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPostShell);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PostSubmodelWithReferenceAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable));

        // Act
        var act = () => _sut.PostSubmodelWithReferenceAsync("aasId", "smId", "{\"key\":\"value\"}");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPutSubmodel);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PutFileContent_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden));

        // Act
        var act = () => _sut.PutFileContent("some/path", "file.txt", new byte[] { 1, 2, 3 });

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPostShell);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PatchAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Internal Server Error", null, HttpStatusCode.InternalServerError));

        // Act
        var act = () => _sut.PatchAsync("some/path", "some-value");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPatchSubmodel);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task DeleteAsync_WhenHttpClientThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));

        // Act
        var act = () => _sut.DeleteAsync("some/path");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotDelete);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    #region NormalizeJsonForRepository Tests

    [Test]
    public void NormalizeJson_ShouldStripNullProperties()
    {
        var json = JObject.Parse("{\"id\": \"x\", \"description\": null, \"value\": \"hello\"}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["description"].Should().BeNull();
        result["id"]!.Value<string>().Should().Be("x");
        result["value"]!.Value<string>().Should().Be("hello");
    }

    [Test]
    public void NormalizeJson_ShouldStripDataSpecificationAndHasDataSpecification()
    {
        var json = JObject.Parse("{\"id\": \"x\", \"dataSpecification\": [], \"hasDataSpecification\": []}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["dataSpecification"].Should().BeNull();
        result["hasDataSpecification"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldStripParentBackReferences()
    {
        var json = JObject.Parse("{\"idShort\": \"x\", \"parent\": {\"keys\": []}}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["parent"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldStripV2KeyFields()
    {
        var json = JObject.Parse("{\"semanticId\": {\"keys\": [{\"type\": \"GlobalReference\", \"value\": \"x\", \"local\": false, \"idType\": \"IRI\", \"index\": 0}]}}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        var key = result["semanticId"]!["keys"]![0]!;
        key["local"].Should().BeNull();
        key["idType"].Should().BeNull();
        key["index"].Should().BeNull();
        key["type"]!.Value<string>().Should().Be("GlobalReference");
    }

    [Test]
    public void NormalizeJson_ShouldStripKindFromNonSubmodelElements()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"kind\": \"Instance\", \"value\": \"1\"}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["kind"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldKeepKindOnSubmodel()
    {
        var json = JObject.Parse("{\"modelType\": \"Submodel\", \"kind\": \"Template\"}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["kind"]!.Value<string>().Should().Be("Template");
    }

    [Test]
    public void NormalizeJson_ShouldNormalizeValueTypeCase()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:Date\", \"value\": \"2024-01-01\"}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["valueType"]!.Value<string>().Should().Be("xs:date");
    }

    [Test]
    public void NormalizeJson_ShouldCoercePropertyValueToString()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:integer\", \"value\": 42}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["value"]!.Type.Should().Be(Newtonsoft.Json.Linq.JTokenType.String);
        result["value"]!.Value<string>().Should().Be("42");
    }

    [Test]
    public void NormalizeJson_ShouldCoerceBooleanPropertyValueToString()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:boolean\", \"value\": true}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["value"]!.Type.Should().Be(Newtonsoft.Json.Linq.JTokenType.String);
        result["value"]!.Value<string>().Should().Be("True");
    }

    [Test]
    public void NormalizeJson_ShouldInjectValueTypeOnQualifiersMissingIt()
    {
        var json = JObject.Parse("{\"qualifiers\": [{\"type\": \"SMT/MappingInfo\", \"value\": \"$.name\"}]}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    [Test]
    public void NormalizeJson_ShouldStripOrderedAndAllowDuplicates()
    {
        var json = JObject.Parse("{\"modelType\": \"SubmodelElementCollection\", \"ordered\": false, \"allowDuplicates\": false}");
        var result = MnestixCore.RepoProxyClient.RepoProxyClient.NormalizeJsonForRepository(json);
        result["ordered"].Should().BeNull();
        result["allowDuplicates"].Should().BeNull();
    }

    #endregion

    [Test]
    public async Task GetAsync_WhenHttpClientProviderThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _httpClientProviderMock
            .Setup(p => p.GetConfiguredClientAsync(It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));

        // Act
        var act = () => _sut.GetAsync("some/path");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotGet);
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Test]
    public async Task PostAsync_WhenHttpClientProviderThrows_ShouldThrowRepoProxyException()
    {
        // Arrange
        _httpClientProviderMock
            .Setup(p => p.GetConfiguredClientAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Auth failed"));

        // Act
        var act = () => _sut.PostAsync("some/path", "{\"key\":\"value\"}");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotPostShell);
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }
}
