using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider.OpenIdClientProvider;

/// <summary>
/// Provides a configured RestClient instance asynchronously, including an access token if available.
/// Implements the IHttpClientProvider interface.
/// </summary>
public class HttpClientTokenProvider(IAccessTokenService accessTokenService) : IHttpClientProvider
{
    // Cache the initialization task, not the client: concurrent callers await the same
    // in-flight build, so the token endpoint is hit once and only one RestClient is created.
    private readonly Lock _gate = new();
    private Task<IRestClient>? _clientTask;

    /// <inheritdoc />
    public Task<IRestClient> GetConfiguredClientAsync(string baseUrl)
    {
        lock (_gate)
        {
            return _clientTask ??= BuildClientAsync(baseUrl);
        }
    }

    private async Task<IRestClient> BuildClientAsync(string baseUrl)
    {
        var token = await accessTokenService.GetTokenAsync();
        var client = new RestClient(baseUrl);
        client.AddDefaultHeader("Authorization", $"Bearer {token}");
        return client;
    }
}
