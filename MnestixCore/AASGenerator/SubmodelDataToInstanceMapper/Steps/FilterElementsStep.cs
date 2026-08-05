using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json.Linq;
using Jsonata.Net.Native;
using Jsonata.Net.Native.JsonNet;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Pipeline step that filters submodel elements based on MnestixAASGenerator/FilterMappingInfo qualifiers.
/// </summary>
/// <remarks>
/// This step evaluates JSONATA boolean expressions in filter qualifiers and removes elements
/// from the submodel instance when the filter condition evaluates to false or null.
/// The filtering is applied after collection duplication but before data mapping.
/// </remarks>
public sealed class FilterElementsAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started FilterElementsStep");
        FilterElements(ctx);
        ctx.Log($"Finished FilterElementsStep");
        return Task.FromResult(ctx);
    }

    /// <summary>
    /// Filters elements from the submodel instance based on filter qualifiers.
    /// </summary>
    /// <param name="ctx">The data mapping context containing submodel instance and payload data.</param>
    /// <exception cref="SubmodelDataToInstanceMapperException">
    /// Thrown when filter evaluation fails or when a mandatory filter fails.
    /// </exception>
    /// <remarks>
    /// This method processes all MnestixAASGenerator/FilterMappingInfo qualifiers found in the submodel instance.
    /// For each filter:
    /// 1. Evaluates the JSONATA expression against the data payload
    /// 2. Removes the element if the expression evaluates to false/null
    /// 3. Throws an exception if a mandatory filter fails
    /// 4. Logs all filter decisions for debugging
    /// </remarks>
    private static void FilterElements(DataMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;
        
        // Find all MnestixAASGenerator/FilterMappingInfo qualifiers recursively
        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[?(@.type=='MnestixAASGenerator/FilterMappingInfo')]");
        
        if (!qualifiers.Any())
        {
            ctx.LogInfo("No filter qualifiers found, skipping filter step");
            return;
        }

        ctx.LogInfo($"Found {qualifiers.Count()} filter qualifier(s) to evaluate");

        // Process filters - collect elements to remove first to avoid modification during iteration
        var elementsToRemove = new List<JToken>();
        
        foreach (var qualifier in qualifiers)
        {
            ctx.Qualifier = qualifier;
            
            // Navigate up to find the element that contains this qualifier
            // qualifier → qualifiers array → value object → element
            var element = qualifier.Parent?.Parent?.Parent;
            if (element == null)
            {
                ctx.LogWarning("Could not find element for filter qualifier, skipping");
                continue;
            }
            
            // Get the filter expression
            var filterExpression = qualifier["value"]?.Value<string>();
            if (string.IsNullOrEmpty(filterExpression))
            {
                throw new SubmodelDataToInstanceMapperException("Filter expression cannot be null or empty", ctx);
            }
            
            // Check if this is a mandatory filter
            var isMandatory = GetCardinalityQualifier(qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
            
            try
            {
                // Evaluate the JSONATA boolean expression using EvalNewtonsoft for automatic type conversion
                var filterQuery = new JsonataQuery(filterExpression);
                var result = filterQuery.EvalNewtonsoft(data);
                
                bool shouldInclude = false;
                
                // Interpret the result as boolean
                if (result != null)
                {
                    if (result.Type == JTokenType.Boolean)
                    {
                        shouldInclude = result.Value<bool>();
                    }
                    else if (result.Type == JTokenType.Null)
                    {
                        shouldInclude = false;
                    }
                    else
                    {
                        // Non-null values are truthy
                        shouldInclude = true;
                    }
                }
                
                var elementId = element["idShort"]?.Value<string>() ?? "unknown";
                
                if (!shouldInclude)
                {
                    if (isMandatory)
                    {
                        throw new SubmodelDataToInstanceMapperException(
                            $"Mandatory filter failed for element '{elementId}': expression '{filterExpression}' evaluated to false", ctx);
                    }
                    
                    ctx.LogInfo($"Filter expression '{filterExpression}' evaluated to false for element '{elementId}', marking for removal");
                    elementsToRemove.Add(element);
                }
                else
                {
                    ctx.LogInfo($"Filter expression '{filterExpression}' evaluated to true for element '{elementId}', keeping element");
                }
            }
            catch (Exception e) when (e is not SubmodelDataToInstanceMapperException)
            {
                throw new SubmodelDataToInstanceMapperException(
                    $"Error evaluating filter expression '{filterExpression}': {e.Message}", e, ctx);
            }
        }
        
        // Remove elements that didn't pass the filter
        foreach (var elementToRemove in elementsToRemove)
        {
            var parent = elementToRemove.Parent;
            if (parent is JArray parentArray)
            {
                parentArray.Remove(elementToRemove);
            }
        }
        
        if (elementsToRemove.Any())
        {
            ctx.LogInfo($"Removed {elementsToRemove.Count} element(s) that failed filter conditions");
        }
    }
    
    /// <summary>
    /// Gets the cardinality qualifier (if any) from the same qualifiers array.
    /// </summary>
    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.Parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }
}
