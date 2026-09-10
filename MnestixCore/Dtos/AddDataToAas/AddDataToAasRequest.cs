using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos.AddDataToAas;

public class AddDataToAasRequest
{
    /// <summary>
    /// the language that new MultiLanguage Properties should be given. An example would be 'de'.
    /// Not required when using MnestixAASGenerator/MappingInfo/multiLanguage qualifiers (language codes come from the data).
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The DataJson that will be used to populate the blueprint with data based on the mapping info in the blueprint.
    /// </summary>
    [Required]
    public JObject Data { get; set; } = null!;

    /// <summary>
    /// A list of BlueprintsIds (not encoded in base64)
    /// </summary>
    [Required]
    public IEnumerable<string> BlueprintsIds { get; set; } = [];
    
    /// <summary>
    /// Deprecated: Use BlueprintsIds instead
    /// </summary>
    [JsonProperty("CustomTemplateIds")]
    [Obsolete("Use BlueprintsIds instead")]
    public IEnumerable<string> CustomTemplateIds
    {
        get => BlueprintsIds;
        set => BlueprintsIds = value;
    }

    /// <summary>
    /// Whether to include debug information (logs) in the response.
    /// </summary>
    public bool Debug { get; set; } = false;
}