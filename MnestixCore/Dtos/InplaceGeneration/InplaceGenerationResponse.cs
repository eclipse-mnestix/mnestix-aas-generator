using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Dtos.InplaceGeneration;

/// <summary>
/// An AAS environment containing only the generated submodel. assetAdministrationShells and conceptDescriptions
/// are always empty - the caller is responsible for persisting the submodel and linking it to a shell.
/// </summary>
public class InplaceGenerationResponse
{
    [JsonProperty("assetAdministrationShells")]
    public IEnumerable<JObject> AssetAdministrationShells { get; init; } = Enumerable.Empty<JObject>();

    [JsonProperty("submodels")]
    public IEnumerable<JObject> Submodels { get; init; } = Enumerable.Empty<JObject>();

    [JsonProperty("conceptDescriptions")]
    public IEnumerable<JObject> ConceptDescriptions { get; init; } = Enumerable.Empty<JObject>();
}
