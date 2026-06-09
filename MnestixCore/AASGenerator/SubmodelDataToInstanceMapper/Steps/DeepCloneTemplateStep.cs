using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Clones the blueprint into a fresh instance we can mutate.
/// </summary>
internal sealed class DeepCloneBlueprintAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started DeepCloneBlueprintStep");
        var blueprintId = ctx.Blueprint["id"]?.Value<string>() ?? ctx.Blueprint["idShort"]?.Value<string>() ?? "unknown";
        ctx.LogInfo($"Cloning blueprint with ID/idShort: '{blueprintId}'");
        ctx.SubmodelInstance = (JObject)ctx.Blueprint.DeepClone();
        ctx.Log($"Finished DeepCloneBlueprintStep");
        return Task.FromResult(ctx);
    }
}
