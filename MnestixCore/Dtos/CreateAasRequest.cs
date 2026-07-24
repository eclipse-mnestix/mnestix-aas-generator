using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos;

/// <summary>
/// Request body for creating an AAS with optional submodels
/// </summary>
public class CreateAasRequest
{
    /// <summary>
    /// The language that new MultiLanguage Properties should be given. An example would be 'de'.
    /// Optional - only required if BlueprintsIds and Data are provided.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The DataJson that will be used to populate the blueprint with data based on the mapping info in the blueprint.
    /// Optional - only required if BlueprintsIds is provided.
    /// </summary>
    public JObject? Data { get; set; }

    /// <summary>
    /// A list of BlueprintsIds (not encoded in base64) to create submodels for the new AAS.
    /// Optional - if not provided, an empty AAS will be created.
    /// </summary>
    public IEnumerable<string>? BlueprintsIds { get; set; }
    
    /// <summary>
    /// Whether to include debug information (logs) in the response.
    /// Optional - defaults to false.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Optional globalAssetId to use instead of generating one.
    /// If provided, this value is used directly as the globalAssetId of the AAS.
    /// </summary>
    public string? GlobalAssetId { get; set; }

    /// <summary>
    /// Optional default thumbnail for the AAS asset information.
    /// Matches the AAS V3 Resource schema (path required, contentType optional).
    /// </summary>
    public DefaultThumbnail? DefaultThumbnail { get; set; }

    /// <summary>
    /// Optional AssetKind for the AAS asset information.
    /// Specifies whether the AAS represents an Instance (concrete asset), Type (template), or NotApplicable.
    /// Defaults to Instance if not provided.
    /// </summary>
    public AssetKind? AssetKind { get; set; }

    /// <summary>
    /// Optional extensions as key-value pairs.
    /// These will be added to the AAS root level as additional metadata.
    /// </summary>
    public Dictionary<string, string>? Extensions { get; set; }

    /// <summary>
    /// Optional specific asset identifiers to add to the asset information.
    /// These identifiers are used to identify the asset in specific contexts (e.g., serial numbers, part numbers).
    /// If provided, these will be added to the default assetIdShort identifier.
    /// </summary>
    public List<SpecificAssetId>? SpecificAssetIds { get; set; }

    /// <summary>
    /// Optional administrative information for the AAS.
    /// Contains version and revision information at the AAS root level.
    /// </summary>
    public AdministrativeInformation? Administration { get; set; }

    /// <summary>
    /// Optional derivedFrom reference indicating the parent AAS from which this AAS was derived.
    /// Accepts the parent AAS ID as a string, which will be converted to the proper AAS Metamodel v3.0 reference structure.
    /// Used for navigating product family hierarchies and inheritance relationships.
    /// </summary>
    public string? DerivedFrom { get; set; }

    /// <summary>
    /// Optional list of existing submodel IDs to link to the new AAS.
    /// These are plain (not base64-encoded) submodel IDs. Existence is not validated;
    /// an ID that is not in the repository yields a dangling reference on the created shell.
    /// Can be combined with BlueprintsIds to both generate new submodels and link existing ones.
    /// </summary>
    public IEnumerable<string>? SubmodelIds { get; set; }
}
