using Newtonsoft.Json.Linq;

namespace MnestixCore.AasGenerator.Interfaces;

public interface IAasGenerator
{
    /// <summary>
    /// supplies data to the shell with given aasId. It will fetch templates with given ids, populate them with data according to their mapping info
    /// using given data json as data source. After that it will store the templates under their idShort in the shell.
    /// See <see cref="IDataMapper"/> for more Information about mapping.
    /// </summary>
    /// <param name="base64EncodedAasId">the shell where the submodel will be added to</param>
    /// <param name="blueprintsIds">the blueprints where data will be mapped to according to their mapping info.
    /// After that, they will be added the shell under their idShort
    /// </param>
    /// <param name="data">the json where the data will be looked up from the templates according to mapping info</param>
    /// <param name="language">the language that will be used when encountering a multi language property</param>
    /// <param name="debug">whether to include debug logs in the results</param>
    /// <param name="preamble">optional context message logged as the first entry per blueprint (e.g. caller info)</param>
    /// <returns>a list of results for each template</returns>
    Task<IEnumerable<AasGeneratorResult>> AddDataToAasAsync(string base64EncodedAasId, IEnumerable<string> blueprintsIds, JObject data, string? language, bool debug = false, string? preamble = null);

    /// <summary>
    /// Builds and validates a submodel instance in memory from the given blueprint. Performs no repository writes.
    /// </summary>
    /// <param name="blueprintId">Blueprint id (not base64 encoded) describing the submodel to build.</param>
    /// <param name="data">Payload that provides the values projected onto the blueprint.</param>
    /// <param name="language">Preferred language code for localized text.</param>
    /// <param name="debug">Whether to include debug logs in the result.</param>
    /// <param name="preamble">Optional context message logged as the first entry.</param>
    /// <returns>A <see cref="BuiltSubmodel"/> with the in-memory instance and per-blueprint result.</returns>
    Task<BuiltSubmodel> BuildSubmodelAsync(string blueprintId, JObject data, string? language, bool debug = false, string? preamble = null);

    /// <summary>
    /// Posts an already-built submodel instance body to the repository. Does not create a shell reference.
    /// </summary>
    /// <param name="submodelInstance">The submodel instance to persist.</param>
    /// <returns>The id of the persisted submodel.</returns>
    Task<string> PostSubmodelAsync(JObject submodelInstance);
}