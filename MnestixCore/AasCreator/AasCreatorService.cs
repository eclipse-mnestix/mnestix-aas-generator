using Microsoft.Extensions.Options;
using MnestixCore.AasCreator.Interfaces;
using MnestixCore.AasGenerator.Interfaces;
using MnestixCore.Dtos.AppSettingsOptions;
using MnestixCore.Errors;
using MnestixCore.IdGenerator.Interfaces;
using MnestixCore.RepoProxyClient.Interfaces;
using MnestixCore.Shared;
using MnestixCore.Dtos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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
                    AasCreationStatus.UnknownError,
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
                    AasCreationStatus.UnknownError,
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
                var orphans = await RollbackSubmodelsAsync(postedSubmodelIds);
                return new AasCreationWithSubmodelsResult(
                    aasIds,
                    AasCreationStatus.UnknownError,
                    builtResults,
                    errorMessage: $"Failed to persist submodel: {e.Message}",
                    orphanedSubmodelIds: orphans);
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
            var orphans = await RollbackSubmodelsAsync(postedSubmodelIds);
            return new AasCreationWithSubmodelsResult(
                aasIds,
                AasCreationStatus.UnknownError,
                builtResults,
                errorMessage: $"Failed to create AAS shell: {e.Message}",
                orphanedSubmodelIds: orphans);
        }

        return new AasCreationWithSubmodelsResult(
            aasIds,
            AasCreationStatus.Created,
            builtResults,
            repoProxyClient.GetAasRepositoryUrl());
    }

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

        string? previousAas;
        try
        {
            previousAas = await repoProxyClient.GetAsync(shellPath);
            await repoProxyClient.PutAsync(shellPath, shell);
        }
        catch (Exception e)
        {
            var orphans = await RollbackSubmodelsAsync(postedSubmodelIds);
            return new AasCreationWithSubmodelsResult(
                aasIds,
                AasCreationStatus.UnknownError,
                builtResults,
                errorMessage: $"Failed to overwrite existing AAS shell: {e.Message}",
                orphanedSubmodelIds: orphans);
        }

        return new AasCreationWithSubmodelsResult(
            aasIds,
            AasCreationStatus.Overwritten,
            builtResults,
            repoProxyClient.GetAasRepositoryUrl(),
            previousAas: previousAas);
    }

    private string BuildShellWithRefs(AasIds aasIds, IReadOnlyCollection<string> submodelIds)
    {
        var shell = JObject.Parse(TemplateProvider.GetAas(aasIds));
        if (submodelIds.Count > 0)
        {
            var refs = new JArray();
            foreach (var submodelId in submodelIds)
            {
                var reference = new SubmodelReference(new List<Key> { new("Submodel", submodelId) }, "ModelReference");
                var referenceJson = JsonConvert.SerializeObject(reference, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                refs.Add(JObject.Parse(referenceJson));
            }
            shell["submodels"] = refs;
        }
        return shell.ToString();
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