using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines.FieldAssigners;
using MnestixCore.AasGenerator.Pipelines.Shared;
using MnestixCore.Errors;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Discovers MnestixAASGenerator/MappingInfo qualifiers and populates ctx.MappingDescriptors for downstream steps.
/// Structural validation (field names, duplicates, conflicts) is handled by the upstream
/// ValidateBlueprintAasGeneratorPipelineStep via BlueprintValidator.
/// </summary>
public sealed class DiscoverMappingDescriptorsAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    private const string MappingInfoPrefix = QualifierAliases.MappingInfoPrefix;

    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started DiscoverMappingDescriptorsStep");
        Discover(ctx);
        ctx.Log("Finished DiscoverMappingDescriptorsStep");
        return Task.FromResult(ctx);
    }

    private static void Discover(DataMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;

        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[*]")
            .Where(q => q["type"]?.Value<string>() is string t &&
                        (t == MappingInfoPrefix || t.StartsWith(MappingInfoPrefix + "/", StringComparison.Ordinal)))
            .ToList();

        var qualifiersByElement = qualifiers
            .GroupBy(q => q.Parent?.Parent?.Parent)
            .Where(g => g.Key != null);

        var descriptors = new List<MappingDescriptor>();

        foreach (var elementGroup in qualifiersByElement)
        {
            var element = elementGroup.Key!;
            var modelType = element["modelType"]?.Value<string>()
                ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of selected SME", ctx);

            foreach (var qualifier in elementGroup)
            {
                ctx.Qualifier = qualifier;

                var qualifierType = qualifier["type"]?.Value<string>() ?? "";
                var segments = qualifierType.Split('/');
                var fieldName = segments.Length == 3 ? segments[2] : "value";

                var mappingExpression = qualifier["value"]?.Value<string>() ?? "";
                // The element's cardinality (One/ZeroToOne/...) is shared across all of its field
                // mappings, but some fields are always optional regardless (e.g. displayName).
                // The assigner owns that decision, keeping field-specific rules out of this step.
                var isMandatory = !FieldAssignerRegistry.GetAssigner(fieldName).IsAlwaysOptional &&
                    (QualifierHelpers.GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false);

                descriptors.Add(new MappingDescriptor
                {
                    Element = element,
                    FieldName = fieldName,
                    MappingExpression = mappingExpression,
                    IsMandatory = isMandatory,
                    ModelType = modelType,
                    Qualifier = qualifier
                });
            }
        }

        ctx.MappingDescriptors = descriptors;
    }
}
