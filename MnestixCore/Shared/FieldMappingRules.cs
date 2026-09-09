using System.Collections.Frozen;

namespace MnestixCore.Shared;

/// <summary>
/// Single source of truth for which MnestixAASGenerator/MappingInfo field names are permitted on each AAS model type,
/// and the per-field cardinality metadata derived from the AAS metamodel (IDTA-01001 v3.1.2).
/// Used by the blueprint validator (save-time) and the generator pipeline (generation-time).
/// </summary>
public static class FieldMappingRules
{
    /// <summary>
    /// Maps each supported AAS model type to its set of mappable fields with per-field cardinality metadata.
    /// Model types absent from this dictionary are unsupported for mapping and will be rejected.
    /// </summary>
    public static readonly FrozenDictionary<string, ModelTypeMapping> AllowedFields =
        new Dictionary<string, ModelTypeMapping>
        {
            ["Property"] = new([
                new FieldSpec("value"),
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("semanticId",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("valueType",   FieldSpec.Cardinality.AlwaysMandatory),
            ]),
            ["MultiLanguageProperty"] = new([
                new FieldSpec("value"),
                new FieldSpec("idShort"),
                new FieldSpec("displayName",   FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("multiLanguage"),
                new FieldSpec("semanticId",    FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["Blob"] = new([
                new FieldSpec("value"),
                new FieldSpec("idShort"),
                new FieldSpec("displayName",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("contentType",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("valueType"),
            ]),
            ["File"] = new([
                new FieldSpec("value"),
                new FieldSpec("idShort"),
                new FieldSpec("displayName",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("contentType",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("semanticId",   FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["Entity"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName",   FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("globalAssetId", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("entityType",    FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["RelationshipElement"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("first"),
                new FieldSpec("second"),
            ]),
            ["AnnotatedRelationshipElement"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("first"),
                new FieldSpec("second"),
            ]),
            ["SubmodelElementCollection"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("semanticId",  FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["SubmodelElementList"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("semanticId",  FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["ReferenceElement"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
            ]),
            ["Range"] = new([
                new FieldSpec("idShort"),
                new FieldSpec("displayName", FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("semanticId",  FieldSpec.Cardinality.AlwaysOptional),
                new FieldSpec("valueType",   FieldSpec.Cardinality.AlwaysMandatory),
            ]),
        }.ToFrozenDictionary();

    /// <summary>
    /// The union of all allowed field names across all model types.
    /// Any field name not in this set is considered unknown and will be rejected during validation.
    /// </summary>
    public static readonly IReadOnlySet<string> AllAllowedFieldNames =
        new HashSet<string>(AllowedFields.Values.SelectMany(m => m.FieldNames));
}
