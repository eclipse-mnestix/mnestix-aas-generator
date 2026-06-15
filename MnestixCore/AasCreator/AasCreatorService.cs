using Microsoft.Extensions.Options;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using MnestixCore.Dtos;
using Newtonsoft.Json.Linq;
using TemplateProvider = MnestixCore.AasCreator.Templates.TemplateProvider;

namespace MnestixCore.AasCreator;

public class AasCreatorService(
        IAasIdGeneratorService aasIdGeneratorService,
        IRepoProxyClient repoProxyClient,
        IOptions<RepoProxyOptions> repoProxyOptions,
        IAasGenerator aasGenerator)
    : IAasCreatorService
{
    private readonly RepoProxyOptions _repoProxyOptions = repoProxyOptions.Value ?? throw new ArgumentNullException(nameof(repoProxyOptions));

    /// <inheritdoc />
    public async Task<AasCreationResult> CreateAasAsync(string assetIdShortParam, string? globalAssetId = null)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, globalAssetId);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);

        var aasIdAlreadyExists = await IsAasIdAlreadyExisting(base64EncodedAasId);
        if (aasIdAlreadyExists)
        {
            return new AasCreationResult(aasIds, AasCreationStatus.AlreadyExists);
        }

        var aas = TemplateProvider.GetAas(aasIds);

        try
        {
            await repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}", aas);
            return new AasCreationResult(aasIds, AasCreationStatus.Created);
        }
        catch (Exception e)
        {
            return new AasCreationResult(aasIds, AasCreationStatus.UnknownError, e.Message);
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

    /// <inheritdoc />
    public async Task<AasCreationWithSubmodelsResult> CreateAasWithSubmodelsAsync(
        string assetIdShortParam,
        IEnumerable<string>? blueprintsIds = null,
        JObject? data = null,
        string? language = null,
        bool debug = false,
        string? globalAssetId = null,
        bool overwrite = false)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, globalAssetId);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);
        var shellPath = $"{_repoProxyOptions.AasPath}/{base64EncodedAasId}";
        var hasBlueprints = blueprintsIds != null && blueprintsIds.Any();

        // 1. Build + validate all submodels in memory (no repo writes)
        var builtResults = new List<AasGenerator.AasGeneratorResult>();
        var instancesToPost = new List<JObject>();

        if (hasBlueprints)
        {
            if (data == null || string.IsNullOrEmpty(language))
            {
                return new AasCreationWithSubmodelsResult(
                    aasIds,
                    AasCreationStatus.GenerationFailed,
                    Enumerable.Empty<AasGenerator.AasGeneratorResult>(),
                    errorMessage: "BlueprintsIds provided but Data or Language is missing. All three parameters are required for submodel generation.");
            }

            foreach (var blueprintId in blueprintsIds!)
            {
                var built = await aasGenerator.BuildSubmodelAsync(blueprintId, data, language, debug,
                    preamble: $"Creating AAS with aasId {aasIds.aasId}");
                builtResults.Add(built.Result);
                if (built.Result.Success && built.Instance != null)
                {
                    instancesToPost.Add(built.Instance);
                }
            }

            if (builtResults.Any(r => !r.Success))
            {
                return new AasCreationWithSubmodelsResult(
                    aasIds,
                    AasCreationStatus.GenerationFailed,
                    builtResults,
                    errorMessage: "Submodel generation failed. No AAS was created.");
            }
        }

        // 2. POST submodel bodies (fail fast: roll back already-posted submodels on failure)
        var postedSubmodelIds = new List<string>();
        foreach (var instance in instancesToPost)
        {
            try
            {
                var postedId = await aasGenerator.PostSubmodelAsync(instance);
                postedSubmodelIds.Add(postedId);
            }
            catch (Exception e)
            {
                return await RollbackAndFail(aasIds, builtResults, postedSubmodelIds, $"Failed to persist submodel: {e.Message}");
            }
        }

        // 3. Build shell template with all submodel-refs baked in
        var shell = BuildShellWithRefs(aasIds, postedSubmodelIds);

        // 4. POST shell
        try
        {
            await repoProxyClient.PostAsync($"{_repoProxyOptions.AasPath}", shell);
        }
        catch (RepoProxyException e) when (e.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return await HandleShellConflictAsync(aasIds, shellPath, shell, builtResults, postedSubmodelIds, overwrite);
        }
        catch (Exception e)
        {
            return await RollbackAndFail(aasIds, builtResults, postedSubmodelIds, $"Failed to create AAS shell: {e.Message}");
        }

        return new AasCreationWithSubmodelsResult(
            aasIds,
            AasCreationStatus.Created,
            builtResults,
            repoProxyClient.GetAasRepositoryUrl());
    }

    /// <summary>
    /// Handles a 409 from the shell POST. When <paramref name="overwrite"/> is false the request is rejected as a
    /// conflict and this request's submodels are rolled back. When true the existing shell is captured, then replaced
    /// via PUT; the shell itself is never deleted.
    /// </summary>
    /// <param name="aasIds">Ids of the AAS being created.</param>
    /// <param name="shellPath">Repository path of the conflicting shell (base64 url-safe encoded id).</param>
    /// <param name="shell">The new shell body to PUT when overwriting.</param>
    /// <param name="builtResults">Per-blueprint submodel build results to echo back on the response.</param>
    /// <param name="postedSubmodelIds">Submodels already POSTed in this request, rolled back on conflict or failure.</param>
    /// <param name="overwrite">Whether to replace the existing shell instead of returning a conflict.</param>
    private async Task<AasCreationWithSubmodelsResult> HandleShellConflictAsync(
        AasIds aasIds,
        string shellPath,
        string shell,
        List<AasGenerator.AasGeneratorResult> builtResults,
        List<string> postedSubmodelIds,
        bool overwrite)
    {
        if (!overwrite)
        {
            var orphans = await RollbackSubmodelsAsync(postedSubmodelIds);
            return new AasCreationWithSubmodelsResult(
                aasIds,
                AasCreationStatus.Conflict,
                builtResults,
                errorMessage: "AAS already exists, use overwrite=true to replace",
                orphanedSubmodelIds: orphans);
        }

        JObject? previousAas;
        try
        {
            var previousRaw = await repoProxyClient.GetAsync(shellPath);
            previousAas = string.IsNullOrWhiteSpace(previousRaw) ? null : JObject.Parse(previousRaw);
            await repoProxyClient.PutAsync(shellPath, shell);
        }
        catch (Exception e)
        {
            return await RollbackAndFail(aasIds, builtResults, postedSubmodelIds, $"Failed to overwrite existing AAS shell: {e.Message}");
        }

        return new AasCreationWithSubmodelsResult(
            aasIds,
            AasCreationStatus.Overwritten,
            builtResults,
            repoProxyClient.GetAasRepositoryUrl(),
            previousAas: previousAas);
    }

    /// <summary>
    /// Builds the shell template for <paramref name="aasIds"/> with submodel references for every id in
    /// <paramref name="submodelIds"/> baked into its <c>submodels</c> array, so the shell is created in one POST.
    /// </summary>
    /// <param name="aasIds">Ids of the AAS to template.</param>
    /// <param name="submodelIds">Submodel ids (not encoded) to reference from the shell.</param>
    /// <returns>The serialized shell JSON.</returns>
    private string BuildShellWithRefs(AasIds aasIds, IReadOnlyCollection<string> submodelIds)
    {
        var shell = JObject.Parse(TemplateProvider.GetAas(aasIds));
        if (submodelIds.Count > 0)
        {
            var refs = new JArray();
            foreach (var submodelId in submodelIds)
            {
                refs.Add(JObject.Parse(SubmodelReference.ToJson(submodelId)));
            }
            shell["submodels"] = refs;
        }
        return shell.ToString();
    }

    /// <summary>
    /// Rolls back submodels posted in this request and builds an <see cref="AasCreationStatus.UnknownError"/> result.
    /// Submodels that could not be deleted are surfaced as orphans on the result.
    /// </summary>
    private async Task<AasCreationWithSubmodelsResult> RollbackAndFail(
        AasIds aasIds,
        List<AasGenerator.AasGeneratorResult> builtResults,
        List<string> postedSubmodelIds,
        string errorMessage)
    {
        var orphans = await RollbackSubmodelsAsync(postedSubmodelIds);
        return new AasCreationWithSubmodelsResult(
            aasIds,
            AasCreationStatus.UnknownError,
            builtResults,
            errorMessage: errorMessage,
            orphanedSubmodelIds: orphans);
    }

    /// <summary>
    /// Best-effort deletion of submodels created during this request. Returns ids that could not be deleted.
    /// </summary>
    private async Task<List<string>> RollbackSubmodelsAsync(IEnumerable<string> submodelIds)
    {
        var orphaned = new List<string>();
        foreach (var submodelId in submodelIds)
        {
            try
            {
                var base64SubmodelId = Base64StringDeAndEncoder.EncodeTo64(submodelId);
                await repoProxyClient.DeleteAsync($"{_repoProxyOptions.SubmodelPath}/{base64SubmodelId}");
            }
            catch
            {
                orphaned.Add(submodelId);
            }
        }
        return orphaned;
    }
}