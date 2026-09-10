using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Rewrites legacy "SMT/" mapping-qualifier types to their "MnestixAASGenerator/" equivalents on
/// the cloned instance, so every downstream step and JSONPath literal only ever sees the new prefix.
/// Backward compatibility (MNE-428): blueprints authored with the old prefix keep working, whether
/// they are freshly created or already stored. "SMT/Cardinality" and any custom qualifier are left
/// untouched (see <see cref="QualifierAliases"/>).
/// </summary>
public sealed class NormalizeQualifierPrefixAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log("Started NormalizeQualifierPrefixStep");

        var rewrote = false;
        foreach (var typeToken in ctx.SubmodelInstance.SelectTokens("$..qualifiers[*].type").OfType<JValue>())
        {
            var original = typeToken.Value<string>();
            if (string.IsNullOrEmpty(original))
            {
                continue;
            }

            var canonical = QualifierAliases.Canonicalize(original);
            if (!string.Equals(canonical, original, StringComparison.Ordinal))
            {
                typeToken.Value = canonical;
                rewrote = true;
            }
        }

        if (rewrote)
        {
            ctx.LogInfo(
                "Blueprint uses legacy 'SMT/' mapping qualifiers; these are still supported for " +
                "backward compatibility but should be migrated to the 'MnestixAASGenerator/' prefix. " +
                "Note: the 'SMT/' prefix on these blueprint tags is only relevant for the generator's " +
                "mapping info and is not the IDTA SMT-spec 'SMT/Cardinality' qualifier, which is unchanged.");
        }

        ctx.Log("Finished NormalizeQualifierPrefixStep");
        return Task.FromResult(ctx);
    }
}
