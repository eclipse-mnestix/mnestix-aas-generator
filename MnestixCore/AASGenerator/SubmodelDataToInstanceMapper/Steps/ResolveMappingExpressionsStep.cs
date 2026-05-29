using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines.Shared;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Evaluates JSONata expressions for each mapping descriptor and enforces cardinality.
/// Populates ctx.ResolvedMappings for the assignment step.
/// </summary>
public sealed class ResolveMappingExpressionsAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started ResolveMappingExpressionsStep");
        ResolveExpressions(ctx);
        ctx.Log("Finished ResolveMappingExpressionsStep");
        return Task.FromResult(ctx);
    }

    private static void ResolveExpressions(DataMappingContext ctx)
    {
        var resolved = new List<ResolvedMapping>();

        foreach (var descriptor in ctx.MappingDescriptors)
        {
            ctx.Qualifier = descriptor.Qualifier;

            var result = JsonataEvaluator.Evaluate(ctx.Data, descriptor.MappingExpression, ctx);

            // For multiLanguage field: treat empty/all-null object as missing
            if (descriptor.FieldName == "multiLanguage" && result is JObject obj &&
                (!obj.HasValues || obj.Properties().All(p =>
                    p.Value.Type == JTokenType.Null || string.IsNullOrEmpty(p.Value.ToString()))))
            {
                result = null;
            }

            if (result == null)
            {
                if (descriptor.IsMandatory)
                {
                    throw new SubmodelDataToInstanceMapperException(
                        $"Mandatory mapping '{descriptor.MappingExpression}' not found.", ctx);
                }

                ctx.LogWarning($"Optional mapping '{descriptor.MappingExpression}' not found in data, skipping.");
                resolved.Add(new ResolvedMapping { Descriptor = descriptor, ResolvedValue = null });
                continue;
            }

            resolved.Add(new ResolvedMapping { Descriptor = descriptor, ResolvedValue = result });
        }

        ctx.ResolvedMappings = resolved;
    }
}
