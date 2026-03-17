using static System.String;

namespace MnestixCore.Dtos.AppSettingsOptions;

public static class RequiredShellsOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.json
    /// </summary>
    public const string RequiredShellsSectionName = "RequiredShells";
}

/// <summary>
/// Represents the RequiredShells configuration in appsettings.json
/// HINT: must not be an abstract class
/// </summary>
public class RequiredShells
{
    /// <summary>
    /// The name of the AAS.
    /// There must be a folder with this name which holds a json file with the aas-json.
    /// </summary>
    public string Name { get; set; } = Empty;

    /// <summary>
    /// The AasId of the shell.
    /// There must be a json file in the folder with the <see cref="Name"/> which holds a json with this <see cref="Base64EncodedAasId"/> name.
    /// </summary>
    public string Base64EncodedAasId { get; set; } = Empty;

    /// <summary>
    /// Name of the thumbnail file for the AAS.
    /// There must be a file in the Files folder with a thumbnail image.
    /// </summary>
    public string AasThumbnailName { get; set; } = Empty;

    /// <summary>
    /// Default is false.
    /// Set to true, if the AAS and its submodels should not be overwritten on restart.
    /// Set to true for Configuration.
    /// </summary>
    public bool SkipIfAlreadyExists { get; set; } = false;

    /// <summary>
    /// The submodelIdShorts of the submodels to add to the AasId.
    /// There must be a json file in the "Submodels" folder of the 
    /// </summary>
    public List<string> SubmodelIdShorts { get; set; } = [];

    /// <summary>
    /// The files of the submodels to add to upload.
    /// There must be a file in the "Files" folder of the 
    /// </summary>
    public List<FileUpload> Files { get; set; } = [];
}

public class FileUpload
{
    public string FileName { get; set; } = Empty;
    public string IdShortPath { get; set; } = Empty;
    public string SubmodelIdBase64Encoded { get; set; } = Empty;
}