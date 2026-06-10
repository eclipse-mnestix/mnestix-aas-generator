using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.ConfigurationService.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator;
using MnestixCore.RepoProxyClient.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace MnestixCore.ConfigurationService;

internal class ConfigurationService : IConfigurationService
{
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly string _base64ConfigurationSmId;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly ILogger<MnestixConfigurationProvider> _logger;

    public ConfigurationService(
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IOptions<ConfigurationOptions> configurationOptions,
        ILogger<MnestixConfigurationProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyClient = repoProxyClient;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _base64ConfigurationSmId = EncodeTo64(configurationOptions.Value.ConfigurationSubmodelId);
        _logger = logger;
    }

    public async Task<JObject?> GetIdGenerationSettings()
    {
        var result = await _repoProxyClient.GetAsync($"{_repoProxyOptions.SubmodelPath}/{_base64ConfigurationSmId}");

        if (string.IsNullOrWhiteSpace(result)) {
            _logger.LogWarning("No configuration submodel found");
            return null;
        }
        try
        {
            var json = JObject.Parse(result);

            return json;
        }
        catch (JsonReaderException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON recieved, please verify configuration submodel settings!");
            return null;
        }
    }

    public async Task<bool> PatchSingleIdGenerationSetting(string idShortPath, string value)
    {
        try
        {
            await _repoProxyClient.PatchAsync($"{_repoProxyOptions.SubmodelPath}/{_base64ConfigurationSmId}/submodel-elements/{idShortPath}/$value", value);
            return true;
        }
        catch (RepoProxyException ex)
        {
            _logger.LogWarning(ex.Message);
            return false;
        }
    }
}

