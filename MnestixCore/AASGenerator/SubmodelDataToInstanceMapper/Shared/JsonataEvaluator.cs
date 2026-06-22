using Jsonata.Net.Native;
using Jsonata.Net.Native.JsonNet;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Shared;

public static class JsonataEvaluator
{
    /// <summary>
    /// Evaluates a JSONata expression against the given data token.
    /// Returns null when the expression resolves to Undefined (path not found).
    /// </summary>
    public static JToken? Evaluate(JToken dataJson, string expression, DataMappingContext ctx)
    {
        try
        {
            var query = new JsonataQuery(expression);
            var result = query.EvalNewtonsoft(dataJson);

            // JSONATA returns Undefined for missing paths instead of null
            if (result?.Type == JTokenType.Undefined)
            {
                return null;
            }

            return result;
        }
        catch (Exception e) when (e is not SubmodelDataToInstanceMapperException)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Error while evaluating JSONATA expression '{expression}': " + e.Message, e, ctx);
        }
    }
}
