using Newtonsoft.Json.Linq;

namespace MnestixCore.Shared.Interfaces;

public interface ISubmodelHandler
{
    /// <summary>
    /// Retrieves submodel ids from Reference type.
    /// </summary>
    List<string> GetSubmodelsIdsFromSubmodelsRefs(JObject submodelsRefsFromRepository);
}