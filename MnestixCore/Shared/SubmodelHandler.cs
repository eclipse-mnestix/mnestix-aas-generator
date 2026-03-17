using Microsoft.Extensions.Logging;
using MnestixCore.Shared.Interfaces;
using Newtonsoft.Json.Linq;

namespace MnestixCore.Shared;

public class SubmodelHandler : ISubmodelHandler
{
    private readonly ILogger<SubmodelHandler> _logger;

    public SubmodelHandler(ILogger<SubmodelHandler> logger)
    {
        _logger = logger;
    }

    public List<string> GetSubmodelsIdsFromSubmodelsRefs(JObject submodelsRefsFromRepository)
    {
        var results = submodelsRefsFromRepository["result"] ?? throw new ArgumentNullException(nameof(GetSubmodelsIdsFromSubmodelsRefs));
        var submodelIds = new List<string>();

        foreach (var reference in results)
        {
            var submodelId = reference.SelectToken("keys[0]")?.SelectToken("value")?.ToString();
            _logger.LogDebug("Submodel reference with id: {submodelId}", submodelId);
            if (!string.IsNullOrEmpty(submodelId)) submodelIds.Add(submodelId);
        }

        return submodelIds;
    }
}
