namespace MnestixCore.Dtos;

/// <summary>
/// Represents administrative information for an AAS as defined in the AAS specification.
/// Contains version and revision information for the AAS.
/// </summary>
public class AdministrativeInformation
{
    /// <summary>
    /// Version of the AAS (e.g., "1", "2.0", "1.0.0").
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Optional revision of the AAS (e.g., "0", "1", "2").
    /// </summary>
    public string? Revision { get; set; }
}
