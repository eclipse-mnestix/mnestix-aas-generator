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
    /// Returns true if <paramref name="valueType"/> is a recognized AAS DataTypeDefXsd value (canonical casing).
    /// </summary>
    public static bool IsValid(string? valueType) => valueType != null && All.Contains(valueType);
}
