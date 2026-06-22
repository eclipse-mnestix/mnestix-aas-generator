using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider.OpenIdClientProvider;

/// <summary>
/// Provides a configured RestClient instance asynchronously, including an access token if available.
/// Implements the IHttpClientProvider interface.
/// </summary>
public class HttpClientTokenProvider(IAccessTokenService accessTokenService) : IHttpClientProvider
{
    private string? _accessToken;

    private async Task<string?> GetToken()
    {
        if (string.IsNullOrEmpty(_accessToken))
        {
            _accessToken = await accessTokenService.GetTokenAsync();
        }
        return _accessToken;
    }
    
    /// <inheritdoc />
    public async Task<IRestClient> GetConfiguredClientAsync(string baseUrl)
    {
        var token = await GetToken();
        var client = new RestClient(baseUrl);
        client.AddDefaultHeader("Authorization", $"Bearer {token}");
        return client;
    }
}