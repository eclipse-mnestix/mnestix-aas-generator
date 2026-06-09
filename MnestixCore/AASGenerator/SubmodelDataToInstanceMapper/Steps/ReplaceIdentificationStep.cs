using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

internal sealed class ReplaceIdentificationAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started ReplaceIdentificationStep");
        ReplaceIdentification(ctx.SubmodelInstance, ctx.NewSubmodelId, ctx);
        ctx.Log($"Finished ReplaceIdentificationStep");
        return Task.FromResult(ctx);
    }

    private static void ReplaceIdentification(JObject submodel, string newSubmodelId, DataMappingContext ctx)
    {
        var id = submodel["id"] ?? throw new SubmodelDataToInstanceMapperException("Could not find id property in blueprint", ctx);
        var oldId = id.Value<string>();
        id.Replace(newSubmodelId);
        ctx.LogInfo($"Replaced submodel ID from '{oldId}' to '{newSubmodelId}'");
    }
}
