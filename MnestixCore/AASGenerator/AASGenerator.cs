using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using MnestixCore.TemplateBuilder.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MnestixCore.AasGenerator;

/// <summary>
/// Coordinates blueprint retrieval, data mapping, and repository updates to append submodels to an AAS shell.
/// </summary>
/// <remarks>
/// The generator orchestrates a sequence of operations for each blueprint id: fetch the blueprint, derive identifiers,
/// map incoming payload data, and persist the resulting submodel while creating the appropriate shell reference.
/// </remarks>
public class AasGenerator : IAasGenerator
{
    private readonly IDataMapper _dataToInstanceMapper;
    private readonly IRepoProxyClient _repoProxyClient;
    private readonly IBlueprintProvider _blueprintProvider;
    private readonly IAasIdGeneratorService _idGenerator;
    private readonly RepoProxyOptions _repoProxyOptions;
    private readonly ILogger<AasGenerator> _logger;

    public AasGenerator(
        IDataMapper dataToInstanceMapper,
        IRepoProxyClient repoProxyClient,
        IBlueprintProvider blueprintProvider,
        IAasIdGeneratorService idGenerator,
        IOptions<RepoProxyOptions> repoProxyOptions,
        ILogger<AasGenerator> logger)
    {
        _dataToInstanceMapper = dataToInstanceMapper;
        _repoProxyClient = repoProxyClient;
        _blueprintProvider = blueprintProvider;
        _idGenerator = idGenerator;
        _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
        _logger = logger;
    }

    /// <summary>
    /// Adds mapped submodels described by the provided blueprints to the target AAS shell.
    /// </summary>
    /// <param name="base64EncodedAasId">Identifier of the target AAS shell encoded in Base64 URL safe format.</param>
    /// <param name="blueprintsIds">Blueprint identifiers that define the submodels to create.</param>
    /// <param name="data">Payload that contains the values to project onto each blueprint.</param>
    /// <param name="language">Preferred language code for localized text within the generated submodels.</param>
    /// <param name="debug">Whether to include debug logs in the results.</param>
    /// <returns>Collection of results indicating success or failure for each processed blueprint.</returns>
    public async Task<IEnumerable<AasGeneratorResult>> AddDataToAasAsync(string base64EncodedAasId, IEnumerable<string> blueprintsIds, JObject data, string language, bool debug = false, string? preamble = null)
    {
        var blueprintsResults = blueprintsIds.Select(async blueprintId =>
        {
            var workflowLogger = new WorkflowLogger(_logger);

            if (preamble != null)
            {
                workflowLogger.LogInfo(preamble);
            }
            workflowLogger.LogInfo($"Mapping blueprint {blueprintId} to AAS {base64EncodedAasId}");

            try
            {
                var blueprint = await GetBlueprintAsync(blueprintId, workflowLogger);
                ValidateIdShort(blueprint, blueprintId, workflowLogger);
                var newSubmodelId = await GenerateSubmodelIdAsync(workflowLogger);
                var instance = MapDataToInstance(blueprint, data, language, newSubmodelId, workflowLogger);
                await AddSubmodelToAasAsync(base64EncodedAasId, instance, workflowLogger);

                return new AasGeneratorResult
                {
                    Success = true,
                    BlueprintId = blueprintId,
                    GeneratedSubmodelId = newSubmodelId,
                    DebugInfo = debug ? new AasGeneratorDebugInfo { Logs = workflowLogger.Logs } : null
                };
            }
            catch (SubmodelDataToInstanceMapperException e)
            {
                _logger.LogError(e, "Failed to map data to instance. BlueprintId: {BlueprintId}, Message: {Message}", blueprintId, e.Message);
                return new AasGeneratorResult
                {
                    Success = false,
                    BlueprintId = blueprintId,
                    Message = e.Message,
                    ErrorInfo = new AasGeneratorErrorInfo
                    {
                        Logs = workflowLogger.Logs,
                        Qualifier = e.Context?.Qualifier.ToString(Formatting.None),
                        QualifierPath = e.Context?.Qualifier.Path
                    },
                    DebugInfo = debug ? new AasGeneratorDebugInfo { Logs = workflowLogger.Logs } : null
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Blueprint workflow failed. BlueprintId: {BlueprintId}, Message: {Message}", blueprintId, e.Message);
                return new AasGeneratorResult
                {
                    Success = false,
                    BlueprintId = blueprintId,
                    Message = e.Message,
                    ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs },
                    DebugInfo = debug ? new AasGeneratorDebugInfo { Logs = workflowLogger.Logs } : null
                };
            }
        });

        return await Task.WhenAll(blueprintsResults);
    }

    private async Task<JObject> GetBlueprintAsync(string blueprintId, WorkflowLogger workflowLogger)
    {
        var base64BlueprintId = Base64StringDeAndEncoder.EncodeTo64(blueprintId);

        workflowLogger.LogInfo($"Fetching blueprint: {blueprintId}");
        try
        {
            var blueprint = await _blueprintProvider.GetBlueprintAsync(base64BlueprintId);
            workflowLogger.LogInfo("Blueprint fetched successfully");
            return blueprint;
        }
        catch (Exception e)
        {
            workflowLogger.LogError($"Blueprint fetch failed: {e.Message}");
            throw;
        }
    }

    private JObject MapDataToInstance(JObject blueprint, JObject data, string language, string newSubmodelId, WorkflowLogger workflowLogger)
    {
        workflowLogger.LogInfo("Starting data mapping");
        try
        {
            var (instance, context) = _dataToInstanceMapper.CreateSubmodelInstanceFromDataJson(blueprint, data, language, newSubmodelId);
            workflowLogger.AddRange(context.Logs);
            workflowLogger.LogInfo("Data mapping completed");
            return instance;
        }
        catch (SubmodelDataToInstanceMapperException e)
        {
            if (e.Context?.Logs != null)
            {
                workflowLogger.AddRange(e.Context.Logs);
            }
            workflowLogger.LogError($"Data mapping failed: {e.Message}");
            throw;
        }
    }

    private void ValidateIdShort(JObject blueprint, string blueprintId, WorkflowLogger workflowLogger)
    {
        var subModelShortId = blueprint["idShort"]?.Value<string>();
        if (subModelShortId == null)
        {
            workflowLogger.LogError($"Blueprint idShort is null for {blueprintId}");
            throw new InvalidOperationException($"blueprint idShort of {blueprintId} needs to be not null");
        }

        workflowLogger.LogInfo($"Extracted idShort: {subModelShortId}");
    }

    private async Task AddSubmodelToAasAsync(string base64EncodedAasId, JObject submodelInstance, WorkflowLogger workflowLogger)
    {
        if (string.IsNullOrWhiteSpace(base64EncodedAasId))
        {
            workflowLogger.LogError("The AAS id is empty");
            throw new ArgumentException("The aas id cannot be empty!", nameof(base64EncodedAasId));
        }

        var submodelId = submodelInstance["id"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(submodelId))
        {
            workflowLogger.LogError("The submodel id is empty");
            throw new ArgumentException("The submodel id cannot be empty!");
        }

        workflowLogger.LogInfo("Posting submodel to repository");
        try
        {
            await _repoProxyClient.PostAsync(_repoProxyOptions.SubmodelPath, submodelInstance.ToString());

            var submodelReference =
                new SubmodelReference(new List<Key> { new("Submodel", submodelId) }, "ModelReference");
            var submodelReferenceJson = JsonConvert.SerializeObject(submodelReference, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            workflowLogger.LogInfo("Adding submodel reference to shell");
            await _repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}/{base64EncodedAasId}/submodel-refs", submodelReferenceJson);
            workflowLogger.LogInfo("Submodel reference added to shell");
        }
        catch (RepoProxyException e)
        {
            workflowLogger.LogError($"Repository operation failed: {e.Message}");
            throw;
        }
    }

    private async Task<string> GenerateSubmodelIdAsync(WorkflowLogger workflowLogger)
    {
        workflowLogger.LogInfo("Generating submodel ID");
        try
        {
            var ids = await _idGenerator.GenerateSubmodelIdsAsync();
            var newId = ids.First();
            workflowLogger.LogInfo($"Submodel ID generated: {newId}");
            return newId;
        }
        catch (Exception e)
        {
            workflowLogger.LogError($"Submodel ID generation failed: {e.Message}");
            throw;
        }
    }
}