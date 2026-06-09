namespace MnestixApi.Options;

/// <summary>
/// Host-local view of the repository configuration section. The AAS Generator package consumes
/// these values via its own options; the host keeps a copy for controller-level routing decisions
/// (e.g. whether a remote templates API is configured).
/// </summary>
public class ConfigurationOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string Configuration = "Configuration";

    public string SubmodelTemplatesApiUrl { get; set; } = string.Empty;
}
