using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider;

/// <summary>
/// Provides a configured RestClient instance without including an access token.
/// Implements the IHttpClientProvider interface.
/// </summary>
public class HttpClientProvider : IHttpClientProvider
{
    private IRestClient? _client;

    /// <inheritdoc />
    public async Task<IRestClient> GetConfiguredClientAsync(string baseUrl)
    {
        return _client ??= new RestClient(baseUrl); 
    }
}