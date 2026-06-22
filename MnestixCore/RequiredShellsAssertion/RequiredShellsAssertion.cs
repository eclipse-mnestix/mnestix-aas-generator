using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.RequiredShellsAssertion.Interfaces;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;

namespace MnestixCore.RequiredShellsAssertion;

public class RequiredShellsAssertion(ILogger<RequiredShellsAssertion> logger,
        IOptions<List<RequiredShells>> requiredShellsOptions,
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IOptions<ConfigurationOptions> configurationOptions)
    : IRequiredShellsAssertion
{
    private const string EmbeddedResourceShellPath = "RequiredShellsAssertion.RequiredShellsResources.";

    private readonly RepoProxyOptions _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));
    private readonly List<RequiredShells> _requiredShellsOptions = requiredShellsOptions.Value ?? throw new ArgumentNullException(nameof(requiredShellsOptions));
    private readonly string _submodelTemplatesApiUrl = configurationOptions.Value?.SubmodelTemplatesApiUrl ?? string.Empty;
    private readonly string _submodelBlueprintsApiUrl = configurationOptions.Value?.SubmodelBlueprintsApiUrl ?? string.Empty;
    private static readonly HashSet<string> TemplatesBlacklist = new(StringComparer.Ordinal)
    {
        "DefaultTemplate"
    };

    private static readonly HashSet<string> BlueprintsBlacklist = new(StringComparer.Ordinal)
    {
        "CustomTemplate"
    };

    /// <summary>
    /// Assures that all required AAS are stored in the repository.
    /// </summary>
    /// <remarks>
    /// If any required AAS is missing in the repository, it will be added.
    /// If any submodel of a required AAS is missing in the repository, it will be added.
    /// Existing submodels of a required AAS will be overwritten.
    /// Setting 'SkipIfAlreadyExists' of an AAS to true prevents existing submodels to be overwritten.
    /// </remarks>
    public async Task AssertRequiredShellsAsync()
    {
        logger.LogInformation("Assert existence of required AAS");

        var successCounter = 0;
        var required = _requiredShellsOptions.Count;

        foreach (var requiredShell in _requiredShellsOptions)
        {
            logger.LogDebug("Checking AAS {AasId} (Name='{Name}')",
                requiredShell.Base64EncodedAasId, requiredShell.Name);

            if (VerifyBlueprintsConfiguration(requiredShell.Name))
            {
                logger.LogInformation("Skip checking required AAS {AasId} (Name='{Name}') due to configured SubmodelBlueprintsApiUrl",
                    requiredShell.Base64EncodedAasId, requiredShell.Name);
                required--;
                continue;
            }
            if (VerifyTemplatesConfiguration(requiredShell.Name))
            {
                logger.LogInformation("Skip checking required AAS {AasId} (Name='{Name}') due to configured SubmodelTemplatesApiUrl",
                    requiredShell.Base64EncodedAasId, requiredShell.Name);
                required--;
                continue;
            }

            try
            {
                var isAasIdExisting = await IsAasIdAlreadyExisting(requiredShell.Base64EncodedAasId);

                if (isAasIdExisting)
                {
                    logger.LogInformation("Existing AAS {AasId} (Name='{Name}')",
                        requiredShell.Base64EncodedAasId, requiredShell.Name);
                }
                else
                {
                    logger.LogWarning("Missing AAS {AasId} (Name='{Name}')",
                        requiredShell.Base64EncodedAasId, requiredShell.Name);

                    await AddAasToRepo(requiredShell);

                    if (requiredShell.AasThumbnailName != string.Empty)
                    {
                        await UploadThumbnailToAas(requiredShell);
                    }
                }

                await AddOrUpdateSubmodelsForAas(requiredShell);

                // only increase if the AAS and all of its submodels is contained in the repository
                successCounter++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while checking AAS {AasId} (Name='{Name}'): {Error}",
                    requiredShell.Base64EncodedAasId, requiredShell.Name, e.Message);
            }
        }

        logger.LogInformation(@"Finished assertion of required AAS.
{Success} of {Expected} required shells available.",
            successCounter, required);
    }

    private async Task AddAasToRepo(RequiredShells requiredShell)
    {
        var pathToAas = EmbeddedResourceShellPath + requiredShell.Name + "."
                        + requiredShell.Base64EncodedAasId + ".json";
        var aasToPut = EmbeddedResourceProvider.GetEmbeddedResourceContent(pathToAas);

        if (string.IsNullOrEmpty(aasToPut))
        {
            throw new InvalidDataException($"Failed to load required shell '{pathToAas}' from embedded resources");
        }

        try
        {
            await repoProxyClient.PostAsync(_repoProxyOptions.AasPath, aasToPut);

            logger.LogInformation("Added AAS {AasId} (Name='{Name}')",
                requiredShell.Base64EncodedAasId, requiredShell.Name);
        }
        catch (Exception)
        {
            logger.LogError("Failed to add AAS {AasId} (Name='{Name}')",
                requiredShell.Base64EncodedAasId, requiredShell.Name);
            throw;
        }
    }

    private async Task AddOrUpdateSubmodelsForAas(RequiredShells requiredShell)
    {
        foreach (var submodelIdShort in requiredShell.SubmodelIdShorts)
        {
            var encodedAasId = requiredShell.Base64EncodedAasId;
            var pathToSubmodel = EmbeddedResourceShellPath + requiredShell.Name
                                 + ".Submodels." + submodelIdShort + ".json";
            var submodelToPut = EmbeddedResourceProvider.GetEmbeddedResourceContent(pathToSubmodel);

            if (string.IsNullOrEmpty(submodelToPut))
            {
                throw new InvalidDataException($"Failed to load required submodel '{pathToSubmodel}' from embedded resources");
            }

            try
            {
                dynamic submodelJson = JObject.Parse(submodelToPut);
                var submodelId = submodelJson.id.ToString();

                logger.LogInformation("Checking submodel {SubmodelIdShort} for {AasId}", submodelIdShort, encodedAasId);
                var isSubmodelExisting = await IsSubmodelAlreadyExisting(submodelId);

                if (isSubmodelExisting)
                {
                    // For some default AAS it is useful to put all submodels if the AAS already exists, for example to provide new default templates
                    // For the Configuration AAS you do not want to overwrite existing submodels because there the actual settings are stored => skip it!
                    if (requiredShell.SkipIfAlreadyExists)
                    {
                        logger.LogInformation(
                            "Skip updating submodels due to configuration 'SkipIfAlreadyExists=true' in appsettings.json for this AAS.");
                        continue;
                    }

                    var base64EncodedSubmodelId = Base64StringDeAndEncoder.EncodeTo64(submodelId);
                    await repoProxyClient.PutAsync($"{_repoProxyOptions.SubmodelPath}/{base64EncodedSubmodelId}",
                        submodelToPut);

                    logger.LogInformation("Updated submodel {SubmodelIdShort} for {AasId}",
                        submodelIdShort, encodedAasId);
                }
                else
                {
                    await repoProxyClient.PostSubmodelWithReferenceAsync(encodedAasId, submodelId, submodelToPut);
                    logger.LogInformation("Added submodel {SubmodelIdShort} to {AasId}",
                        submodelIdShort, encodedAasId);
                }
            }
            catch (Exception)
            {
                logger.LogError("Failed to add or update submodel {SubmodelIdShort} to {AasId}",
                    submodelIdShort, encodedAasId);
                throw;
            }
        }

        if (requiredShell.Files.Count != 0)
        {
            await UploadFileContentToAnExistingSubmodelElement(requiredShell);
        }
    }

    private async Task<bool> IsAasIdAlreadyExisting(string base64EncodedAasId)
    {
        try
        {
            await repoProxyClient.GetAsync($"{_repoProxyOptions.AasPath}/{base64EncodedAasId}");
            return true;
        }
        catch (RepoProxyException ex) when (ex.InnerException is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return false;
        }
    }

    private async Task<bool> IsSubmodelAlreadyExisting(string submodelIdNotEncoded)
    {
        var base64EncodedSubmodelId = Base64StringDeAndEncoder.EncodeTo64(submodelIdNotEncoded);
        try
        {
            await repoProxyClient.GetAsync($"{_repoProxyOptions.SubmodelPath}/{base64EncodedSubmodelId}");
            return true;
        }
        catch (RepoProxyException ex) when (ex.InnerException is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return false;
        }
    }

    private async Task UploadThumbnailToAas(RequiredShells requiredShell)
    {
        logger.LogInformation("Uploading thumbnail for {AasID} (Name='{Name}')", requiredShell.Base64EncodedAasId, requiredShell.Name);

        var pathToThumbnail = EmbeddedResourceShellPath + requiredShell.Name
                                 + ".Thumbnail." + requiredShell.AasThumbnailName;

        try
        {
            var fileBytes = EmbeddedResourceProvider.GetEmbeddedResourceBytes(pathToThumbnail);
            var thumbnailPath = $"{_repoProxyOptions.AasPath}/{requiredShell.Base64EncodedAasId}/asset-information/thumbnail?fileName={requiredShell.AasThumbnailName}";
            await repoProxyClient.PutFileContent(thumbnailPath, requiredShell.AasThumbnailName, fileBytes);
        }
        catch (Exception e)
        {
            logger.LogWarning("Failed to load thumbnail for {AasID} with error: {error}",
                        requiredShell.Base64EncodedAasId, e.Message);
            throw;
        }
    }

    private async Task UploadFileContentToAnExistingSubmodelElement(RequiredShells requiredShell)
    {
        foreach (var file in requiredShell.Files)
        {
            logger.LogInformation("Uploading {File} (AAS Name='{Name}')", file.FileName, requiredShell.Name);

            var pathToAttachment = EmbeddedResourceShellPath + requiredShell.Name
                                     + ".Files." + file.FileName;

            try
            {
                var fileBytes = EmbeddedResourceProvider.GetEmbeddedResourceBytes(pathToAttachment);
                var attachmentPath = $"{_repoProxyOptions.SubmodelPath}/{file.SubmodelIdBase64Encoded}/submodel-elements/{file.IdShortPath}/attachment?fileName={file.FileName}";
                await repoProxyClient.PutFileContent(attachmentPath, file.FileName, fileBytes);
            }
            catch (Exception e)
            {
                logger.LogWarning("Failed to load {AasID} with error: {error}",
                            file.FileName, e.Message);
                throw;
            }
        }
    }

    private bool VerifyTemplatesConfiguration(string requiredShellName) =>
        TemplatesBlacklist.Contains(requiredShellName) &&
        !string.IsNullOrWhiteSpace(_submodelTemplatesApiUrl);

    private bool VerifyBlueprintsConfiguration(string requiredShellName) =>
        BlueprintsBlacklist.Contains(requiredShellName) &&
        !string.IsNullOrWhiteSpace(_submodelBlueprintsApiUrl);
}
