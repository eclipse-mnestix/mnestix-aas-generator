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
    public async Task<IEnumerable<AasGeneratorResult>> AddDataToAasAsync(string base64EncodedAasId, IEnumerable<string> blueprintsIds, JObject data, string language, bool debug = false)
    {
        var blueprintsResults = blueprintsIds.Select(async blueprintId =>
        {
            var workflowLogger = new WorkflowLogger(_logger);

            var (blueprintError, blueprint) = await TryGetBlueprintFromBlueprintProviderAsync(blueprintId, workflowLogger);
            if (blueprintError != null)
            {
                return blueprintError;
            }

            var (shortIdError, blueprintShortId) = TryGetIdShortFromBlueprint(blueprint!, blueprintId, workflowLogger);
            if (shortIdError != null)
            {
                return shortIdError;
            }

            var (idGeneratorError, newSubmodelId) = await TryGenerateSubmodelIdAsync(blueprintId, workflowLogger);
            if (idGeneratorError != null)
            {
                return idGeneratorError;
            }

            var (mappingError, instance) = TryMapDataToInstance(blueprint!, data, language, blueprintId, newSubmodelId!, debug, workflowLogger);
            if (mappingError != null)
            {
                return mappingError;
            }

            var errorWhileAdding = await TryAddSubmodelToAasAsync(base64EncodedAasId, instance!, blueprintId, workflowLogger);
            if (errorWhileAdding != null)
            {
                return errorWhileAdding;
            }

            // when everything went through, we can return a success for this blueprint id
            return new AasGeneratorResult
            {
                Success = true,
                BlueprintId = blueprintId,
                GeneratedSubmodelId = newSubmodelId!,
                DebugInfo = debug ? new AasGeneratorDebugInfo { Logs = workflowLogger.Logs } : null
            };
        });

        return await Task.WhenAll(blueprintsResults);
    }

    /// <summary>
    /// Attempts to fetch a blueprint from the provider and wraps any failure into an <see cref="AasGeneratorResult"/>.
    /// </summary>
    /// <param name="blueprintId">Identifier of the blueprint to retrieve.</param>
    /// <returns>Tuple containing an error result or the fetched blueprint.</returns>
    private async Task<(AasGeneratorResult? Error, JObject? Result)> TryGetBlueprintFromBlueprintProviderAsync(string blueprintId, WorkflowLogger workflowLogger)
    {
        var base64BlueprintId = Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(blueprintId));

        workflowLogger.LogInfo($"Fetching blueprint: {blueprintId}");
        try
        {
            var blueprint = await _blueprintProvider.GetBlueprintAsync(base64BlueprintId);
            workflowLogger.LogInfo("Blueprint fetched successfully");
            return (null, blueprint);
        }
        catch (Exception e)
        {
            workflowLogger.LogError($"Blueprint fetch failed: {e.Message}");
            var error = new AasGeneratorResult
            {
                Message = "Failed to fetch blueprint from blueprint provider: " + e.Message,
                BlueprintId = blueprintId,
                Success = false,
                ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
            };
            _logger.LogError(e, $"Failed to fetch blueprint from blueprint provider. BlueprintId: {blueprintId}, Message: {e.Message}");
            return (error, null);
        }
    }

    /// <summary>
    /// Maps incoming data onto the provided blueprint while handling mapping failures.
    /// </summary>
    /// <param name="blueprint">Blueprint that should receive the payload values.</param>
    /// <param name="data">Payload values to apply.</param>
    /// <param name="language">Language code used for localization during mapping.</param>
    /// <param name="blueprintId">Identifier of the processed blueprint.</param>
    /// <param name="newSubmodelId">Identifier allocated for the produced submodel instance.</param>
    /// <param name="debug">Whether to include debug logs in the result.</param>
    /// <returns>Tuple containing an error result or the generated submodel instance with optional logs.</returns>
    private (AasGeneratorResult? Error, JObject? Result) TryMapDataToInstance(JObject blueprint, JObject data, string language, string blueprintId, string newSubmodelId, bool debug, WorkflowLogger workflowLogger)
    {
        workflowLogger.LogInfo("Starting data mapping");
        try
        {
            var (instance, context) = _dataToInstanceMapper.CreateSubmodelInstanceFromDataJson(blueprint, data, language, newSubmodelId);
            workflowLogger.AddRange(context.Logs);
            workflowLogger.LogInfo("Data mapping completed");
            return (null, instance);
        }
        catch (SubmodelDataToInstanceMapperException e)
        {
            if (e.Context?.Logs != null)
            {
                workflowLogger.AddRange(e.Context.Logs);
            }
            workflowLogger.LogError($"Data mapping failed: {e.Message}");
            var error = new AasGeneratorResult
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
                DebugInfo = debug && workflowLogger.Logs.Count > 0 ? new AasGeneratorDebugInfo { Logs = workflowLogger.Logs } : null
            };
            _logger.LogError(e, $"Failed to map data to instance. BlueprintId: {blueprintId}, Message: {e.Message}, ErrorInfo: {error.ErrorInfo}");
            return (error, null);
        }
    }

    /// <summary>
    /// Extracts the submodel short identifier from the blueprint and validates its presence.
    /// </summary>
    /// <param name="blueprint">Blueprint that should contain the short identifier.</param>
    /// <param name="blueprintId">Identifier used in error reporting.</param>
    /// <returns>Tuple containing an error when the short id is missing or the extracted value.</returns>
    private (AasGeneratorResult? Error, string? Result) TryGetIdShortFromBlueprint(JObject blueprint, string blueprintId, WorkflowLogger workflowLogger)
    {
        var subModelShortId = blueprint["idShort"]?.Value<string>();
        if (subModelShortId == null)
        {
            workflowLogger.LogError($"Blueprint idShort is null for {blueprintId}");
            var error = new AasGeneratorResult
            {
                Success = false,
                BlueprintId = blueprintId,
                Message = $"blueprint shortId of {blueprintId} needs to be not null",
                ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
            };
            return (error, null);
        }

        workflowLogger.LogInfo($"Extracted idShort: {subModelShortId}");
        return (null, subModelShortId);
    }

    /// <summary>
    /// Persists a generated submodel instance and links it to the target shell, converting repository errors into results.
    /// </summary>
    /// <param name="base64EncodedAasId">Encoded identifier of the shell that receives the submodel reference.</param>
    /// <param name="submodelInstance">Instance Submodel to store in the repository.</param>
    /// <param name="blueprintId">Blueprint identifier used for logging and error messages.</param>
    /// <returns>Null when the operation succeeds or an error result describing the failure.</returns>
    private async Task<AasGeneratorResult?> TryAddSubmodelToAasAsync(string base64EncodedAasId, JObject submodelInstance, string blueprintId, WorkflowLogger workflowLogger)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(base64EncodedAasId))
            {
                workflowLogger.LogError("The AAS id is empty");
                return new AasGeneratorResult
                {
                    Success = false,
                    BlueprintId = blueprintId,
                    Message = "The aas id cannot be empty!",
                    ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
                };
            }

            var submodelId = submodelInstance["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(submodelId))
            {
                workflowLogger.LogError("The submodel id is empty");
                return new AasGeneratorResult
                {
                    Success = false,
                    BlueprintId = blueprintId,
                    Message = "The submodel id cannot be empty!",
                    ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
                };
            }

            workflowLogger.LogInfo("Posting submodel to repository");
            await _repoProxyClient.PostAsync(_repoProxyOptions.SubmodelPath, submodelInstance.ToString());

            var submodelReference =
                new SubmodelReference(new List<Key> { new("Submodel", submodelId) }, "ModelReference");
            var submodelReferenceJson = JsonConvert.SerializeObject(submodelReference, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            workflowLogger.LogInfo("Adding submodel reference to shell");
            await _repoProxyClient.PostAsync($"shells/{base64EncodedAasId}/submodel-refs", submodelReferenceJson);
            workflowLogger.LogInfo("Submodel reference added to shell");

            return null;
        }
        catch (RepoProxyException e)
        {
            workflowLogger.LogError($"Repository operation failed: {e.Message}");
            var error = new AasGeneratorResult
            {
                Success = false,
                BlueprintId = blueprintId,
                Message = e.Message,
                ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
            };
            _logger.LogError(e, $"Failed to add submodel to AAS. BlueprintId: {blueprintId}, AasId: {base64EncodedAasId}, Message: {e.Message}");
            return error;
        }
    }

    /// <summary>
    /// Generates a new submodel identifier using the configured generator service.
    /// </summary>
    /// <param name="blueprintId">Blueprint identifier used for contextual error reporting.</param>
    /// <returns>Tuple containing a failure result or the generated identifier.</returns>
    private async Task<(AasGeneratorResult?, string?)> TryGenerateSubmodelIdAsync(string blueprintId, WorkflowLogger workflowLogger)
    {
        workflowLogger.LogInfo("Generating submodel ID");
        try
        {
            var ids = await _idGenerator.GenerateSubmodelIdsAsync();
            var newId = ids.First();
            workflowLogger.LogInfo($"Submodel ID generated: {newId}");
            return (null, newId);
        }
        catch (Exception e)
        {
            workflowLogger.LogError($"Submodel ID generation failed: {e.Message}");
            var error = new AasGeneratorResult
            {
                Success = false,
                BlueprintId = blueprintId,
                Message = "could not generate submodel id",
                ErrorInfo = new AasGeneratorErrorInfo { Logs = workflowLogger.Logs }
            };
            _logger.LogError(e, $"Could not generate submodel id. BlueprintId: {blueprintId}, Message: {e.Message}");
            return (error, null);
        }
    }
}