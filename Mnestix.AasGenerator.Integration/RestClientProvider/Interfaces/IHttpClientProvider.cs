using RestSharp;

namespace MnestixCore.RestClientProvider.Interfaces;

/// <summary>
/// Defines a contract for providing a configured RestClient instance asynchronously.
/// </summary>
public interface IHttpClientProvider
{
    /// <summary>
    /// Asynchronously retrieves a configured RestClient instance for the specified base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL to be used by the RestClient for making HTTP requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing the configured RestClient instance.</returns>
    public Task<IRestClient> GetConfiguredClientAsync(string baseUrl);
}