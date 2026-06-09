using Microsoft.Extensions.Options;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using Newtonsoft.Json.Linq;
using TemplateProvider = MnestixCore.AasCreator.Templates.TemplateProvider;

namespace MnestixCore.AasCreator;

internal class AasCreatorService(
        IAasIdGeneratorService aasIdGeneratorService,
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IAasGenerator aasGenerator)
    : IAasCreatorService
{
    private readonly RepoProxyOptions _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));

    /// <inheritdoc />
    public async Task<AasCreationResult> CreateAasAsync(string assetIdShortParam, CancellationToken cancellationToken = default)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, cancellationToken);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);

        var aasIdAlreadyExists = await IsAasIdAlreadyExisting(base64EncodedAasId, cancellationToken);
        if (aasIdAlreadyExists)
        {
            return new AasCreationResult(aasIds, AasCreationStatus.AlreadyExists);
        }

        var aas = TemplateProvider.GetAas(aasIds);

        try
        {
            await repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}", aas, cancellationToken);
            return new AasCreationResult(aasIds, AasCreationStatus.Created);
        }
        catch (Exception e)
        {
            return new AasCreationResult(aasIds, AasCreationStatus.UnknownError, e.Message);
        }
    }

    private async Task<bool> IsAasIdAlreadyExisting(string base64EncodedAasId, CancellationToken cancellationToken)
    {
        try
        {
            await repoProxyClient.GetAsync($"{_repoProxyOptions.AasPath}/{base64EncodedAasId}", cancellationToken);
            return true;
        }
        catch (RepoProxyException ex) when (ex.InnerException is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AasCreationWithSubmodelsResult> CreateAasWithSubmodelsAsync(
        string assetIdShortParam,
        IEnumerable<string>? blueprintsIds = null,
        JObject? data = null,
        string? language = null,
        bool debug = false,
        CancellationToken cancellationToken = default)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, cancellationToken);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);

        var aasIdAlreadyExists = await IsAasIdAlreadyExisting(base64EncodedAasId, cancellationToken);
        if (aasIdAlreadyExists)
        {
            return new AasCreationWithSubmodelsResult(aasIds, AasCreationStatus.AlreadyExists, Enumerable.Empty<AasGenerator.AasGeneratorResult>());
        }

        // Create the AAS first
        var aas = TemplateProvider.GetAas(aasIds);

        try
        {
            await repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}", aas, cancellationToken);
        }
        catch (Exception e)
        {
            return new AasCreationWithSubmodelsResult(aasIds, AasCreationStatus.UnknownError, Enumerable.Empty<AasGenerator.AasGeneratorResult>(), e.Message);
        }

        // If submodel parameters are provided, generate and add submodels
        IEnumerable<AasGenerator.AasGeneratorResult> submodelResults = Enumerable.Empty<AasGenerator.AasGeneratorResult>();
        
        if (blueprintsIds != null && blueprintsIds.Any())
        {
            // Validate required parameters for submodel generation
            if (data == null || string.IsNullOrEmpty(language))
            {
                // Delete the AAS since submodel generation cannot proceed
                await TryDeleteAasAsync(base64EncodedAasId, cancellationToken);
                return new AasCreationWithSubmodelsResult(
                    aasIds,
                    AasCreationStatus.UnknownError,
                    Enumerable.Empty<AasGenerator.AasGeneratorResult>(),
                    "BlueprintsIds provided but Data or Language is missing. All three parameters are required for submodel generation.");
            }

            // Generate and add submodels to the newly created AAS
            submodelResults = await aasGenerator.AddDataToAasAsync(base64EncodedAasId, blueprintsIds, data, language, debug,
                preamble: $"Created a new AAS with aasId {aasIds.aasId}", cancellationToken: cancellationToken);

            // Check if any submodel generation failed
            if (submodelResults.Any(r => !r.Success))
            {
                // Delete the AAS since submodel generation failed
                await TryDeleteAasAsync(base64EncodedAasId, cancellationToken);
                return new AasCreationWithSubmodelsResult(
                    aasIds, 
                    AasCreationStatus.UnknownError, 
                    submodelResults,
                    "Submodel generation failed. AAS was deleted.");
            }
        }

        var aasRepoUrl = repoProxyClient.GetAasRepositoryUrl();

        return new AasCreationWithSubmodelsResult(aasIds, AasCreationStatus.Created, submodelResults, aasRepoUrl);
    }

    private async Task TryDeleteAasAsync(string base64EncodedAasId, CancellationToken cancellationToken)
    {
        try
        {
            await repoProxyClient.DeleteAsync($"{_repoProxyOptions.AasPath}/{base64EncodedAasId}", cancellationToken);
        }
        catch
        {
            // Ignore deletion errors - best effort cleanup
        }
    }
}