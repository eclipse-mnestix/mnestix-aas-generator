using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.AasGenerator.Pipelines.Shared;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Discovers SMT/MappingInfo qualifiers, validates their format, detects duplicates,
/// and populates ctx.MappingDescriptors for downstream steps.
/// </summary>
public sealed class ValidateMappingQualifiersAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    private const string MappingInfoPrefix = "SMT/MappingInfo";

    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started ValidateMappingQualifiersStep");
        DiscoverAndValidate(ctx);
        ctx.Log("Finished ValidateMappingQualifiersStep");
        return Task.FromResult(ctx);
    }

    private static void DiscoverAndValidate(DataMappingContext ctx)
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
            var modelTypeToken = element["modelType"]
                ?? throw new SubmodelDataToInstanceMapperException("could not find matching modelType field of selected SME", ctx);
            var modelType = modelTypeToken.Value<string>()!;

            var fieldsOnElement = new List<(JToken qualifier, string fieldName)>();

            foreach (var qualifier in elementGroup)
            {
                ctx.Qualifier = qualifier;

                var qualifierType = qualifier["type"]?.Value<string>() ?? "";
                var fieldName = ParseFieldName(qualifierType, ctx);

                fieldsOnElement.Add((qualifier, fieldName));
            }

            // Duplicate field detection
            ValidateDuplicateFields(fieldsOnElement, ctx);

            // Build descriptors
            foreach (var (qualifier, fieldName) in fieldsOnElement)
            {
                var mappingExpression = qualifier["value"]?.Value<string>()
                    ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);

                var isMandatory = QualifierHelpers.GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;

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

    private static string ParseFieldName(string qualifierType, DataMappingContext ctx)
    {
        var segments = qualifierType.Split('/');
        if (segments.Length > 3)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Malformed qualifier type '{qualifierType}'. Expected 'SMT/MappingInfo' or 'SMT/MappingInfo/<FieldName>'.", ctx);
        }
        return segments.Length == 3 ? segments[2] : "value";
    }

    private static void ValidateDuplicateFields(List<(JToken qualifier, string fieldName)> qualifiersWithFields, DataMappingContext ctx)
    {
        var seen = new HashSet<string>();
        foreach (var (qualifier, fieldName) in qualifiersWithFields)
        {
            if (!seen.Add(fieldName))
            {
                var elementIdShort = qualifier.Parent?.Parent?.Parent?["idShort"]?.Value<string>() ?? "unknown";
                ctx.Qualifier = qualifier;
                throw new SubmodelDataToInstanceMapperException(
                    $"Duplicate mapping for field '{fieldName}' on element '{elementIdShort}'", ctx);
            }
        }
    }
}
