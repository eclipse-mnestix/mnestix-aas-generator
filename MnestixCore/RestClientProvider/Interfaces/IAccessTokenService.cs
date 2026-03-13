using MnestixCore.Dtos.AppSettingsOptions;

namespace MnestixCore.RestClientProvider.Interfaces;

/// <summary>
/// Service for managing and retrieving OAuth2 access tokens using the client credentials flow.
/// </summary>
/// <remarks>
/// This service is responsible for interacting with an OpenID Connect provider to 
/// obtain OAuth2 access tokens. It uses configuration settings provided via 
/// <see cref="RepositoryOpenIdConfiguration"/> to communicate with the authentication 
/// server. 
/// </remarks>
public interface IAccessTokenService
{
    /// <summary>
    /// Asynchronously retrieves an access token using the client credentials flow.
    /// </summary>
    /// <remarks>
    /// This method communicates with the authentication server to obtain an OAuth2 
    /// access token. It performs a request to the token endpoint using the client 
    /// credentials provided in the configuration. 
    /// </remarks>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains 
    /// the access token as a string.
    /// </returns>
    public Task<string> GetTokenAsync();
}