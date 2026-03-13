using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using MnestixCore.Shared.Interfaces;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json.Linq;
using RestSharp;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace MnestixCore.TemplateBuilder;

public class BlueprintProvider : IBlueprintProvider
{
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly string _base64BlueprintAasId;
    private readonly ISubmodelHandler _submodelHandler;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly string _submodelBlueprintsApiUrl;
    private readonly ILogger<BlueprintProvider> _logger;
    private readonly Func<string, RestClient> _restClientFactory;

    public BlueprintProvider(
        IRepoProxyClient repoProxyClient,
        IOptions<ConfigurationOptions> configurationOptions,
        IOptions<RepoProxyOptions> repoProxyOptions,
        ISubmodelHandler submodelHandler,
        ILogger<BlueprintProvider> logger,
        Func<string, RestClient>? restClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(repoProxyOptions);
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyClient = repoProxyClient;
        _submodelHandler = submodelHandler;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _base64BlueprintAasId = EncodeTo64(configurationOptions.Value.BlueprintsAasId);
        _submodelBlueprintsApiUrl = configurationOptions.Value.SubmodelBlueprintsApiUrl;
        _logger = logger;
        _restClientFactory = restClientFactory ?? (url => new RestClient(url));
    }

    /// <inheritdoc />
    public async Task<JArray> GetAllBlueprintsAsync()
    {
        if (!string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl))
        {
            return await FetchBlueprintsAsync();
        }

        var submodelsRefsFromRepo = await _repoProxyClient.GetAsync($"{_repoProxyOptions.AasPath}/{_base64BlueprintAasId}/submodel-refs");
        var submodelRefs = JObject.Parse(submodelsRefsFromRepo.Result);

        var submodelsIds = _submodelHandler.GetSubmodelsIdsFromSubmodelsRefs(submodelRefs);

        var submodels = await GetBlueprintsFromReference(submodelsIds);
        return JArray.Parse(submodels);
    }

    /// <inheritdoc />
    public async Task<JObject> GetBlueprintAsync(string submodelIdShort)
    {
        if (!string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl))
        {
            return await FetchBlueprintsAsync(submodelIdShort);
        }

        var submodelFromRepo = await _repoProxyClient.GetAsync(_repoProxyOptions.SubmodelPath + "/" + submodelIdShort);
        return JObject.Parse(submodelFromRepo.Result);
    }

    private async Task<string> GetBlueprintsFromReference(IEnumerable<string> submodelsIds)
    {
        var submodels = new StringBuilder();
        submodels.Append('[');
        foreach (var submodelIdEncoded in submodelsIds.Select(Base64StringDeAndEncoder.EncodeTo64))
        {
            var (_, result) = await _repoProxyClient.GetAsync(_repoProxyOptions.SubmodelPath + "/" + submodelIdEncoded);
            if(JsonHelper.IsValidJson(result)){
                submodels.Append(result + ",");
            }
        }
        submodels.Append(']');

        return submodels.ToString();
    }

    private async Task<JArray> FetchBlueprintsAsync()
    {
        var client = _restClientFactory(_submodelBlueprintsApiUrl);
        var request = new RestRequest
        {
            Method = Method.Get
        };
        request.AddHeader("Accept", "application/json");

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Failed to fetch from blueprints endpoint. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                response.StatusCode,
                response.ErrorMessage);

            throw new InvalidOperationException(
                $"Failed to fetch from blueprints endpoint. Status code: {(int)response.StatusCode}.");
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            _logger.LogError("Blueprints endpoint returned an empty response.");
            throw new InvalidOperationException("Blueprints endpoint returned an empty response.");
        }

        JToken payload;
        try
        {
            payload = JToken.Parse(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse response from blueprints endpoint.");
            throw new InvalidOperationException("Failed to parse response from blueprints endpoint.", ex);
        }

        if (payload is JArray array)
        {
            return array;
        }

        if (payload is JObject obj && obj["result"] is JArray resultArray)
        {
            return resultArray;
        }

        _logger.LogError("Unexpected response format received when fetching blueprints: {Payload}", payload.ToString());
        throw new InvalidOperationException("Unexpected response format from blueprints endpoint.");
    }

    private async Task<JObject> FetchBlueprintsAsync(string submodelIdShort)
    {
        var client = _restClientFactory(_submodelBlueprintsApiUrl);
        var request = new RestRequest('/' + submodelIdShort)
        {
            Method = Method.Get
        };
        request.AddHeader("Accept", "application/json");

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Failed to fetch blueprint '{SubmodelId}'. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                submodelIdShort,
                response.StatusCode,
                response.ErrorMessage);

            throw new InvalidOperationException(
                $"Failed to fetch blueprints. Status code: {(int)response.StatusCode}.");
        }

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            _logger.LogError(
                "Blueprints endpoint returned an empty response for '{SubmodelId}'.",
                submodelIdShort);
            throw new InvalidOperationException("Blueprints endpoint returned an empty response.");
        }

        try
        {
            return JObject.Parse(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to parse blueprint '{SubmodelId}'.",
                submodelIdShort);
            throw new InvalidOperationException("Failed to parse blueprint.", ex);
        }
    }
}