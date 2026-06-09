using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

internal sealed class RemoveTopLevelQualifiersAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started RemoveTopLevelQualifiersStep");
        var qualifierCount = RemoveTopLevelQualifiers(ctx.SubmodelInstance);
        ctx.LogInfo($"Removed {qualifierCount} top-level qualifiers from submodel instance");
        ctx.Log($"Finished RemoveTopLevelQualifiersStep");
        return Task.FromResult(ctx);
    }

    private static int RemoveTopLevelQualifiers(JObject submodel)
    {
        var qualifiers = submodel["qualifiers"] as JArray;
        var count = qualifiers?.Count ?? 0;
        submodel["qualifiers"]?.Replace(new JArray());
        return count;
    }
}

