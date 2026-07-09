using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines.FieldAssigners;
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

            // The assigner owns what counts as "missing" for its field (e.g. language-map
            // fields treat an empty / all-empty object as missing), so optional mappings are
            // omitted (no empty value written) and mandatory ones fail.
            if (result != null &&
                FieldAssignerRegistry.GetAssigner(descriptor.FieldName).IsResolvedValueMissing(result))
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
                continue;
            }

            resolved.Add(new ResolvedMapping { Descriptor = descriptor, ResolvedValue = result });
        }

        ctx.ResolvedMappings = resolved;
    }
}
