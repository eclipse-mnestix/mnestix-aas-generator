using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Shared;

internal static class QualifierHelpers
{
    /// <summary>
    /// Navigates from a qualifier token to its sibling SMT/Cardinality qualifier within the same qualifiers array.
    /// </summary>
    public static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.Parent is the "qualifiers" JArray
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }
}
