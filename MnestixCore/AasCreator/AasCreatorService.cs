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
    public async Task<AasCreationResult> CreateAasAsync(string assetIdShortParam, string? globalAssetId = null, AssetKind assetKind = AssetKind.Instance)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, globalAssetId);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);

        var aasIdAlreadyExists = await IsAasIdAlreadyExisting(base64EncodedAasId);
        if (aasIdAlreadyExists)
        {
            return new AasCreationResult(aasIds, AasCreationStatus.AlreadyExists);
        }

        var aas = TemplateProvider.GetAas(aasIds, assetKind);

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
        bool overwrite = false,
        DefaultThumbnail? defaultThumbnail = null,
        AssetKind assetKind = AssetKind.Instance)
    {
        var aasIds = await aasIdGeneratorService.GenerateAasIdsAsync(assetIdShortParam, globalAssetId);
        var base64EncodedAasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId);
        var shellPath = $"{_repoProxyOptions.AasPath}/{base64EncodedAasId}";

        // 1. Build + validate all submodels in memory (no repo writes)
        var (buildFailure, builtResults, instancesToPost) =
            await BuildSubmodelsAsync(aasIds, blueprintsIds, data, language, debug);
        if (buildFailure != null)
        {
            return buildFailure;
        }

        // 2. POST submodel bodies (fail fast: roll back already-posted submodels on failure)
        var (postFailure, postedSubmodelIds) = await PostSubmodelsAsync(aasIds, builtResults, instancesToPost);
        if (postFailure != null)
        {
            return postFailure;
        }

        // 3. Build shell template with all submodel-refs baked in
        var shell = BuildShellWithRefs(aasIds, postedSubmodelIds, defaultThumbnail, assetKind);

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
    /// Builds and validates every requested submodel in memory without writing to the repo. When no blueprints are
    /// requested this is a no-op. On missing inputs or any build failure a terminal
    /// <see cref="AasCreationStatus.GenerationFailed"/> result is returned via <c>failure</c> and the shell is never touched.
    /// </summary>
    /// <param name="aasIds">Ids of the AAS being created.</param>
    /// <param name="blueprintsIds">Blueprint ids to generate submodels from; null/empty means no submodels.</param>
    /// <param name="data">Source data for generation; required when blueprints are provided.</param>
    /// <param name="language">Language for generation; required when blueprints are provided.</param>
    /// <param name="debug">Whether to include debug detail in build results.</param>
    /// <returns>
    /// A terminal <c>failure</c> result (non-null on error), the per-blueprint <c>builtResults</c> to echo back, and the
    /// successfully built <c>instances</c> ready to POST.
    /// </returns>
    private async Task<(AasCreationWithSubmodelsResult? failure, List<AasGenerator.AasGeneratorResult> builtResults, List<JObject> instances)> BuildSubmodelsAsync(
        AasIds aasIds,
        IEnumerable<string>? blueprintsIds,
        JObject? data,
        string? language,
        bool debug)
    {
        var builtResults = new List<AasGenerator.AasGeneratorResult>();
        var instancesToPost = new List<JObject>();

        if (blueprintsIds == null || !blueprintsIds.Any())
        {
            return (null, builtResults, instancesToPost);
        }

        if (data == null || string.IsNullOrEmpty(language))
        {
            var failure = new AasCreationWithSubmodelsResult(
                aasIds,
                AasCreationStatus.GenerationFailed,
                Enumerable.Empty<AasGenerator.AasGeneratorResult>(),
                errorMessage: "BlueprintsIds provided but Data or Language is missing. All three parameters are required for submodel generation.");
            return (failure, builtResults, instancesToPost);
        }

        foreach (var blueprintId in blueprintsIds)
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
            var failure = new AasCreationWithSubmodelsResult(
                aasIds,
                AasCreationStatus.GenerationFailed,
                builtResults,
                errorMessage: "Submodel generation failed. No AAS was created.");
            return (failure, builtResults, instancesToPost);
        }

        return (null, builtResults, instancesToPost);
    }

    /// <summary>
    /// POSTs each built submodel body, failing fast: on the first persistence error already-posted submodels in this
    /// request are rolled back and a terminal <see cref="AasCreationStatus.UnknownError"/> result is returned via <c>failure</c>.
    /// </summary>
    /// <param name="aasIds">Ids of the AAS being created.</param>
    /// <param name="builtResults">Per-blueprint build results, echoed back on a failure result.</param>
    /// <param name="instancesToPost">Submodel bodies to persist.</param>
    /// <returns>A terminal <c>failure</c> result (non-null on error) and the ids of submodels successfully POSTed.</returns>
    private async Task<(AasCreationWithSubmodelsResult? failure, List<string> postedSubmodelIds)> PostSubmodelsAsync(
        AasIds aasIds,
        List<AasGenerator.AasGeneratorResult> builtResults,
        List<JObject> instancesToPost)
    {
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
                var failure = await RollbackAndFail(aasIds, builtResults, postedSubmodelIds, $"Failed to persist submodel: {e.Message}");
                return (failure, postedSubmodelIds);
            }
        }

        return (null, postedSubmodelIds);
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
    /// <param name="defaultThumbnail">Optional default thumbnail to inject into the shell's asset information.</param>
    /// <param name="assetKind">AssetKind for the AAS (Instance, Type, or NotApplicable).</param>
    /// <returns>The serialized shell JSON.</returns>
    private string BuildShellWithRefs(AasIds aasIds, IReadOnlyCollection<string> submodelIds, DefaultThumbnail? defaultThumbnail, AssetKind assetKind)
    {
        var shell = JObject.Parse(TemplateProvider.GetAas(aasIds, defaultThumbnail, assetKind));
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