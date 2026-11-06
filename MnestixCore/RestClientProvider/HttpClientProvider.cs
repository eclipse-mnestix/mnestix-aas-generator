using MnestixCore.RestClientProvider.Interfaces;
using RestSharp;

namespace MnestixCore.RestClientProvider;

/// <summary>
/// Provides a configured RestClient instance without including an access token.
/// Implements the IHttpClientProvider interface.
/// </summary>
public class HttpClientProvider : IHttpClientProvider
{
    // Cache the initialization task, not the client, so concurrent callers share one build
    // instead of each racing to construct a RestClient.
    private readonly Lock _gate = new();
    private Task<IRestClient>? _clientTask;

    /// <inheritdoc />
    public Task<IRestClient> GetConfiguredClientAsync(string baseUrl)
    {
        lock (_gate)
        {
            return _clientTask ??= Task.FromResult<IRestClient>(new RestClient(baseUrl));
        }
    }
}
