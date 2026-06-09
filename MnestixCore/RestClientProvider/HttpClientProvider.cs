using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider;

/// <summary>
/// Provides a configured RestClient instance asynchronously without including an access token.
/// Implements the IHttpClientProvider interface.
/// </summary>
internal class HttpClientProvider : IHttpClientProvider
{
    /// <inheritdoc />
    public async Task<IRestClient> GetConfiguredClientAsync(string baseUrl)
    {
        var client = new RestClient(baseUrl);
        return client;
    }
}