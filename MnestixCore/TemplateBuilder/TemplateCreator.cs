using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using static MnestixCore.Shared.Base64StringDeAndEncoder;

namespace MnestixCore.TemplateBuilder;

internal class TemplateCreator : ITemplateCreator
{
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly string _base64TemplateAasId;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly ILogger<TemplateCreator> _logger;

    public TemplateCreator(
        IRepoProxyClient repoProxyClient,
        IOptions<ConfigurationOptions> configurationOptions,
        IOptions<RepoProxyOptions> repoProxyOptions,
        ILogger<TemplateCreator> logger)
    {
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));

        _repoProxyClient = repoProxyClient;
        _base64TemplateAasId = EncodeTo64(configurationOptions.Value.TemplatesAasId);
        _logger = logger;
    }

    public async Task AddNewSubmodelInTemplateAasAsync(string template, CancellationToken cancellationToken = default)
    {
        var templateSubmodelJson = JObject.Parse(template);

        if (string.IsNullOrEmpty(templateSubmodelJson["id"]?.ToString()))
        {
            throw new ArgumentException("template id cannot be empty.");
        }

        SetSemanticId(ref templateSubmodelJson);

        _logger.LogTrace("Write new template to repository: {SubmodelForRepo}", templateSubmodelJson);
        await _repoProxyClient.PostAsync(_repoProxyOptions.SubmodelPath, templateSubmodelJson.ToString(), cancellationToken);

        var submodelReference =
            new SubmodelReference(new List<Key> { new("Submodel", templateSubmodelJson["id"]!.ToString()) }, "ModelReference");

        var submodelReferenceJson = JsonConvert.SerializeObject(submodelReference, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });

        _logger.LogTrace("Write new reference submodel to aas: {submodelReferenceJSON}", submodelReferenceJson);
        await _repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}/{_base64TemplateAasId}/submodel-refs", submodelReferenceJson, cancellationToken);
    }

    private void SetSemanticId(ref JObject submodel)
    {
        var settings = new JsonSerializerSettings
        { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        var key = new Key("ConceptDescription", submodel["id"]!.ToString());

        var semanticId = submodel["semanticId"];

        if (semanticId == null)
        {
            var semanticIds = new SubmodelReference(new List<Key> { key }, "ExternalReference");
            submodel["semanticId"] = JToken.FromObject(semanticIds, JsonSerializer.CreateDefault(settings));
        }
        else
        {
            var keys = (JArray)semanticId["keys"]!;
            var keyToken = JToken.FromObject(key, JsonSerializer.CreateDefault(settings));
            keys.Insert(0, keyToken);
        }
    }
}