namespace MnestixCore.Dtos.AppSettingsOptions;

public class RepositoryOpenIdConfiguration
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string Options = "RepositoryOpenIdConnect";

    public string Authority { get; init; } = string.Empty;
    
    public string DiscoveryEndpoint { get; init; } = string.Empty;
    
    public string ClientId { get; init; } = string.Empty;
    
    public string ClientSecret { get; init; } = string.Empty;
    
    public string TokenEndpoint { get; init; } = string.Empty;
    
    public bool ValidateIssuer { get; init; }
}