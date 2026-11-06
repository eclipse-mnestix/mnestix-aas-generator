using Jsonata.Net.Native;
using Jsonata.Net.Native.JsonNet;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Shared;

public static class JsonataEvaluator
{
    /// <summary>
    /// Evaluates a JSONata expression against the given data token under a wall-clock timeout.
    /// Returns null when the expression resolves to Undefined (path not found).
    /// </summary>
    /// <remarks>
    /// The timeout is a resource-governance measure, not a containment boundary: a runaway
    /// evaluation is abandoned by the caller but cannot be aborted mid-flight, and a
    /// stack-overflowing expression still kills the process. Dangerous
    /// constructs are instead rejected at write time by the BlueprintValidator.
    /// </remarks>
    public static async Task<JToken?> EvaluateAsync(JToken dataJson, string expression, DataMappingContext ctx, TimeSpan timeout)
    {
        try
        {
            var result = await Task.Run(() =>
            {
                var query = new JsonataQuery(expression);
                return query.EvalNewtonsoft(dataJson);
            }).WaitAsync(timeout).ConfigureAwait(false);

            // JSONATA returns Undefined for missing paths instead of null
            if (result?.Type == JTokenType.Undefined)
            {
                return null;
            }

            return result;
        }
        catch (TimeoutException e)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Error while evaluating JSONATA expression '{expression}': evaluation exceeded the timeout of {timeout.TotalSeconds:0.###}s and was abandoned.", e, ctx);
        }
        catch (Exception e) when (e is not SubmodelDataToInstanceMapperException)
        {
            throw new SubmodelDataToInstanceMapperException(
                $"Error while evaluating JSONATA expression '{expression}': {e.Message}", e, ctx);
        }
    }
}
