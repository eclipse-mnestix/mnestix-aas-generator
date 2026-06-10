using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using MnestixCore.TemplateBuilder.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Shared.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using static MnestixCore.Shared.Base64StringDeAndEncoder;
using RestSharp;

namespace MnestixCore.TemplateBuilder;

internal class TemplateProvider : ITemplateProvider
{
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly string _base64TemplateAasId;
    private readonly ISubmodelHandler _submodelHandler;
    private readonly ILogger<TemplateProvider> _logger;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly string _submodelTemplatesApiUrl;
    private readonly Func<string, RestClient> _restClientFactory;

    public TemplateProvider(
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IOptions<ConfigurationOptions> configurationOptions,
        ISubmodelHandler submodelHandler,
        ILogger<TemplateProvider> logger,
        Func<string, RestClient>? restClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(repoProxyOptions);
        ArgumentNullException.ThrowIfNull(configurationOptions);

        _repoProxyClient = repoProxyClient;
        _submodelHandler = submodelHandler;
        _logger = logger;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _base64TemplateAasId = EncodeTo64(configurationOptions.Value.TemplatesAasId);
        _submodelTemplatesApiUrl = configurationOptions.Value.SubmodelTemplatesApiUrl;
        _restClientFactory = restClientFactory ?? (url => new RestClient(url));
    }

    /// <inheritdoc />
    public async Task<JArray> GetAllTemplateSubmodelsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation($"GetAllTemplateSubmodelsAsync");

        if (!string.IsNullOrWhiteSpace(_submodelTemplatesApiUrl))
        {
            return await FetchTemplatesFromApiAsync(cancellationToken);
        }
        else
        {
            return await FetchTemplatesFromAasGeneratorApiAsync(cancellationToken);
        }
    }

    private async Task<string> GetTemplatesFromReference(List<string> submodelsIds, CancellationToken cancellationToken)
    {
        StringBuilder submodels = new StringBuilder();
        submodels.Append("[");
        foreach (var submodelsId in submodelsIds)
        {
            var submodelIdEncoded = Base64StringDeAndEncoder.EncodeTo64(submodelsId);
            var fetchedSubmodel = await _repoProxyClient.GetAsync(_repoProxyOptions.SubmodelPath + "/" + submodelIdEncoded, cancellationToken);
            submodels.Append(fetchedSubmodel + ",");
        }
        submodels.Append("]");

        return submodels.ToString();
    }

    private async Task<JArray> FetchTemplatesFromAasGeneratorApiAsync(CancellationToken cancellationToken)
        {
            var result = await _repoProxyClient.GetAsync($"{_repoProxyOptions.AasPath}/{_base64TemplateAasId}/submodel-refs", cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                const string message = "Failed to fetch submodel references from repository proxy.";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            var submodelRefs = JObject.Parse(result);
            var submodelsIds = _submodelHandler.GetSubmodelsIdsFromSubmodelsRefs(submodelRefs);

            var submodels = await GetTemplatesFromReference(submodelsIds, cancellationToken);

            return JArray.Parse(submodels);
        }

    private async Task<JArray> FetchTemplatesFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _restClientFactory(_submodelTemplatesApiUrl);
            var request = new RestRequest
            {
                Method = Method.Get
            };

            request.AddHeader("Cache-Control", "no-store");
            request.AddHeader("Accept", "application/json");

            var response = await client.ExecuteAsync(request, cancellationToken);

            if (!response.IsSuccessful)
            {
                _logger.LogError(
                    "Failed to fetch submodel templates from the repository. StatusCode: {StatusCode}, Error: {ErrorMessage}",
                    response.StatusCode,
                    response.ErrorMessage);

                throw new InvalidOperationException(
                    $"Failed to fetch submodel templates from the repository. Status code: {(int)response.StatusCode}.");
            }

            if (string.IsNullOrWhiteSpace(response.Content))
            {
                const string emptyResponseMessage = "Submodel templates endpoint returned an empty response.";
                _logger.LogError(emptyResponseMessage);
                throw new InvalidOperationException(emptyResponseMessage);
            }

            var payload = JObject.Parse(response.Content);

            if (payload["result"] is not JArray resultArray)
            {
                _logger.LogError(
                    "Unexpected response format from submodel templates endpoint. Payload: {Payload}",
                    payload);

                throw new InvalidOperationException(
                    "Unexpected response format from submodel templates endpoint.");
            }

            return resultArray;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            const string message = "Unknown error while fetching submodel templates.";
            _logger.LogError(ex, "Submodel templates request failed: {Message}", message);
            throw new InvalidOperationException(message, ex);
        }
    }
}
