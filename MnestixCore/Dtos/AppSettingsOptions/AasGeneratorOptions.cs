namespace MnestixCore.Dtos.AppSettingsOptions;

/// <summary>
/// Holds the resource limits for the AAS generator.
/// </summary>
public class AasGeneratorOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string AasGenerator = "AasGenerator";

    /// <summary>
    /// Maximum payload size accepted per ingest request. Governs both the number of
    /// blueprint IDs per request and the number of elements a collection may be
    /// duplicated to. Larger payloads are rejected instead of exhausting the process.
    /// </summary>
    public int MaxPayloadLimit { get; set; } = 200;

    /// <summary>
    /// Wall-clock timeout in seconds for evaluating a single JSONata expression during
    /// data mapping. Evaluations exceeding it are abandoned and fail the mapping.
    /// </summary>
    public int JsonataEvaluationTimeoutSeconds { get; set; } = 2;
}
