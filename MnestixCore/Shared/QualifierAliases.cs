namespace MnestixCore.Shared;

/// <summary>
/// Single source of truth for renaming the Mnestix-owned mapping qualifiers from the legacy
/// "SMT/" prefix to the "MnestixAASGenerator/" prefix (MNE-428).
///
/// Only the three Mnestix-owned qualifiers are in the map. "SMT/Cardinality" is an IDTA
/// SMT-spec standard qualifier (not Mnestix-owned) and is intentionally excluded, so it is
/// never renamed. Any type not covered by the map is returned unchanged, which keeps custom
/// or unknown qualifiers untouched.
/// </summary>
public static class QualifierAliases
{
    public const string MappingInfoPrefix = "MnestixAASGenerator/MappingInfo";
    public const string CollectionMappingInfoType = "MnestixAASGenerator/CollectionMappingInfo";
    public const string FilterMappingInfoType = "MnestixAASGenerator/FilterMappingInfo";

    private const string LegacyMappingInfoPrefix = "SMT/MappingInfo";

    /// <summary>
    /// Exact-match legacy -> canonical entries. The MappingInfo "/&lt;field&gt;" suffix form is
    /// handled separately in <see cref="Canonicalize"/> via prefix matching.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Map { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [LegacyMappingInfoPrefix] = MappingInfoPrefix,
        ["SMT/CollectionMappingInfo"] = CollectionMappingInfoType,
        ["SMT/FilterMappingInfo"] = FilterMappingInfoType,
    };

    /// <summary>
    /// Returns the canonical (new-prefix) type for a given qualifier type.
    /// Exact legacy match -> mapped value; legacy "SMT/MappingInfo/&lt;field&gt;" -> new prefix + suffix;
    /// anything else (already-canonical, SMT/Cardinality, custom qualifiers) -> returned unchanged.
    /// </summary>
    public static string Canonicalize(string type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return type;
        }

        if (Map.TryGetValue(type, out var mapped))
        {
            return mapped;
        }

        // Legacy MappingInfo with a field suffix, e.g. "SMT/MappingInfo/value".
        if (type.StartsWith(LegacyMappingInfoPrefix + "/", StringComparison.Ordinal))
        {
            return MappingInfoPrefix + type.Substring(LegacyMappingInfoPrefix.Length);
        }

        return type;
    }
}
