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
}
