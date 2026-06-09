namespace MnestixApi.Options;

/// <summary>
/// Holds the configuration of the inbound API key used to authenticate customer-facing endpoints.
/// This is host-local inbound auth and is independent of any outbound repository API key the
/// AAS Generator package consumes.
/// </summary>
public class CustomerEndpointsSecurityOptions
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
