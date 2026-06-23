namespace MnestixCore.Dtos;

/// <summary>
/// Optional configuration parameters for AAS creation.
/// Groups all optional metadata fields to allow future extension without breaking method signatures.
/// </summary>
public class AasCreationOptions
{
    /// <summary>
    /// AssetKind for the AAS (Instance, Type, or NotApplicable). Defaults to Instance.
    /// </summary>
    public AssetKind AssetKind { get; set; } = AssetKind.Instance;

    /// <summary>
    /// Optional extensions as key-value pairs to add to the AAS root level.
    /// </summary>
    public Dictionary<string, string>? Extensions { get; set; }

    /// <summary>
    /// Optional specific asset identifiers to add to the asset information.
    /// These identifiers are used to identify the asset in specific contexts (e.g., serial numbers, part numbers).
    /// </summary>
    public List<SpecificAssetId>? SpecificAssetIds { get; set; }

    /// <summary>
    /// Optional administrative information (version and revision) for the AAS.
    /// </summary>
    public AdministrativeInformation? Administration { get; set; }

    /// <summary>
    /// Optional default thumbnail for the AAS asset information.
    /// </summary>
    public DefaultThumbnail? DefaultThumbnail { get; set; }

    /// <summary>
    /// Optional derivedFrom reference indicating the parent AAS from which this AAS was derived.
    /// </summary>
    public string? DerivedFrom { get; set; }
}
