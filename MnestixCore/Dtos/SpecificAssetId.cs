using Newtonsoft.Json;

namespace MnestixCore.Dtos;

/// <summary>
/// Represents a SpecificAssetId as defined in the AAS specification.
/// A specific asset ID is a key-value pair that identifies an asset in a specific context.
/// </summary>
public class SpecificAssetId
{
    /// <summary>
    /// The name/key of the specific asset identifier (e.g., "SerialNumber", "PartNumber").
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public required string Name { get; set; }

    /// <summary>
    /// The value of the specific asset identifier.
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public required string Value { get; set; }
}
