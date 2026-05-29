using Jsonata.Net.Native;
using MnestixCore.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder;

public sealed class BlueprintValidator : IBlueprintValidator
{
    private const string MappingInfoPrefix = "SMT/MappingInfo";
    private const string FilterMappingInfoType = "SMT/FilterMappingInfo";
    private const string CollectionMappingInfoType = "SMT/CollectionMappingInfo";
    private const string CardinalityType = "SMT/Cardinality";

    private static readonly HashSet<string> ValidCardinalities = new()
    {
        "One", "ZeroToOne", "OneToMany", "ZeroToMany"
    };

    private static readonly HashSet<string> ValidCollectionParentModelTypes = new()
    {
        "SubmodelElementCollection", "SubmodelElementList", "Entity"
    };

    public IReadOnlyList<BlueprintValidationError> Validate(JObject blueprint)
    {
        var errors = new List<BlueprintValidationError>();
        var allQualifiers = blueprint.SelectTokens("$..qualifiers[*]").ToList();

        // Group MappingInfo qualifiers by parent element for cross-qualifier checks
        var mappingInfoByElement = new Dictionary<JToken, List<(JToken Qualifier, string FieldName)>>(
            ReferenceEqualityComparer.Instance);

        foreach (var qualifier in allQualifiers)
        {
            var type = qualifier["type"]?.Value<string>();
            if (string.IsNullOrEmpty(type)) continue;

            var path = BuildPath(qualifier);

            if (type == MappingInfoPrefix || type.StartsWith(MappingInfoPrefix + "/", StringComparison.Ordinal))
            {
                ValidateMappingInfoQualifier(qualifier, type, path, errors, mappingInfoByElement);
            }
            else if (type == FilterMappingInfoType)
            {
                ValidateFilterQualifier(qualifier, path, errors);
            }
            else if (type == CollectionMappingInfoType)
            {
                ValidateCollectionQualifier(qualifier, path, errors);
            }
            else if (type == CardinalityType)
            {
                ValidateCardinalityQualifier(qualifier, path, errors);
            }
        }

        // Cross-element checks for MappingInfo
        ValidateDuplicatesAndConflicts(mappingInfoByElement, errors);

        return errors;
    }

    private static void ValidateMappingInfoQualifier(
        JToken qualifier,
        string type,
        string path,
        List<BlueprintValidationError> errors,
        Dictionary<JToken, List<(JToken Qualifier, string FieldName)>> mappingInfoByElement)
    {
        var segments = type.Split('/');

        // Rule 1: InvalidQualifierSegmentCount
        if (segments.Length > 3)
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidQualifierSegmentCount,
                path,
                $"Qualifier type '{type}' has {segments.Length} segments; expected at most 3."));
            return;
        }

        // Resolve field name: bare "SMT/MappingInfo" → "value"
        var fieldName = segments.Length == 3 ? segments[2] : "value";

        // Rule 2: EmptyMappingExpression
        var expression = qualifier["value"]?.Value<string>();
        if (string.IsNullOrEmpty(expression))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.EmptyMappingExpression,
                path,
                "Mapping qualifier value is empty or missing."));
            return;
        }

        // Rule 3: UnknownFieldName
        if (!FieldMappingRules.AllAllowedFieldNames.Contains(fieldName))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.UnknownFieldName,
                path,
                $"Field '{fieldName}' is not a recognized mapping field. Allowed: {string.Join(", ", FieldMappingRules.AllAllowedFieldNames)}."));
            return;
        }

        // Determine the element and its modelType
        var element = GetParentElement(qualifier);
        var modelType = element?["modelType"]?.Value<string>();

        if (modelType != null)
        {
            // Rule 5: UnsupportedModelType
            if (!FieldMappingRules.AllowedFieldsByModelType.ContainsKey(modelType))
            {
                errors.Add(new BlueprintValidationError(
                    BlueprintValidationRule.UnsupportedModelType,
                    path,
                    $"Model type '{modelType}' is not supported for mapping qualifiers."));
                return;
            }

            // Rule 4: FieldNotApplicableToModelType
            if (!FieldMappingRules.AllowedFieldsByModelType[modelType].Contains(fieldName))
            {
                errors.Add(new BlueprintValidationError(
                    BlueprintValidationRule.FieldNotApplicableToModelType,
                    path,
                    $"Field '{fieldName}' is not valid on model type '{modelType}'. Allowed fields: {string.Join(", ", FieldMappingRules.AllowedFieldsByModelType[modelType])}."));
                return;
            }
        }

        // Rule 8: InvalidJsonataSyntax
        if (!TryParseJsonata(expression, out var parseError))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidJsonataSyntax,
                path,
                $"Invalid JSONata syntax: {parseError}"));
            return;
        }

        // Track for duplicate/conflict detection
        if (element != null)
        {
            if (!mappingInfoByElement.ContainsKey(element))
                mappingInfoByElement[element] = new List<(JToken, string)>();
            mappingInfoByElement[element].Add((qualifier, fieldName));
        }
    }

    private static void ValidateFilterQualifier(
        JToken qualifier,
        string path,
        List<BlueprintValidationError> errors)
    {
        var expression = qualifier["value"]?.Value<string>();

        // Rule 9: EmptyFilterExpression
        if (string.IsNullOrEmpty(expression))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.EmptyFilterExpression,
                path,
                "Filter qualifier value is empty or missing."));
            return;
        }

        // Rule 10: InvalidFilterJsonataSyntax
        if (!TryParseJsonata(expression, out var parseError))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidFilterJsonataSyntax,
                path,
                $"Invalid JSONata syntax in filter expression: {parseError}"));
        }
    }

    private static void ValidateCollectionQualifier(
        JToken qualifier,
        string path,
        List<BlueprintValidationError> errors)
    {
        var jsonPath = qualifier["value"]?.Value<string>();

        // Rule 11: EmptyCollectionPath
        if (string.IsNullOrEmpty(jsonPath))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.EmptyCollectionPath,
                path,
                "Collection mapping qualifier value is empty or missing."));
            return;
        }

        // Rule 13: CollectionPathMissingWildcard
        if (!jsonPath.EndsWith("[*]"))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.CollectionPathMissingWildcard,
                path,
                $"Collection mapping path '{jsonPath}' must end with '[*]'."));
            return;
        }

        // Rule 12: InvalidCollectionJsonPath
        if (!TryParseJsonPath(jsonPath, out var parseError))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidCollectionJsonPath,
                path,
                $"Invalid JSONPath syntax: {parseError}"));
            return;
        }

        // Rule 14: InvalidCollectionParentModelType
        var element = GetParentElement(qualifier);
        var parentElement = GetContainingElement(element);
        var parentModelType = parentElement?["modelType"]?.Value<string>();

        if (parentModelType == null || !ValidCollectionParentModelTypes.Contains(parentModelType))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidCollectionParentModelType,
                path,
                $"Collection mapping requires the parent element to be SubmodelElementCollection, SubmodelElementList, or Entity, but found '{parentModelType ?? "Submodel (top-level)"}'."));
        }
    }

    private static void ValidateCardinalityQualifier(
        JToken qualifier,
        string path,
        List<BlueprintValidationError> errors)
    {
        var value = qualifier["value"]?.Value<string>();

        // Rule 15: InvalidCardinalityValue
        if (string.IsNullOrEmpty(value) || !ValidCardinalities.Contains(value))
        {
            errors.Add(new BlueprintValidationError(
                BlueprintValidationRule.InvalidCardinalityValue,
                path,
                $"Cardinality value '{value}' is invalid. Allowed values: {string.Join(", ", ValidCardinalities)}."));
        }
    }

    private static void ValidateDuplicatesAndConflicts(
        Dictionary<JToken, List<(JToken Qualifier, string FieldName)>> mappingInfoByElement,
        List<BlueprintValidationError> errors)
    {
        foreach (var (element, mappings) in mappingInfoByElement)
        {
            var fieldCounts = mappings
                .GroupBy(m => m.FieldName)
                .Where(g => g.Count() > 1)
                .ToList();

            // Rule 6: DuplicateMappingField
            foreach (var duplicate in fieldCounts)
            {
                var path = BuildPath(duplicate.Skip(1).First().Qualifier);
                errors.Add(new BlueprintValidationError(
                    BlueprintValidationRule.DuplicateMappingField,
                    path,
                    $"Duplicate mapping for field '{duplicate.Key}' on the same element."));
            }

            // Rule 7: MlpValueAndMultiLanguageConflict
            var modelType = element["modelType"]?.Value<string>();
            if (modelType == "MultiLanguageProperty")
            {
                var fieldNames = mappings.Select(m => m.FieldName).ToHashSet();
                if (fieldNames.Contains("value") && fieldNames.Contains("multiLanguage"))
                {
                    var conflictQualifier = mappings.First(m => m.FieldName == "multiLanguage").Qualifier;
                    var path = BuildPath(conflictQualifier);
                    errors.Add(new BlueprintValidationError(
                        BlueprintValidationRule.MlpValueAndMultiLanguageConflict,
                        path,
                        "MultiLanguageProperty cannot have both 'value' and 'multiLanguage' mappings. Use one or the other."));
                }
            }
        }
    }

    private static bool TryParseJsonata(string expression, out string? error)
    {
        try
        {
            _ = new JsonataQuery(expression);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseJsonPath(string jsonPath, out string? error)
    {
        try
        {
            new JObject().SelectTokens(jsonPath).ToList();
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Navigates from a qualifier to its parent SME element.
    /// Structure: element → qualifiers (array) → qualifier (item)
    /// </summary>
    private static JToken? GetParentElement(JToken qualifier)
    {
        // qualifier → JArray (qualifiers) → JProperty("qualifiers") → JObject (element)
        return qualifier.Parent?.Parent?.Parent;
    }

    /// <summary>
    /// Navigates from an element to the element that contains it.
    /// Structure: parent element → value/statements/submodelElements (array) → element
    /// </summary>
    private static JToken? GetContainingElement(JToken? element)
    {
        if (element == null) return null;
        // element → JArray (value/statements/submodelElements) → JProperty → JObject (parent element)
        var containingArray = element.Parent;
        var property = containingArray?.Parent;
        var parentElement = property?.Parent;
        return parentElement;
    }

    /// <summary>
    /// Builds a human-readable path from qualifier to root using idShort breadcrumbs.
    /// Falls back to array index if idShort is missing.
    /// </summary>
    private static string BuildPath(JToken qualifier)
    {
        var parts = new List<string>();
        var current = GetParentElement(qualifier);

        while (current is JObject obj)
        {
            var idShort = obj["idShort"]?.Value<string>();
            if (!string.IsNullOrEmpty(idShort))
            {
                parts.Add(idShort);
            }
            else
            {
                // Fallback: use array index
                var parent = current.Parent;
                if (parent is JArray arr)
                {
                    var index = arr.IndexOf(current);
                    parts.Add($"[{index}]");
                }
            }

            current = GetContainingElement(current);
        }

        parts.Reverse();
        return parts.Count > 0 ? string.Join(" > ", parts) : "(root)";
    }
}
