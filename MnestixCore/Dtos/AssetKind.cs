namespace MnestixCore.Dtos;

/// <summary>
/// AssetKind enum as defined in AAS specification.
/// Describes whether an AAS represents a Type (template), Instance (concrete asset), or is NotApplicable.
/// </summary>
public enum AssetKind
{
    Instance,
    Type,
    NotApplicable
}
