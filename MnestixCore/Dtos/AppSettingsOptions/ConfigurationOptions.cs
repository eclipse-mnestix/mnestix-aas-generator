
namespace MnestixCore.Dtos.AppSettingsOptions;

internal class ConfigurationOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string Configuration = "Configuration";

    public string ConfigurationSubmodelId { get; set; } = string.Empty;
    public string TemplatesAasId { get; set; } = string.Empty;
    public string BlueprintsAasId { get; set; } = string.Empty;
    public string SubmodelTemplatesApiUrl { get; set; } = string.Empty;
    public string SubmodelBlueprintsApiUrl { get; set; } = string.Empty;
}