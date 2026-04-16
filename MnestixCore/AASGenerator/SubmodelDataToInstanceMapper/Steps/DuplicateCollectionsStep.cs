using System.Text.RegularExpressions;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Pipelines.Steps;

/// <summary>
/// Pipeline step that duplicates submodel collection elements according to the payload cardinality metadata.
/// </summary>
/// <remarks>
/// This step processes submodel collections by finding elements with CollectionMappingInfo qualifiers
/// and duplicating them based on the amount the corresponding element appears in the data payload. The duplication process
/// maintains proper naming conventions and mapping paths for each duplicated element.
/// </remarks>
public sealed class DuplicateCollectionsAasGeneratorPipelineStep : IPipelineStep<DataMappingContext>
{
    public Task<DataMappingContext> ExecuteAsync(DataMappingContext ctx)
    {
        ctx.Log($"Started DuplicateCollectionsStep");
        DuplicateCollectionElements(ctx);
        ctx.Log($"Finished DuplicateCollectionsStep");
        return Task.FromResult(ctx);
    }

    /// <summary>
    /// Selects JSON tokens from the data JSON using the specified mapping path.
    /// </summary>
    /// <param name="dataJson">The JSON token containing the data to search in.</param>
    /// <param name="mappingPath">The JSONPath expression used to select tokens.</param>
    /// <param name="isMandatory">Indicates whether the path selection is mandatory (affects error handling).</param>
    /// <param name="ctx">The data mapping context for error reporting and logging.</param>
    /// <returns>An enumerable of JSON tokens that match the specified path.</returns>
    /// <exception cref="SubmodelDataToInstanceMapperException">
    /// Thrown when the mapping path cannot be found in the data JSON or when JSON path evaluation fails.
    /// </exception>
    /// <remarks>
    /// This method forces evaluation of the enumerable to catch exceptions during JSON path evaluation.
    /// It provides comprehensive error handling for both missing paths and JSON parsing errors.
    /// </remarks>
    private static IEnumerable<JToken> SelectTokensFromDataJson(JToken dataJson, string mappingPath, bool isMandatory, DataMappingContext ctx)
    {
        try
        {
            IEnumerable<JToken> tokens = dataJson.SelectTokens(mappingPath, isMandatory) ?? throw new SubmodelDataToInstanceMapperException($"could not find {mappingPath} in data json", ctx);
            // Force evaluation of the enumerable to catch exceptions during JSON path evaluation
            return tokens.ToList();
        }
        catch (JsonException e)
        {
            throw new SubmodelDataToInstanceMapperException($"Error while trying to get data from mapping path {mappingPath}: ", e, ctx);
        }
    }
    private static JToken? GetCardinalityQualifier(JToken qualifier)
    {
        // qualifier.parent is the "qualifiers" array
        return qualifier.Parent?.SelectToken("[?(@.type=='SMT/Cardinality')]");
    }

    /// <summary>
    /// Duplicates collection elements based on CollectionMappingInfo qualifiers and data cardinality.
    /// </summary>
    /// <param name="ctx">The data mapping context containing submodel instance and payload data.</param>
    /// <exception cref="SubmodelDataToInstanceMapperException">
    /// Thrown when:
    /// - The matching value field of a qualifier object cannot be found
    /// - The parent of the element to be duplicated is not a SubmodelElementCollection or SubmodelElementList
    /// - Mapping info is null or invalid
    /// </exception>
    /// <remarks>
    /// This recursive method processes collection elements by:
    /// 1. Finding qualifiers with type 'SMT/CollectionMappingInfo'
    /// 2. Sorting qualifiers by the number of [*] patterns (collection depth) - starting with the shallowest
    /// 3. Duplicating elements based on the collection length in the data payload
    /// 4. Updating mapping paths and idShort values for each duplicated element
    /// 5. Removing processed qualifiers to avoid infinite loops
    /// 6. Recursively processing remaining qualifiers
    /// 
    /// The element to be duplicated is found by navigating up three levels from the qualifier
    /// (qualifier → qualifiers array → value object → element). The parent must be either a
    /// SubmodelElementCollection or SubmodelElementList to ensure proper collection structure.
    /// </remarks>
    private static void DuplicateCollectionElements(DataMappingContext ctx)
    {
        var submodelInstance = ctx.SubmodelInstance;
        var data = ctx.Data;

        var qualifiers = submodelInstance.SelectTokens("$..qualifiers[?(@.type=='SMT/CollectionMappingInfo')]");

        if (!qualifiers.Any())
        {
            ctx.Qualifier = new JObject();
            submodelInstance.SelectTokens("$..qualifiers[?(@.type=='_SMT/CollectionMappingInfo')]")
                .ToList()
                .ForEach(q => q["type"]?.Replace("SMT/CollectionMappingInfo"));
            return;
        }

        var sortedQualifiers = qualifiers
            .OrderBy(q => Regex.Matches(q["value"]?.Value<string>() ?? string.Empty, @"\[\*\]").Count)
            .ToList();

        ctx.Qualifier = sortedQualifiers[0];

        /// <remarks>
        /// Navigation hierarchy: The element that will be copied can be found by going up three levels from the qualifier:
        /// qualifier → qualifiers array → value object → element
        /// </remarks>
        var elementToBeDuplicated = ctx.Qualifier.Parent?.Parent?.Parent ?? throw new SubmodelDataToInstanceMapperException("could not find matching value field of a qualify object", ctx);

        /// <remarks>
        /// Structural validation: The parent of the element to be duplicated must be a SMC (SubmodelElementCollection) 
        /// a SML (SubmodelElementList) or an Entity to ensure proper collection structure
        /// </remarks>
        if (elementToBeDuplicated.Parent?.Parent?.Parent?["modelType"]?.Value<string>() is not ("SubmodelElementCollection" or "SubmodelElementList" or "Entity"))
            throw new SubmodelDataToInstanceMapperException("The parent of the element to be duplicated must be a SubmodelElementCollection, a SubmodelElementList or an Entity", ctx);

        /// <remarks>
        /// Input validation: The mapping path cannot be null as it's required for data selection and element duplication
        /// </remarks>
        var mappingPath = ctx.Qualifier["value"]?.Value<string>() ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);

        var isMandatory = GetCardinalityQualifier(ctx.Qualifier)?["value"]?.Value<string>()?.StartsWith("One") ?? false;
        var collectionLength = SelectTokensFromDataJson(data, mappingPath.Replace("[*]", "[0]").TrimEnd('[', '0', ']') + "[*]", isMandatory, ctx).Count();

        var nestingDepth = Regex.Matches(mappingPath, @"\[\*\]").Count;
        ctx.LogInfo($"Processing collection at path '{mappingPath}' (depth: {nestingDepth}, mandatory: {isMandatory}, elements: {collectionLength})");
        
        if (isMandatory && collectionLength == 0)
        {
            ctx.LogWarning($"Mandatory collection at path '{mappingPath}' has 0 elements in data");
        }

        var listIdentifier = mappingPath.EndsWith("[*]") ? mappingPath.Substring(0, mappingPath.Length - 3) : mappingPath;

        for (var i = 0; i < collectionLength; i++)
        {
            var newElement = elementToBeDuplicated.DeepClone();
            /// <remarks>
            /// Deletes the qualifier that triggered this duplication to avoid infinite loops
            /// </remarks>
            newElement.SelectTokens("$..qualifiers[?(@.type=='SMT/CollectionMappingInfo')]")
                .ToList()
                .ForEach(q =>
                {
                    if (q["value"]?.Value<string>() == mappingPath)
                    {
                        q.Remove();
                    }
                });

            var idShortToken = newElement["idShort"];
            if (idShortToken is JValue idVal && idVal.Type == JTokenType.String)
            {
                idVal.Value = $"{idVal.Value}_{i}";
            }

            var iteratedQualifiers = newElement
                    .SelectTokens("$..qualifiers[*]")
                    .Where(q =>
                    {
                        var t = q["type"]?.Value<string>();
                        if (t == null) return false;
                        var isMappingInfo = t == "SMT/MappingInfo" || t.StartsWith("SMT/MappingInfo/", StringComparison.Ordinal);
                        if (!isMappingInfo && t != "SMT/CollectionMappingInfo") return false;
                        var v = q["value"]?.Value<string>();
                        return v != null && v.Contains($"{listIdentifier}[*]", StringComparison.Ordinal);
                    })
                    .ToList();


            foreach (var iteratedQualifer in iteratedQualifiers)
            {
                var iteratedMappingPath = iteratedQualifer["value"]?.Value<string>()
                                          ?? throw new SubmodelDataToInstanceMapperException("Mapping Info cannot be null", ctx);
                iteratedMappingPath = iteratedMappingPath.Replace($"{listIdentifier}[*]", $"{listIdentifier}[{i}]");
                iteratedQualifer["value"] = iteratedMappingPath;
            }

            elementToBeDuplicated.Parent!.Add(newElement);
        }

        ctx.Qualifier["type"]?.Replace("_SMT/CollectionMappingInfo");

        elementToBeDuplicated.Remove();

        ctx.LogInfo($"Successfully duplicated {collectionLength} elements for collection with mapping path '{mappingPath}'");

        DuplicateCollectionElements(ctx);
    }
}
