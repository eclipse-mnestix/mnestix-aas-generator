namespace MnestixCore.Dtos;

/// <summary>
/// Optional default thumbnail for the AAS asset information.
/// Matches the AAS V3 Resource schema (path required, contentType optional).
/// </summary>
public class DefaultThumbnail
{
    /// <summary>
    /// Path or URL to the thumbnail resource. Required when a thumbnail is provided.
    /// </summary>
    public string Path { get; set; } = null!;

    /// <summary>
    /// Optional content type (MIME type) of the thumbnail resource, e.g. "image/png".
    /// </summary>
    public string? ContentType { get; set; }
}
