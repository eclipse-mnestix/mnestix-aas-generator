using Microsoft.Extensions.Logging;
using MnestixCore.AasGenerator;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TemplateProvider = MnestixCore.AasCreator.Templates.TemplateProvider;

namespace Mnestix.AasGenerator;

/// <summary>
/// Default <see cref="IAasGenerationEngine"/> implementation. Wraps the internal
/// data-mapping pipeline and AAS shell template to produce objects purely in-memory.
/// </summary>
internal sealed class AasGenerationEngine : IAasGenerationEngine
{
    private readonly IDataMapper _dataMapper;
    private readonly ILogger<AasGenerationEngine> _logger;

    public AasGenerationEngine(IDataMapper dataMapper, ILogger<AasGenerationEngine> logger)
    {
        _dataMapper = dataMapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public JObject MapSubmodel(JObject blueprint, JObject data, string? language, string submodelId)
    {
        var workflowLogger = new WorkflowLogger(_logger);
        var (instance, _) = _dataMapper.CreateSubmodelInstanceFromDataJson(blueprint, data, language, submodelId, workflowLogger);
        return instance;
    }

    /// <inheritdoc />
    public IReadOnlyList<SubmodelGenerationResult> GenerateSubmodels(
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false)
    {
        return requests.Select(request => GenerateOne(request, data, language, debug)).ToList();
    }

    /// <inheritdoc />
    public string CreateAasShellJson(AasIds aasIds) => TemplateProvider.GetAas(aasIds);

    /// <inheritdoc />
    public AasGenerationResult GenerateAas(
        AasIds aasIds,
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false)
    {
        var aasJson = CreateAasShellJson(aasIds);
        var submodelResults = GenerateSubmodels(requests, data, language, debug);
        return new AasGenerationResult(aasJson, submodelResults);
    }

    private SubmodelGenerationResult GenerateOne(SubmodelGenerationRequest request, JObject data, string? language, bool debug)
    {
        var workflowLogger = new WorkflowLogger(_logger);
        workflowLogger.LogInfo($"Mapping blueprint {request.BlueprintId} to submodel {request.SubmodelId}");

        try
        {
            ValidateIdShort(request.Blueprint, request.BlueprintId, workflowLogger);

            workflowLogger.LogInfo("Starting data mapping");
            var (instance, _) = _dataMapper.CreateSubmodelInstanceFromDataJson(
                request.Blueprint, data, language, request.SubmodelId, workflowLogger);
            workflowLogger.LogInfo("Data mapping completed");

            return new SubmodelGenerationResult
            {
                Success = true,
                BlueprintId = request.BlueprintId,
                GeneratedSubmodelId = request.SubmodelId,
                Submodel = instance,
                DebugInfo = debug ? new GenerationDebugInfo { Logs = workflowLogger.Logs } : null
            };
        }
        catch (SubmodelDataToInstanceMapperException e)
        {
            _logger.LogError(e, "Failed to map data to instance. BlueprintId: {BlueprintId}, Message: {Message}", request.BlueprintId, e.Message);
            return new SubmodelGenerationResult
            {
                Success = false,
                BlueprintId = request.BlueprintId,
                Message = e.Message,
                ErrorInfo = new GenerationErrorInfo
                {
                    Logs = workflowLogger.Logs,
                    Qualifier = e.Context?.Qualifier.ToString(Formatting.None),
                    QualifierPath = e.Context?.Qualifier.Path
                },
                DebugInfo = debug ? new GenerationDebugInfo { Logs = workflowLogger.Logs } : null
            };
        }
        catch (BlueprintValidationException e)
        {
            _logger.LogError(e, "Blueprint validation failed at generation-time. BlueprintId: {BlueprintId}", request.BlueprintId);
            return new SubmodelGenerationResult
            {
                Success = false,
                BlueprintId = request.BlueprintId,
                Message = "Blueprint validation failed. The blueprint may have been modified externally or was not migrated.",
                ValidationErrors = e.Errors,
                ErrorInfo = new GenerationErrorInfo { Logs = workflowLogger.Logs },
                DebugInfo = debug ? new GenerationDebugInfo { Logs = workflowLogger.Logs } : null
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Blueprint workflow failed. BlueprintId: {BlueprintId}, Message: {Message}", request.BlueprintId, e.Message);
            return new SubmodelGenerationResult
            {
                Success = false,
                BlueprintId = request.BlueprintId,
                Message = e.Message,
                ErrorInfo = new GenerationErrorInfo { Logs = workflowLogger.Logs },
                DebugInfo = debug ? new GenerationDebugInfo { Logs = workflowLogger.Logs } : null
            };
        }
    }

    private static void ValidateIdShort(JObject blueprint, string blueprintId, WorkflowLogger workflowLogger)
    {
        var subModelShortId = blueprint["idShort"]?.Value<string>();
        if (subModelShortId == null)
        {
            workflowLogger.LogError($"Blueprint idShort is null for {blueprintId}");
            throw new InvalidOperationException($"blueprint idShort of {blueprintId} needs to be not null");
        }

        workflowLogger.LogInfo($"Extracted idShort: {subModelShortId}");
    }
}
