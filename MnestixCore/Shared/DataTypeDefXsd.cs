namespace MnestixCore.Shared;

/// <summary>
/// Single source of truth for the AAS <c>DataTypeDefXsd</c> value types.
/// Used to validate mapped <c>valueType</c> values and to normalize valueType casing.
/// </summary>
public static class DataTypeDefXsd
{
    /// <summary>
    /// The canonical set of all AAS-permitted XSD value types (canonical casing).
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "xs:anyURI",
        "xs:base64Binary",
        "xs:boolean",
        "xs:byte",
        "xs:date",
        "xs:dateTime",
        "xs:decimal",
        "xs:double",
        "xs:duration",
        "xs:float",
        "xs:gDay",
        "xs:gMonth",
        "xs:gMonthDay",
        "xs:gYear",
        "xs:gYearMonth",
        "xs:hexBinary",
        "xs:int",
        "xs:integer",
        "xs:long",
        "xs:negativeInteger",
        "xs:nonNegativeInteger",
        "xs:nonPositiveInteger",
        "xs:positiveInteger",
        "xs:short",
        "xs:string",
        "xs:time",
        "xs:unsignedByte",
        "xs:unsignedInt",
        "xs:unsignedLong",
        "xs:unsignedShort",
    };

    /// <summary>
    /// Case-insensitive lookup from any-casing value type to its canonical form.
    /// Centralizes the case policy so validation and normalization agree.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> CanonicalByValue =
        All.ToDictionary(v => v, v => v, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if <paramref name="valueType"/> is a recognized AAS DataTypeDefXsd value.
    /// The comparison is case-insensitive; use <see cref="TryGetCanonical"/> to obtain the canonical casing.
    /// </summary>
    public static bool IsValid(string? valueType) => valueType != null && CanonicalByValue.ContainsKey(valueType);

    /// <summary>
    /// Attempts to resolve <paramref name="valueType"/> (in any casing) to its canonical AAS DataTypeDefXsd form.
    /// Returns false when the value is not a recognized XSD value type.
    /// </summary>
    public static bool TryGetCanonical(string? valueType, out string canonical)
    {
        if (valueType != null && CanonicalByValue.TryGetValue(valueType, out var found))
        {
            canonical = found;
            return true;
        }

        canonical = string.Empty;
        return false;
    }
}
