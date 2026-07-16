using System.Globalization;
using MnestixCore.AasGenerator.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Adds the Concept qualifiers carrying the original blueprint id and the generation timestamp to the
/// submodel root <c>qualifiers</c> array, so the generated submodel remains traceable to the blueprint
/// and generation run that produced it.
/// </summary>
public sealed class AddConceptQualifiersAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    private const string ConceptQualifierKind = "ConceptQualifier";
    private const string BlueprintIdQualifierType = "Mnestix/OriginalBlueprintID";
    private const string GenerationTimestampQualifierType = "Mnestix/GenerationTimestamp";

    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started AddConceptQualifiersAasGeneratorPipelineStep");

        AddConceptQualifiers(ctx.SubmodelInstance, ctx.Blueprint, ctx.TimeProvider);
        ctx.LogInfo("Added concept qualifiers (blueprint id + generation timestamp) to submodel root");

        ctx.Log($"Finished AddConceptQualifiersAasGeneratorPipelineStep");
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
}
