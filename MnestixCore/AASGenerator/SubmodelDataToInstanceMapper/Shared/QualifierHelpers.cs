using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Shared;

public static class QualifierHelpers
{
    /// <summary>
    /// Builds a recursive JSONPath that selects every qualifier of the given <paramref name="qualifierType"/>
    /// anywhere in the submodel tree (i.e. <c>$..qualifiers[?(@.type=='&lt;type&gt;')]</c>).
    /// </summary>
    public static string RecursiveQualifierPath(string qualifierType)
    {
        return $"$..qualifiers[?(@.type=='{qualifierType}')]";
    }

    /// <summary>
    /// Navigates from a qualifier token to its sibling SMT/Cardinality qualifier within the same qualifiers array.
    /// </summary>
    public static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.Parent is the "qualifiers" JArray
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }
}
