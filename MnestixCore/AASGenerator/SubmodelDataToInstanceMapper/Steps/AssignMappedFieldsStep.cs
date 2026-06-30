using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines.FieldAssigners;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Iterates resolved mappings and delegates assignment to the appropriate FieldAssigner.
/// </summary>
public sealed class AssignMappedFieldsAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started AssignMappedFieldsStep");
        AssignFields(ctx);
        ctx.Log("Finished AssignMappedFieldsStep");
        return Task.FromResult(ctx);
    }

    private static void AssignFields(DataMappingContext ctx)
    {
        // valueType must be assigned before value so value content can be validated against it.
        var ordered = ctx.ResolvedMappings
            .OrderBy(m => m.Descriptor.FieldName == "valueType" ? 0 : 1);

        foreach (var mapping in ordered)
        {
            if (mapping.ResolvedValue == null)
            {
                continue;
            }

            var descriptor = mapping.Descriptor;
            ctx.Qualifier = descriptor.Qualifier;

            var assigner = FieldAssignerRegistry.GetAssigner(descriptor.FieldName);
            assigner.Assign(descriptor.Element, mapping.ResolvedValue, descriptor.ModelType, ctx.Language, ctx);

            ctx.LogInfo($"Successfully mapped value '{mapping.ResolvedValue}' from path '{descriptor.MappingExpression}' to field '{descriptor.FieldName}'");
        }
    }
}
