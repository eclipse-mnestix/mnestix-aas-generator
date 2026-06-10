using Newtonsoft.Json.Linq;

namespace MnestixCore.ConfigurationService.Interfaces;

public interface IConfigurationService
{
    public Task<JObject?> GetIdGenerationSettings();
    public Task<bool> PatchSingleIdGenerationSetting(string path, string value);
}