using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using MnestixCore.TemplateBuilder;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Validates the blueprint using the shared BlueprintValidator before any transformation.
/// Throws BlueprintValidationException if the blueprint has structural or semantic errors.
/// </summary>
internal sealed class ValidateBlueprintAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started ValidateBlueprintStep");

        var errors = ctx.BlueprintValidator.Validate(ctx.Blueprint);
        if (errors.Count > 0)
        {
            ctx.LogWarning($"Blueprint validation failed with {errors.Count} error(s)");
            throw new BlueprintValidationException(errors);
        }

        ctx.Log("Finished ValidateBlueprintStep");
        return Task.FromResult(ctx);
    }
}
