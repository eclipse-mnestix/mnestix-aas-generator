using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using RestSharp;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace MnestixCore.TemplateBuilder;

public class BlueprintCreator : IBlueprintCreator
{
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly string _base64BlueprintAasId;
    private readonly ILogger<BlueprintCreator> _logger;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly string _submodelBlueprintsApiUrl;
    private readonly Func<string, RestClient> _restClientFactory;
    private readonly TimeProvider _timeProvider;

    public BlueprintCreator(
        IRepoProxyClient repoProxyClient,
        IOptions<ConfigurationOptions> configurationOptions,
        IOptions<RepoProxyOptions> repoProxyOptions,
        ILogger<BlueprintCreator> logger,
        TimeProvider timeProvider,
        Func<string, RestClient>? restClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _base64BlueprintAasId = EncodeTo64(configurationOptions.Value.BlueprintsAasId);
        _submodelBlueprintsApiUrl = configurationOptions.Value.SubmodelBlueprintsApiUrl;
        _repoProxyClient = repoProxyClient;
        _logger = logger;
        _timeProvider = timeProvider;
        _restClientFactory = restClientFactory ?? (url => new RestClient(url));
    }

    /// <inheritdoc />
    public async Task<string> CreateNewSubmodelInBlueprintAasAsync(string templateSubmodel)
    {
        _logger.LogInformation("CreateBlueprintInAasAsync called");

        var blueprint = JObject.Parse(templateSubmodel);
        SetAasKindToInstance(ref blueprint);

        var submodelIdShort = (string)blueprint.SelectToken("idShort")!;
        SetDisplayName(submodelIdShort, ref blueprint);

        var submodelId = CreateSubmodelIdForBlueprint(submodelIdShort);
        SetSubmodelId(submodelId, ref blueprint);

        _logger.LogTrace("Write new blueprint to repository: {SubmodelForRepo}", blueprint);

        if (string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl))
        {
            await _repoProxyClient.PostAsync(_repoProxyOptions.SubmodelPath, blueprint.ToString());

            var submodelReference =
            new SubmodelReference(new List<Key>() { new("Submodel", submodelId) }, "ModelReference");
            var submodelReferenceJson = JsonConvert.SerializeObject(submodelReference, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            _logger.LogTrace("Write new reference submodel to aas: {submodelReferenceJSON}", submodelReferenceJson);
            await _repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}/{_base64BlueprintAasId}/submodel-refs", submodelReferenceJson);
        }
        else
        {
            await PostToBlueprintsApiAsync(blueprint);
        }

        

        _logger.LogInformation("CreateNewSubmodelInBlueprintAasAsync - return new submodelId: {SubmodelId}",
            submodelId);
        return submodelId;
    }

    /// <inheritdoc />
    public async Task UpdateSubmodelInBlueprintAasAsync(string submodel, string submodelId)
    {
        _logger.LogInformation("UpdateSubmodelInBlueprintAasAsync called");

        if (string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl))
        {
            await _repoProxyClient.PutAsync(
                _repoProxyOptions.SubmodelPath + "/" + EncodeTo64(submodelId),
                submodel);
        }
        else
        {
            await PutToBlueprintsApiAsync(submodel, submodelId);
        }

        _logger.LogInformation("UpdateSubmodelInBlueprintAasAsync - done");
    }

    /// <inheritdoc />
    public async Task DeleteSubmodelInBlueprintAasAsync(string submodelIdBase64Encoded)
    {
        _logger.LogInformation("DeleteSubmodelInBlueprintAasAsync called");

        var submodelReferencePath =
            _repoProxyOptions.AasPath + "/" + _base64BlueprintAasId + "/submodel-refs/" + submodelIdBase64Encoded;
        var submodelPath = _repoProxyOptions.SubmodelPath + "/" + submodelIdBase64Encoded;

        if (string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl))
        {
            await _repoProxyClient.DeleteAsync(submodelReferencePath);
            await _repoProxyClient.DeleteAsync(submodelPath);
        }
        else
        {
            await DeleteFromBlueprintsApiAsync(submodelIdBase64Encoded);
        }

        _logger.LogInformation("DeleteSubmodelInBlueprintAasAsync - done");
    }

    private async Task PostToBlueprintsApiAsync(JObject blueprint)
    {
        var client = _restClientFactory(_submodelBlueprintsApiUrl);
        var request = new RestRequest
        {
            Method = Method.Post
        };

        request.AddHeader("Accept", "application/json");
        request.AddStringBody(blueprint.ToString(), DataFormat.Json);

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Failed to persist blueprint to blueprints endpoint. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                response.StatusCode,
                response.ErrorMessage);

            throw new InvalidOperationException(
                "Failed to persist blueprint to blueprints endpoint.");
        }
    }

    private void SetAasKindToInstance(ref JObject submodelForRepo)
    {
        _logger.LogDebug("SetAasKindToInstance");
        submodelForRepo["kind"] = "Instance";
    }

    private static string CreateSubmodelIdForBlueprint(string submodelIdShort)
    {
        return submodelIdShort.Replace("/", "").Replace(":", "")
               + "_Template_"
               + Guid.NewGuid();
    }

    private void SetDisplayName(string submodelIdShort, ref JObject submodel)
    {
        var displayName = submodelIdShort + "_" + _timeProvider.GetLocalNow().DateTime.ToString("s");
        _logger.LogDebug("SetDisplayName: {DisplayName}", displayName);

        var idShortQualifier = JToken.FromObject(new
        {
            type = "displayName",
            valueType = "xs:string",
            value = displayName
        });

        if (submodel["qualifiers"] is not JArray)
        {
            submodel["qualifiers"] = new JArray();
            var qualifiers = (JArray)submodel["qualifiers"]!;
            qualifiers.Add(idShortQualifier);
        }
        else
        {
            var qualifierDisplayNameAlreadyExisted = false;
            if (submodel["qualifiers"] is not JArray qualifiers) return;

            for (var i = 0; i < qualifiers.Count; i++)
            {
                if ((string)qualifiers[i]["type"]! != "displayName") continue;
                qualifiers[i]["value"] = displayName;
                qualifierDisplayNameAlreadyExisted = true;
            }

            if (qualifierDisplayNameAlreadyExisted == false)
            {
                qualifiers.Add(idShortQualifier);
            }
        }
    }

    private async Task PutToBlueprintsApiAsync(string submodel, string submodelId)
    {
        var submodelIdBase64Encoded = EncodeTo64(submodelId);
        var client = _restClientFactory(_submodelBlueprintsApiUrl);
        var request = new RestRequest('/' + submodelIdBase64Encoded)
        {
            Method = Method.Put
        };

        request.AddHeader("Accept", "application/json");
        request.AddStringBody(submodel, DataFormat.Json);

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Failed to update blueprint '{SubmodelId}'. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                submodelId,
                response.StatusCode,
                response.ErrorMessage);

            throw new InvalidOperationException(
                "Failed to update blueprint.");
        }
    }

    private async Task DeleteFromBlueprintsApiAsync(string submodelIdBase64Encoded)
    {
        var client = _restClientFactory(_submodelBlueprintsApiUrl);
        var request = new RestRequest('/' + submodelIdBase64Encoded)
        {
            Method = Method.Delete
        };

        request.AddHeader("Accept", "application/json");

        var response = await client.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            _logger.LogError(
                "Failed to delete blueprint '{SubmodelId}'. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                submodelIdBase64Encoded,
                response.StatusCode,
                response.ErrorMessage);

            throw new InvalidOperationException(
                "Failed to delete blueprint.");
        }
    }

    private void SetSubmodelId(string submodelId, ref JObject submodel)
    {
        _logger.LogDebug("SetSubmodelId : {SubmodelIdentifier}", submodelId);
        submodel["id"] = submodelId;
    }
}