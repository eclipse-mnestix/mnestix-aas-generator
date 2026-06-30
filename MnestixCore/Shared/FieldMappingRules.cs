namespace MnestixCore.Shared;

/// <summary>
/// Single source of truth for which SMT/MappingInfo field names are permitted on each AAS model type.
/// Used by the blueprint validator (save-time) and referenced by the generator pipeline (generation-time)
/// to enforce consistent field-to-model-type applicability rules.
/// </summary>
public static class FieldMappingRules
{
    /// <summary>
    /// Maps each supported AAS model type to the set of field names that may appear in
    /// <c>SMT/MappingInfo/{field}</c> qualifiers on elements of that type.
    /// Model types absent from this dictionary are unsupported for mapping and will be rejected.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFieldsByModelType =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["Property"] = new HashSet<string> { "value", "idShort", "displayName", "semanticId", "valueType" },
            ["MultiLanguageProperty"] = new HashSet<string> { "value", "idShort", "displayName", "multiLanguage", "semanticId" },
            ["Blob"] = new HashSet<string> { "value", "idShort", "displayName", "contentType", "valueType" },
            ["File"] = new HashSet<string> { "value", "idShort", "displayName", "contentType", "semanticId" },
            ["Entity"] = new HashSet<string> { "idShort", "displayName", "globalAssetId", "entityType" },
            ["RelationshipElement"] = new HashSet<string> { "idShort", "displayName", "first", "second" },
            ["AnnotatedRelationshipElement"] = new HashSet<string> { "idShort", "displayName", "first", "second" },
            ["SubmodelElementCollection"] = new HashSet<string> { "idShort", "displayName", "semanticId" },
            ["SubmodelElementList"] = new HashSet<string> { "idShort", "displayName", "semanticId" },
            ["ReferenceElement"] = new HashSet<string> { "idShort", "displayName" },
            ["Range"] = new HashSet<string> { "idShort", "displayName", "semanticId", "valueType" },
        };

    /// <summary>
    /// The union of all allowed field names across all model types.
    /// Any field name not in this set is considered unknown and will be rejected during validation.
    /// </summary>
    public static readonly IReadOnlySet<string> AllAllowedFieldNames =
        new HashSet<string>(AllowedFieldsByModelType.Values.SelectMany(s => s));
}
