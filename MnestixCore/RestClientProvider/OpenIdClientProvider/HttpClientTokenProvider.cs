using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider.OpenIdClientProvider;

/// <summary>
/// Provides a configured RestClient instance asynchronously, including an access token if available.
/// Implements the IHttpClientProvider interface.
/// </summary>
public class HttpClientTokenProvider(IAccessTokenService accessTokenService) : IHttpClientProvider
{
    private IRestClient? _client;
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
        if (_client != null) return _client;
        
        var token = await GetToken();
        _client = new RestClient(baseUrl);
        _client.AddDefaultHeader("Authorization", $"Bearer {token}");
        return _client;
    }
}