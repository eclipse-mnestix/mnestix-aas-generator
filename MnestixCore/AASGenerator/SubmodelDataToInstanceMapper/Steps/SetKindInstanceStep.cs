using MnestixCore.AasGenerator.Interfaces;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Sets kind = "Instance" at top-level.
/// </summary>
internal sealed class SetKindInstanceAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started SetKindInstanceStep");
        var kindProperty = ctx.SubmodelInstance.Property("kind");
        if (kindProperty == null)
        {
            ctx.LogWarning("Blueprint does not have a 'kind' property, this may indicate an invalid blueprint structure");
        }
        else
        {
            var oldKind = kindProperty.Value.ToString();
            kindProperty.Value = "Instance";
            ctx.LogInfo($"Set kind from '{oldKind}' to 'Instance'");
        }
        ctx.Log($"Finished SetKindInstanceStep");
        return Task.FromResult(ctx);
    }
}
