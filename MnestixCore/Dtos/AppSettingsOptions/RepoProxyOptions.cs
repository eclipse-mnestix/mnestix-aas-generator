namespace MnestixCore.Dtos.AppSettingsOptions;

/// <summary>
/// Holds the configuration for using the repository proxy.
/// </summary>
public class RepoProxyOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string RepoProxy = "RepoProxy";

    /// <summary>
    /// Relative path at the proxy to get all asset administration shells
    /// </summary>
    public string AasPath { get; set; } = string.Empty;

    /// <summary>
    /// Relative path at the proxy to the default templates submodels
    /// </summary>
    public string SubmodelPath { get; set; } = string.Empty;
}