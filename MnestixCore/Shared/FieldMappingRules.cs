namespace MnestixCore.Shared;

public static class FieldMappingRules
{
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedFieldsByModelType =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["Property"] = new HashSet<string> { "value", "idShort", "displayName" },
            ["MultiLanguageProperty"] = new HashSet<string> { "value", "idShort", "displayName", "multiLanguage" },
            ["Blob"] = new HashSet<string> { "value", "idShort", "displayName" },
            ["File"] = new HashSet<string> { "value", "idShort", "displayName" },
            ["Entity"] = new HashSet<string> { "idShort", "displayName", "globalAssetId", "entityType" },
            ["RelationshipElement"] = new HashSet<string> { "idShort", "displayName", "first", "second" },
            ["AnnotatedRelationshipElement"] = new HashSet<string> { "idShort", "displayName", "first", "second" },
            ["SubmodelElementCollection"] = new HashSet<string> { "idShort", "displayName" },
            ["SubmodelElementList"] = new HashSet<string> { "idShort", "displayName" },
            ["ReferenceElement"] = new HashSet<string> { "idShort", "displayName" },
            ["Range"] = new HashSet<string> { "idShort", "displayName" },
        };

    public static readonly IReadOnlySet<string> AllAllowedFieldNames =
        new HashSet<string>(AllowedFieldsByModelType.Values.SelectMany(s => s));
}
