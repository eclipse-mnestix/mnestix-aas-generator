using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines;
using MnestixCore.AasGenerator.Pipelines.Steps;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator;

/// <summary>
/// Maps raw submodel data into a ready-to-persist AAS submodel instance using the configured pipeline.
/// </summary>
/// <remarks>
/// The mapping pipeline clones the blueprint, aligns the generated instance metadata, applies the payload, and
/// removes transient qualifiers before returning a finalized submodel instance.
/// </remarks>
public sealed class DataMapper : IDataMapper
{
    /// <summary>
    /// Builds and executes the mapping pipeline to create a submodel instance from the incoming data payload.
    /// </summary>
    /// <param name="blueprint">Blueprint that defines the structure of the submodel.</param>
    /// <param name="data">Payload with the values that should be injected into the submodel.</param>
    /// <param name="language">Preferred language code used for localized text elements.</param>
    /// <param name="newSubmodelId">Identifier assigned to the generated submodel instance.</param>
    /// <param name="workflowLogger">Shared workflow logger for accumulating log entries.</param>
    /// <returns>Tuple containing the newly created submodel and the context.</returns>
    public (JObject Instance, DataMappingContext Context) CreateSubmodelInstanceFromDataJson(JObject blueprint, JObject data, string? language, string newSubmodelId, WorkflowLogger workflowLogger)
    {
        var context = new DataMappingContext(blueprint, data, language, newSubmodelId, workflowLogger);

        // Build pipeline with all the steps in the correct order
        var pipeline = new Pipelines.Core.PipelineBuilder<DataMappingContext>()
            .Use<DeepCloneBlueprintAasGeneratorPipelineStep>()
            .Use<SetKindInstanceAasGeneratorPipelineStep>()
            .Use<DuplicateCollectionsAasGeneratorPipelineStep>()
            .Use<FilterElementsAasGeneratorPipelineStep>()
            .Use<ValidateMappingQualifiersAasGeneratorPipelineStep>()
            .Use<ResolveMappingExpressionsAasGeneratorPipelineStep>()
            .Use<AssignMappedFieldsAasGeneratorPipelineStep>()
            .Use<RemoveTopLevelQualifiersAasGeneratorPipelineStep>()
            .Use<ReplaceIdentificationAasGeneratorPipelineStep>()
            .Build();

        var resultCtx = pipeline.RunAsync(context).GetAwaiter().GetResult();
        return (resultCtx.SubmodelInstance, resultCtx);
    }
}
