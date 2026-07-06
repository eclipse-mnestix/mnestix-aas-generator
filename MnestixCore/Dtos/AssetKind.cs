using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MnestixCore.Dtos;

/// <summary>
/// AssetKind enum as defined in AAS specification.
/// Describes whether an AAS represents a Type (template), Instance (concrete asset), or is NotApplicable.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AssetKind
{
    Instance,
    Type,
    NotApplicable
}
