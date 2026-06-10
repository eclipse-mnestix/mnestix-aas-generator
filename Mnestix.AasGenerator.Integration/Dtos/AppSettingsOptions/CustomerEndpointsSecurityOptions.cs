namespace MnestixCore.Dtos.AppSettingsOptions;

/// <summary>
/// Holds the configuration of the ApiKey
/// </summary>
internal class CustomerEndpointsSecurityOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string CustomerEndpointsSecurity = "CustomerEndpointsSecurity";

    /// <summary>
    /// The api key needed to request the endpoints.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}