using Newtonsoft.Json.Linq;

namespace MnestixCore.TemplateBuilder.Interfaces;

public interface IBlueprintProvider
{
    /// <summary>
    /// Gets all blueprints from the blueprint AAS.
    /// </summary>
    /// <returns>Json with all  blueprints.</returns>
    Task<JArray> GetAllBlueprintsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one blueprint from the blueprint AAS.
    /// </summary>
    /// <returns>Json with blueprint.</returns>
    /// <param name="blueprintId">Consumer-facing blueprint id. The default BaSyx-backed provider performs repository-path encoding internally.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<JObject> GetBlueprintAsync(string blueprintId, CancellationToken cancellationToken = default);
}