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
    public async Task GetAsync_WhenPathContainsTraversalSequence_ThrowsAndNeverCallsClient()
    {
        // Act
        var act = () => _sut.GetAsync("..\\shells");

        // Assert
        var ex = await act.Should().ThrowAsync<RepoProxyException>();
        ex.Which.ErrorCode.Should().Be(ErrorCodes.CouldNotGet);
        _restClientMock.Verify(
            c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetAsync_WhenPathContainsParentSegment_ThrowsAndNeverCallsClient()
    {
        // Act
        var act = () => _sut.GetAsync("submodels/../shells");

        // Assert
        await act.Should().ThrowAsync<RepoProxyException>();
        _restClientMock.Verify(
            c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["description"].Should().BeNull();
        result["id"]!.Value<string>().Should().Be("x");
        result["value"]!.Value<string>().Should().Be("hello");
    }

    [Test]
    public void NormalizeJson_ShouldStripDataSpecificationAndHasDataSpecification()
    {
        var json = JObject.Parse("{\"id\": \"x\", \"dataSpecification\": [], \"hasDataSpecification\": []}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["dataSpecification"].Should().BeNull();
        result["hasDataSpecification"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldStripParentBackReferences()
    {
        var json = JObject.Parse("{\"idShort\": \"x\", \"parent\": {\"keys\": []}}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["parent"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldStripV2KeyFields()
    {
        var json = JObject.Parse("{\"semanticId\": {\"keys\": [{\"type\": \"GlobalReference\", \"value\": \"x\", \"local\": false, \"idType\": \"IRI\", \"index\": 0}]}}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
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
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["kind"].Should().BeNull();
    }

    [Test]
    public void NormalizeJson_ShouldKeepKindOnSubmodel()
    {
        var json = JObject.Parse("{\"modelType\": \"Submodel\", \"kind\": \"Template\"}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["kind"]!.Value<string>().Should().Be("Template");
    }

    [Test]
    public void NormalizeJson_ShouldNormalizeValueTypeCase()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:Date\", \"value\": \"2024-01-01\"}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["valueType"]!.Value<string>().Should().Be("xs:date");
    }

    [Test]
    public void NormalizeJson_ShouldCoercePropertyValueToString()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:integer\", \"value\": 42}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["value"]!.Type.Should().Be(Newtonsoft.Json.Linq.JTokenType.String);
        result["value"]!.Value<string>().Should().Be("42");
    }

    [Test]
    public void NormalizeJson_ShouldCoerceBooleanPropertyValueToString()
    {
        var json = JObject.Parse("{\"modelType\": \"Property\", \"valueType\": \"xs:boolean\", \"value\": true}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["value"]!.Type.Should().Be(Newtonsoft.Json.Linq.JTokenType.String);
        result["value"]!.Value<string>().Should().Be("true");
    }

    [Test]
    public void NormalizeJson_ShouldInjectValueTypeOnQualifiersMissingIt()
    {
        var json = JObject.Parse("{\"qualifiers\": [{\"type\": \"MnestixAASGenerator/MappingInfo\", \"value\": \"$.name\"}]}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["qualifiers"]![0]!["valueType"]!.Value<string>().Should().Be("xs:string");
    }

    [Test]
    public void NormalizeJson_ShouldStripOrderedAndAllowDuplicates()
    {
        var json = JObject.Parse("{\"modelType\": \"SubmodelElementCollection\", \"ordered\": false, \"allowDuplicates\": false}");
        var result = AasJsonNormalizer.NormalizeJsonForRepository(json);
        result["ordered"].Should().BeNull();
        result["allowDuplicates"].Should().BeNull();
    }

    #endregion

    #region Date Format Preservation Tests

    // BaSyx 1.1.0 validates xs:date/xs:dateTime values strictly. Older versions silently
    // accepted values that Newtonsoft's default JObject.Parse had already reformatted
    // (dates get upgraded to DateTime/JTokenType.Date and reformatted on ToString()).
    // The proxy must forward the value byte-for-byte, valid or not, and let BaSyx be the
    // one to accept or reject it.
    [TestCase("2024-01-01")]
    [TestCase("2024-01-01T10:15:30.100Z")]
    [TestCase("01/01/2024")]
    [TestCase("2024-13-45T99:99:99")]
    [TestCase("not-a-date")]
    public async Task PostAsync_WhenValueIsDateString_ForwardsItUnchanged(string dateValue)
    {
        // Arrange
        RestRequest? capturedRequest = null;
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RestRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            });
        var json = $"{{\"modelType\": \"Property\", \"valueType\": \"xs:date\", \"value\": \"{dateValue}\"}}";

        // Act
        await _sut.PostAsync("submodels", json);

        // Assert
        var sentBody = capturedRequest!.Parameters.First(p => p.Type == ParameterType.RequestBody).Value as string;
        sentBody.Should().Contain($"\"{dateValue}\"");
    }

    [Test]
    public async Task PutAsync_WhenValueIsNonConformantDateString_ForwardsItUnchanged()
    {
        // Arrange
        const string dateValue = "01/01/2024";
        RestRequest? capturedRequest = null;
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RestRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            });
        var json = $"{{\"modelType\": \"Property\", \"valueType\": \"xs:date\", \"value\": \"{dateValue}\"}}";

        // Act
        await _sut.PutAsync("submodels/some-id", json);

        // Assert
        var sentBody = capturedRequest!.Parameters.First(p => p.Type == ParameterType.RequestBody).Value as string;
        sentBody.Should().Contain($"\"{dateValue}\"");
    }

    [Test]
    public async Task PostSubmodelWithReferenceAsync_WhenValueIsNonConformantDateString_ForwardsItUnchanged()
    {
        // Arrange
        const string dateValue = "01/01/2024";
        var capturedRequests = new List<RestRequest>();
        _restClientMock
            .Setup(c => c.ExecuteAsync(It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RestRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(new RestResponse
            {
                StatusCode = HttpStatusCode.OK,
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = true
            });
        var json = $"{{\"modelType\": \"Property\", \"valueType\": \"xs:date\", \"value\": \"{dateValue}\"}}";

        // Act
        await _sut.PostSubmodelWithReferenceAsync("aasId", "smId", json);

        // Assert
        var submodelRequest = capturedRequests.Single(r => r.Resource == "submodels");
        var sentBody = submodelRequest.Parameters.First(p => p.Type == ParameterType.RequestBody).Value as string;
        sentBody.Should().Contain($"\"{dateValue}\"");
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
