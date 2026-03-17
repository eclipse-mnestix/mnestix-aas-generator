using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder.Interfaces;

public interface IBlueprintProvider
{
    /// <summary>
    /// Gets all blueprints from the blueprint AAS.
    /// </summary>
    /// <returns>Json with all  blueprints.</returns>
    Task<JArray> GetAllBlueprintsAsync();

    /// <summary>
    /// Gets one blueprint from the blueprint AAS.
    /// </summary>
    /// <returns>Json with blueprint.</returns>
    /// <param name="submodelIdShort">SubmodelIdShort to identify the submodel within the AAS</param>
    Task<JObject> GetBlueprintAsync(string submodelIdShort);
}