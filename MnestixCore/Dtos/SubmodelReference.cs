using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MnestixCore.Dtos;

public record SubmodelReference(List<Key> Keys, string Type)
{
    /// <summary>
    /// Builds the camelCase JSON for a ModelReference pointing at the given submodel id,
    /// as expected by the AAS repository's submodel-refs and shell submodels array.
    /// </summary>
    /// <param name="submodelId">Submodel id (not encoded) to reference.</param>
    public static string ToJson(string submodelId)
    {
        var reference = new SubmodelReference(new List<Key> { new("Submodel", submodelId) }, "ModelReference");
        return JsonConvert.SerializeObject(reference, new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        });
    }
}
