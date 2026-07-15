using System.Globalization;
using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// In this Step, we remove the top level Qualifiers and all Mapping Qualifiers.
/// We also add the Blueprint ID and the Generation Timestamp at the root of the Submodel for better traceability.
/// </summary>
public sealed class RemoveQualifiersAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    /// <summary>
    /// Prefixes (case-insensitive) that a qualifier <c>type</c> must start with before it is considered
    /// a generation-only qualifier at all. Only the part of the type after the prefix is checked for the
    /// mapping substrings, so unrelated qualifiers that happen to contain "mapping" are left untouched.
    /// </summary>
    private static readonly IReadOnlyList<string> MappingQualifierTypePrefixes =
    [
        "smt/",
        "mnestix/"
    ];

    /// <summary>
    /// Substrings (case-insensitive) that mark a qualifier as a generation-only mapping qualifier.
    /// Any qualifier whose <c>type</c> starts with one of <see cref="MappingQualifierTypePrefixes"/> and
    /// whose remainder contains one of these substrings is stripped from the output.
    /// </summary>
    private static readonly IReadOnlyList<string> MappingQualifierTypeSubstrings =
    [
        "mapping"
    ];

    private const string ConceptQualifierKind = "ConceptQualifier";
    private const string BlueprintIdQualifierType = "Mnestix/OriginalBlueprintID";
    private const string GenerationTimestampQualifierType = "Mnestix/GenerationTimestamp";

    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started RemoveQualifiersAasGeneratorPipelineStep");

        var topLevelQualifierCount = RemoveTopLevelQualifiers(ctx.SubmodelInstance);
        ctx.LogInfo($"Removed {topLevelQualifierCount} top-level qualifiers from submodel instance");

        var mappingQualifierCount = RemoveMappingQualifiers(ctx.SubmodelInstance);
        ctx.LogInfo($"Removed {mappingQualifierCount} mapping qualifiers from submodel instance");

        AddConceptQualifiers(ctx.SubmodelInstance, ctx.Blueprint, ctx.TimeProvider);
        ctx.LogInfo("Added concept qualifiers (blueprint id + generation timestamp) to submodel root");

        ctx.Log($"Finished RemoveQualifiersAasGeneratorPipelineStep");
        return Task.FromResult(ctx);
    }

    /// <summary>
    /// Appends the Concept qualifiers carrying the original blueprint id and the generation timestamp
    /// to the submodel root <c>qualifiers</c> array, so the generated submodel remains traceable to the
    /// blueprint and generation run that produced it.
    /// </summary>
    private static void AddConceptQualifiers(JObject submodel, JObject blueprint, TimeProvider timeProvider)
    {
        var blueprintId = blueprint["id"]?.Value<string>();
        var generationTimestamp = timeProvider.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        if (submodel["qualifiers"] is not JArray qualifiers)
        {
            qualifiers = [];
            submodel["qualifiers"] = qualifiers;
        }

        qualifiers.Add(CreateConceptQualifier(BlueprintIdQualifierType, blueprintId, "xs:string"));
        qualifiers.Add(CreateConceptQualifier(GenerationTimestampQualifierType, generationTimestamp, "xs:dateTime"));
    }

    private static JObject CreateConceptQualifier(string type, string? value, string valueType)
    {
        return new JObject
        {
            ["kind"] = ConceptQualifierKind,
            ["type"] = type,
            ["value"] = value,
            ["valueType"] = valueType,
        };
    }

    private static int RemoveTopLevelQualifiers(JObject submodel)
    {
        var qualifiers = submodel["qualifiers"] as JArray;
        var count = qualifiers?.Count ?? 0;
        submodel["qualifiers"]?.Replace(new JArray());
        return count;
    }

    /// <summary>
    /// Removes all mapping qualifiers from every <c>qualifiers</c> array in the submodel tree
    /// (at any nesting depth, via JSONPath recursive descent).
    /// A qualifier is considered a mapping qualifier when its <c>type</c> (compared case-insensitively) starts with one of
    /// <see cref="MappingQualifierTypePrefixes"/> and the remainder contains any of
    /// <see cref="MappingQualifierTypeSubstrings"/>. Qualifiers without a matching type (e.g. SMT/Cardinality)
    /// are preserved.
    /// </summary>
    private static int RemoveMappingQualifiers(JObject submodel)
    {
        var removed = 0;

        foreach (var qualifiers in submodel.SelectTokens("$..qualifiers").OfType<JArray>())
        {
            var mappingQualifiers = qualifiers
                .Where(IsMappingQualifier)
                .ToList();

            foreach (var qualifier in mappingQualifiers)
            {
                qualifier.Remove();
                removed++;
            }
        }

        return removed;
    }

    private static bool IsMappingQualifier(JToken qualifier)
    {
        var type = qualifier["type"]?.Value<string>();
        if (string.IsNullOrEmpty(type))
        {
            return false;
        }

        var prefix = MappingQualifierTypePrefixes
            .FirstOrDefault(pre => type.StartsWith(pre, StringComparison.OrdinalIgnoreCase));
        if (prefix is null)
        {
            return false;
        }

        var remainder = type[prefix.Length..];
        return MappingQualifierTypeSubstrings
            .Any(substring => remainder.Contains(substring, StringComparison.OrdinalIgnoreCase));
    }
}

