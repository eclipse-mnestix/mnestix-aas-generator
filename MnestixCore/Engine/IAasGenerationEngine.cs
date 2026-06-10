using MnestixCore.Dtos;
using Newtonsoft.Json.Linq;

namespace Mnestix.AasGenerator;

/// <summary>
/// Pure, in-memory AAS generation engine. Takes blueprint and data documents plus
/// caller-supplied ids and produces AAS shell JSON and submodel objects. Performs no
/// HTTP, no repository access, no id generation, and no persistence.
/// </summary>
public interface IAasGenerationEngine
{
    /// <summary>
    /// Maps a single blueprint and data payload into a ready submodel instance.
    /// </summary>
    /// <param name="blueprint">The blueprint document to instantiate.</param>
    /// <param name="data">The data payload projected onto the blueprint.</param>
    /// <param name="language">Preferred language code for localized text, when applicable.</param>
    /// <param name="submodelId">The id assigned to the produced submodel.</param>
    /// <returns>The generated submodel instance.</returns>
    /// <exception cref="Exception">Thrown when blueprint validation or mapping fails.</exception>
    JObject MapSubmodel(JObject blueprint, JObject data, string? language, string submodelId);

    /// <summary>
    /// Maps multiple blueprints against the same data payload, capturing per-blueprint
    /// success/failure, logs, validation errors, and the produced submodel object.
    /// </summary>
    IReadOnlyList<SubmodelGenerationResult> GenerateSubmodels(
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false);

    /// <summary>
    /// Builds an AAS shell JSON document from already-assembled ids.
    /// </summary>
    string CreateAasShellJson(AasIds aasIds);

    /// <summary>
    /// Composite one-shot generation: builds the AAS shell JSON and maps every blueprint
    /// into a submodel, returning the whole bundle. The primary
    /// "data + blueprints in → AAS shell JSON + submodel objects out" entry point.
    /// Produces objects only; performs no persistence.
    /// </summary>
    AasGenerationResult GenerateAas(
        AasIds aasIds,
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false);
}
