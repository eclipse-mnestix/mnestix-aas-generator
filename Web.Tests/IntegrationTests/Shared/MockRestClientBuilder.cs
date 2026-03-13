using Core.Tests.TestFiles;
using Moq;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;

namespace Web.Tests.IntegrationTests.Shared
{
    /// <summary>
    /// A builder utility for configuring a mocked <see cref="IRestClient"/> using Moq,
    /// tailored for integration testing scenarios involving HTTP communication with 
    /// Asset Administration Shells (AAS) and Submodels.
    ///
    /// Allows optional tracking or mutation of AAS and Submodel payloads passed during mocked HTTP calls.
    ///
    /// Constructor Parameters:
    /// <param name="aas">
    ///     Optional list to store or inspect <see cref="JObject"/> representations of AAS-related JSON data 
    ///     intercepted in mocked requests (e.g., for POST to /shells).
    /// </param>
    /// <param name="submodels">
    ///     Optional list to store or inspect <see cref="JObject"/> representations of Submodel JSON data 
    ///     intercepted in mocked requests (e.g., for POST to /submodels).
    /// </param>
    ///
    /// Example usage:
    /// <code>
    /// var submodelList = new List&lt;JObject&gt;();
    /// var builder = new MockRestClientBuilder(submodels: submodelList)
    ///                   .WithGetSubmodel("some-id", someJson)
    ///                   .WithPostSubmodel();
    /// var mockClient = builder.Build();
    /// </code>
    /// </summary>
    public class MockRestClientBuilder(List<JObject>? aas = null, List<JObject>? submodels = null)
    {
        private readonly Mock<IRestClient> _mock = new();
        private readonly List<JObject>? _aas = aas;
        private readonly List<JObject>? _submodels = submodels;

        public MockRestClientBuilder WithGetAas(string id, string content, HttpStatusCode statusCode = HttpStatusCode.OK, bool isSucessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/shells/{id}" && r.Method == Method.Get),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = statusCode,
                    Content = content,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = isSucessful
                });

            return this;
        }

        public MockRestClientBuilder WithGetSubmodel(string id, string content, HttpStatusCode statusCode = HttpStatusCode.OK, bool isSucessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/submodels/{id}" && r.Method == Method.Get),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = statusCode,
                    Content = content,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = isSucessful
                });

            return this;
        }

        public MockRestClientBuilder WithGetIdSettings()
        {
            var idGenerationSettings = TestFileProvider.GetIdGeneratorSettingsSubmodelWithValues();
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == "/submodels/aHR0cHM6Ly9yZXBvZG9tYWludXJsLmNvbS9zbS9CNDYxQzZFRDMyMjE0OTMzQjhCNkNFNTY5QzhGMEEwMy8xLzA" && 
                                            r.Method == Method.Get),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = idGenerationSettings,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true
                });

            return this;
        }

        public MockRestClientBuilder WithPostAas()
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == "/shells" && r.Method == Method.Post),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((RestRequest request, CancellationToken _) =>
                {
                    if (_aas != null)
                    {
                        var bodyParam = request.Parameters.FirstOrDefault(p => p.Type == ParameterType.RequestBody);
                        if (bodyParam?.Value is string jsonBody)
                        {
                            var jObject = JObject.Parse(jsonBody);
                            _aas.Add(jObject);
                        }
                    }

                    return new RestResponse
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = "{\"message\":\"Created\"}",
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = true
                    };
                });

            return this;
        }

        public MockRestClientBuilder WithDeleteAas(string id, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool isSuccessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/shells/{id}" && r.Method == Method.Delete),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = statusCode,
                    Content = "",
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = isSuccessful
                });

            return this;
        }

        public MockRestClientBuilder WithPostSubmodel()
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == "/submodels" && r.Method == Method.Post),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((RestRequest request, CancellationToken _) =>
                {
                    if (_submodels != null)
                    {
                        var bodyParam = request.Parameters.FirstOrDefault(p => p.Type == ParameterType.RequestBody);
                        if (bodyParam?.Value is string jsonBody)
                        {
                            var jObject = JObject.Parse(jsonBody);
                            _submodels.Add(jObject);
                        }
                    }

                    return new RestResponse
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = "{\"message\":\"Created\"}",
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = true
                    };
                });

            return this;
        }

        public MockRestClientBuilder WithPutSubmodel(string id, HttpStatusCode statusCode = HttpStatusCode.OK, bool isSucessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/submodels/{id}" && r.Method == Method.Put),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((RestRequest request, CancellationToken _) =>
                {
                    if (_submodels != null) { 
                        var bodyParam = request.Parameters.FirstOrDefault(p => p.Type == ParameterType.RequestBody);
                        if (bodyParam?.Value is string jsonBody)
                        {
                            var jObject = JObject.Parse(jsonBody);
                            var indexOfElement = _submodels.FindIndex(el => el["id"]?.ToString() == jObject["id"]?.ToString());
                            if (indexOfElement != -1)
                            {
                                _submodels[indexOfElement] = jObject;
                            };
                        };
                    };

                    return new RestResponse
                    {
                        StatusCode = statusCode,
                        Content = "{\"message\":\"Created\"}",
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = isSucessful
                    };
                });

            return this;
        }

        public MockRestClientBuilder WithDeleteSubmodel(string id, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool isSucessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/submodels/{id}" && r.Method == Method.Delete),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((RestRequest request, CancellationToken _) => {
                    return new RestResponse
                    {
                        StatusCode = statusCode,
                        Content = "",
                        ResponseStatus = ResponseStatus.Completed,
                        IsSuccessStatusCode = isSucessful
                    };
                });
            return this;
        }

        public MockRestClientBuilder WithGetSubmodelRefs(string resource, string content)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/shells/{resource}/submodel-refs" && r.Method == Method.Get),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = content,
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true
                });

            return this;
        }

        public MockRestClientBuilder WithPostSubmodelRefs(string resource)
        {
            _mock.Setup(x => x.ExecuteAsync(
                    It.Is<RestRequest>(r => r.Resource == $"/shells/{resource}/submodel-refs" && r.Method == Method.Post),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = "{\"message\":\"Created\"}",
                    ResponseStatus = ResponseStatus.Completed,
                    IsSuccessStatusCode = true
                });

            return this;
        }

        public MockRestClientBuilder WithDeleteSubmodelRefs(string aasId, string submodelId, HttpStatusCode statusCode = HttpStatusCode.NoContent, bool isSucessful = true)
        {
            _mock.Setup(x => x.ExecuteAsync(
                It.Is<RestRequest>(r => r.Resource == $"/shells/{aasId}/submodel-refs/{submodelId}" && r.Method == Method.Delete),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestResponse 
            { 
                StatusCode = statusCode,
                Content = "",
                ResponseStatus = ResponseStatus.Completed,
                IsSuccessStatusCode = isSucessful
            });

            return this;
        }

        public Mock<IRestClient> Mock() => _mock;

        public IRestClient Build() => _mock.Object;
    }
}
