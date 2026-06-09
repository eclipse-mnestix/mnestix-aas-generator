using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RestClientProvider.Interfaces;

namespace MnestixCore.RestClientProvider.OpenIdClientProvider;

/// <inheritdoc />
internal class AccessTokenService(
    IOptions<RepositoryOpenIdConfiguration> options,
    ILogger<AccessTokenService> logger
    ) : IAccessTokenService
{

    private readonly string _serverUrl = options.Value.Authority;
    private readonly string _discoveryUrl = options.Value.DiscoveryEndpoint;
    
    public async Task<string> GetTokenAsync()
    {
        var httpClient = new HttpClient();
        var tokenEndpoint = await GetTokenEndpoint(httpClient);

        var tokenResponse = await httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = tokenEndpoint,
            ClientId = options.Value.ClientId,
            ClientSecret = options.Value.ClientSecret,
        });
        
        if (tokenResponse.AccessToken is null)
        {
            throw new ApplicationException("Failed to retrieve the access token from the token response.");
        }
        
        return tokenResponse.AccessToken;
    }

    private async Task<string?> GetTokenEndpoint(HttpClient httpClient)
    {
        var tokenEndpoint = options.Value.TokenEndpoint;

        if (!string.IsNullOrWhiteSpace(tokenEndpoint)) return tokenEndpoint;
        
        var discoveryDoc = await httpClient.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = $"{_serverUrl}/{_discoveryUrl}",
            Policy =
            {
                ValidateIssuerName = options.Value.ValidateIssuer,
            }
        });

        if (discoveryDoc.IsError)
        {
            throw new HttpRequestException(discoveryDoc.Error);
        }

        tokenEndpoint = discoveryDoc.TokenEndpoint;

        return tokenEndpoint;
    }
}