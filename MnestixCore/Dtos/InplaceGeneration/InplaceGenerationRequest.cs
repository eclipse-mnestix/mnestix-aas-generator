using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos.InplaceGeneration;

/// <summary>
/// Request body for building submodels from blueprints without persisting them to the repository.
/// </summary>
public class InplaceGenerationRequest
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
    /// The BlueprintId (not encoded in base64) to build the submodel from.
    /// </summary>
    [Required]
    public string BlueprintId { get; set; } = null!;

    /// <summary>
    /// Whether to include debug information (logs) in the response.
    /// </summary>
    public bool Debug { get; set; } = false;
}
