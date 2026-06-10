# Concept: Narrow the SDK to a Pure Generation Engine

**Date**: 2026-06-10
**Status**: Draft for review
**Supersedes (partially)**: previous 0.1.0 public API surface (no checked-in
`contracts/public-api.md` exists for this feature yet, so add or update that
contract when this concept is adopted)

## Goal

Today `Mnestix.AasGenerator.Core` is not a pure generation library — it bundles
HTTP transport, repository fetching, id-settings lookup, and persistence together
with the actual generation logic. This concept narrows it so that:

> **Core does generation only**: blueprint JSON(s) + data JSON + caller-supplied
> ids in → AAS shell JSON + Submodel object(s) out. No HTTP, no repository
> client, no fetching, no persistence, no id generation.

Everything else (fetching blueprints, reading id-generation settings, assembling
ids, persisting to BaSyx) lives in a second SDK that *uses* Core. The Docker
container behaves exactly as it does today.

## Three artifacts

```
┌─────────────────────────────────────────────────────────────┐
│ MnestixApi (Docker host)                                      │
│  - REST controllers, auth, Swagger, health checks            │
│  - UNCHANGED public REST surface & Docker behavior           │
│  references ▼                                                 │
├─────────────────────────────────────────────────────────────┤
│ Mnestix.AasGenerator.Integration   (NEW package)             │
│  - RepoProxyClient + HTTP transport + OpenID                 │
│  - BlueprintProvider (fetch blueprint JSON by id)            │
│  - MnestixConfigurationProvider (read id-gen settings)       │
│  - AasIdGeneratorService (ASSEMBLE ids)  ← stays here         │
│  - Orchestration: fetch → call Core → persist                │
│  - AddMnestixAasGenerator(...) DI entry point                │
│  references ▼                                                 │
├─────────────────────────────────────────────────────────────┤
│ Mnestix.AasGenerator.Core          (renamed, now PURE)       │
│  - DataMapper + 10-step mapping pipeline                     │
│  - BlueprintValidator                                        │
│  - AAS shell builder (TemplateProvider.GetAas)               │
│  - Pure DTOs / errors                                        │
│  - NO HTTP, NO repo client, NO fetching, NO id generation    │
└─────────────────────────────────────────────────────────────┘

Mnestix.AasGenerator.DefaultTemplates → references Integration
  (RequiredShellsAssertion seeds the repo, so it needs the repo client)
```

## What Core keeps (pure)

| Area | Files (current `MnestixCore/`) |
|------|--------------------------------|
| Mapping engine | `AASGenerator/SubmodelDataToInstanceMapper/**` (DataMapper, all pipeline steps, field assigners, JsonataEvaluator, QualifierHelpers, DataMappingContext, MappingDescriptor, ResolvedMapping) |
| Validation | `TemplateBuilder/BlueprintValidator.cs`, `IBlueprintValidator.cs`, `BlueprintValidationError.cs`, `BlueprintValidationRule.cs` |
| AAS shell builder | `AasCreator/Templates/TemplateProvider.cs` + embedded `aas.json` |
| Shared helpers | `Shared/AasJsonNormalizer.cs`, `Base64StringDeAndEncoder.cs`, `JsonHelper.cs`, `Pipeline/**`, `FieldMappingRules.cs`, `EmbeddedResourceProvider.cs` |
| Pure DTOs | `Dtos/AasIds.cs`, `Dtos/Key.cs`, `Dtos/SubmodelReference.cs`, `AASGenerator/SubmodelGenerationRequest.cs`, `AASGenerator/SubmodelGenerationResult.cs`, `AASGenerator/AasGenerationResult.cs`, `AASGenerator/GenerationErrorInfo.cs`, `AASGenerator/GenerationDebugInfo.cs`, `WorkflowLogger.cs` |
| Errors | `Errors/BlueprintValidationException.cs`, `SubmodelDataToInstancePipelineException.cs` |

### New Core public surface

A single pure engine — no async needed since there is no I/O (mapping is
CPU-only; the current `RunAsync` is already executed synchronously via
`GetAwaiter().GetResult()` in `DataMapper`):

```csharp
namespace Mnestix.AasGenerator;

public interface IAasGenerationEngine
{
    /// Map one blueprint + data into a ready submodel instance.
    /// Caller supplies the submodel id. Throws on validation/mapping failure.
    JObject MapSubmodel(JObject blueprint, JObject data, string? language, string submodelId);

    /// Map many blueprints, capturing per-blueprint success/failure + logs
    /// (pure Core result shape, with the generated submodel object).
    IReadOnlyList<SubmodelGenerationResult> GenerateSubmodels(
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false);

    /// Build an AAS shell JSON document from already-assembled ids.
    string CreateAasShellJson(AasIds aasIds);

    /// Composite one-shot generation: given the AAS ids, the data payload, and
    /// multiple blueprints (each paired with its source blueprint id and target submodel id), build the
    /// AAS shell JSON AND map every blueprint into a submodel, returning the
    /// whole bundle. This is the primary "data + blueprints in → AAS shell JSON
    /// + submodel objects out" entry point. No persistence — Core only produces
    /// the payloads.
    AasGenerationResult GenerateAas(
        AasIds aasIds,
        IEnumerable<SubmodelGenerationRequest> requests,
        JObject data,
        string? language,
        bool debug = false);
}

public sealed record SubmodelGenerationRequest(string BlueprintId, JObject Blueprint, string SubmodelId);

public sealed record GenerationErrorInfo
{
    public IList<string>? Logs { get; init; }
    public string? Qualifier { get; init; }
    public string? QualifierPath { get; init; }
}

public sealed record GenerationDebugInfo
{
    public IList<string>? Logs { get; init; }
}

public sealed record SubmodelGenerationResult
{
    public string BlueprintId { get; init; } = null!;
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string GeneratedSubmodelId { get; init; } = "";
    public JObject? Submodel { get; init; }
    public GenerationErrorInfo? ErrorInfo { get; init; }
    public GenerationDebugInfo? DebugInfo { get; init; }
    public IReadOnlyList<BlueprintValidationError>? ValidationErrors { get; init; }
}

/// The full output of a composite generation: the AAS shell JSON plus the
/// per-blueprint submodel results (each carrying the produced submodel object
/// or its failure info).
public sealed record AasGenerationResult(
    string AasJson,
    IReadOnlyList<SubmodelGenerationResult> SubmodelResults)
{
    /// Convenience: true when every submodel mapped successfully.
    public bool Success => SubmodelResults.All(r => r.Success);

    /// Convenience: only the successfully produced submodel objects.
    public IEnumerable<JObject> Submodels =>
        SubmodelResults.Where(r => r.Success && r.Submodel is not null).Select(r => r.Submodel!);
}
```

Core must not add `Submodel` to the existing REST-facing `AasGeneratorResult`,
because that type is serialized by `DataIngestController` and `AasCreatorController`
today. Instead, Core returns `SubmodelGenerationResult` with the produced
`JObject`. Integration maps this to the existing `AasGeneratorResult` before
returning through REST, preserving the current response shape (no full submodel
payload in `results` / `submodelResults`).

> **`GenerateAas` is the headline Core API.** `MapSubmodel`, `GenerateSubmodels`,
> and `CreateAasShellJson` are the lower-level building blocks it composes; they stay
> public so Integration (and external consumers) can call individual steps —
> e.g. `AddDataToAasAsync` adds submodels to an *existing* shell and so uses
> `GenerateSubmodels` without `CreateAasShellJson`.

### Core DI

```csharp
namespace Mnestix.AasGenerator;
public static class MnestixAasGenerationCoreServiceCollectionExtensions
{
    // Pure registrations only: IAasGenerationEngine, IDataMapper, IBlueprintValidator.
    // No options, no transport, no network validation.
    public static IServiceCollection AddMnestixAasGenerationCore(this IServiceCollection services);
}
```

## What moves OUT of Core into Integration

| Area | Files moving |
|------|--------------|
| Transport | `RepoProxyClient/**`, `RestClientProvider/**` (HttpClientProvider, AccessTokenService, HttpClientTokenProvider), `Shared/BaseUrlProvider.cs`, `Shared/SubmodelHandler.cs` (+ `ISubmodelHandler`) |
| Fetching | `TemplateBuilder/BlueprintProvider.cs`, `TemplateProvider.cs`, `BlueprintCreator.cs`, `TemplateCreator.cs` (+ their interfaces) |
| Config / id settings | `ConfigurationService/**`, `IdGenerator/MnestixConfigurationProvider.cs` (+ `IMnestixConfigurationProvider`) |
| **ID generation** | `IdGenerator/AasIdGeneratorService.cs`, `StandardConformGuidGenerator.cs`, `IAasIdGeneratorService.cs`, `Dtos/IdGenerationSettings.cs` + `Dtos/Enums/**` |
| Orchestration | `AasCreator/AasCreatorService.cs` (+ `IAasCreatorService`, result records), repo-coupled half of `AASGenerator/AASGenerator.cs` (+ `IAasGenerator`, REST-facing `AasGeneratorResult`/`AasGeneratorErrorInfo`/`AasGeneratorDebugInfo`, `IDataMapper` consumer side) |
| Options | `Dtos/AppSettingsOptions/**` (RepoProxyOptions, ConfigurationOptions, RepositoryOpenIdConfiguration, RequiredShellsOptions, CustomerEndpointsSecurityOptions) |
| DI | `DependencyInjection/ServiceCollectionExtensions.cs` (`AddMnestixAasGenerator`), `MnestixAasGeneratorOptions.cs` |
| Errors | `Errors/RepoProxyException.cs`, `InvalidSubmodelException.cs`, `ErrorCodes.cs` (repo/template-management specific; `InvalidSubmodelException` currently depends on `ErrorCodes`) |

### Integration's job — the orchestration that used to be inside Core

`AasGenerator.AddDataToAasAsync` and `AasCreatorService` keep their **current
signatures** (so the host/controllers and `Web.Tests` are untouched), but their
bodies now delegate the actual generation to Core:

```
AddDataToAasAsync(base64AasId, blueprintIds, data, language, ...)
  validate base64AasId exactly as today
  for each blueprintId:
    blueprint   = BlueprintProvider.GetBlueprintAsync(blueprintId)   // fetch (Integration)
    submodelId  = AasIdGeneratorService.GenerateSubmodelIdsAsync()   // assemble (Integration)
    instance    = core.MapSubmodel(blueprint, data, language, submodelId)  // ← Core
    RepoProxyClient.PostAsync(submodels, instance)                   // persist (Integration)
    RepoProxyClient.PostAsync(.../submodel-refs, ref)                // persist (Integration)
    return existing REST-facing AasGeneratorResult without Submodel payload

CreateAasWithSubmodelsAsync(assetIdShort, ...)
    aasIds = AasIdGeneratorService.GenerateAasIdsAsync(assetIdShort) // assemble (Integration)
    base64AasId = Base64StringDeAndEncoder.EncodeTo64(aasIds.aasId)
    if RepoProxyClient.GetAsync(shells/{base64AasId}) succeeds:
        return AlreadyExists                                         // preserve current behavior
    aas    = core.CreateAasShellJson(aasIds)                         // ← Core
    RepoProxyClient.PostAsync(shells, aas)                           // persist (Integration)
    ... then AddDataToAasAsync for submodels ...
    if any submodel generation fails:
        RepoProxyClient.DeleteAsync(shells/{base64AasId})            // preserve current rollback behavior
        return UnknownError with existing submodel results
```

`AddMnestixAasGenerator(options)` (the wide DI entry point with RepositoryBaseUrl,
auth, paths, blueprint source) lives in **Integration** and internally calls
Core's `AddMnestixAasGenerationCore()`.

## DefaultTemplates package

`RequiredShellsAssertion` seeds shells into the repository, so it needs the repo
client → it references **Integration**, not Core. `AddMnestixDefaultTemplates()`
unchanged from the consumer's view.

## Tests

| Project | Change |
|---------|--------|
| `Core.Tests` | Keep pure tests: `AasGenerator/` mapping tests that exercise `DataMapper`/the pure engine, `TemplateBuilder/BlueprintValidatorTests`, `Shared/` helpers that have no repo/HTTP dependency. Pure subset only. |
| `Integration.Tests` (NEW) | Receives the repo-coupled tests: `RepoProxyClient/**`, `IdGenerator/MnestixConfigurationProviderTest`, `AasCreator/AasCreatorTest`, `TemplateBuilder/Blueprint{Provider,Creator}Test`, `TemplateProviderTest`, repo half of `AasGeneratorTests`, and REST-shape mapping tests proving Core's `SubmodelGenerationResult.Submodel` is not serialized by the host responses. |
| `Web.Tests` | **Unchanged.** Green here = host + Docker behavior identical. This is the primary regression gate. |

`[InternalsVisibleTo]` updated: Core → `Core.Tests` + `Integration` (+ Moq proxy);
Integration → `Integration.Tests` + `MnestixApi` + `DefaultTemplates`.

## Sequencing (commit after each step)

1. **Scaffold** `Mnestix.AasGenerator.Integration` project; add to sln; wire project refs (Integration→Core).
2. **Carve Core's pure API** — add `IAasGenerationEngine` + impl that wraps `DataMapper`/`BlueprintValidator`/AAS shell template provider; add `AddMnestixAasGenerationCore`. Core still compiles with the old classes present.
3. **Move I/O + id-gen classes** Core→Integration; fix namespaces; move `AddMnestixAasGenerator` + options into Integration; rewrite `AasGenerator`/`AasCreatorService` bodies to call Core. Delete the moved files from Core.
4. **Repoint host + DefaultTemplates** to Integration; `Program.cs`, controllers, REST surface unchanged.
5. **Split tests** into `Integration.Tests`; build all package TFMs (`net8.0;net9.0;net10.0` for Core/DefaultTemplates/Integration); run test projects on their declared test TFM(s) (currently `net8.0` for `Core.Tests` and `Web.Tests`; add `Integration.Tests` as `net8.0` unless it is explicitly multi-targeted); `dotnet pack` all packages.

## Decisions to confirm

1. **Naming** — new Core engine interface `IAasGenerationEngine`; new Core DI
   `AddMnestixAasGenerationCore`. OK, or prefer different names?
2. **Namespaces** — Core public types keep `MnestixCore.*` (frozen by prior
   handoff). New types use `Mnestix.AasGenerator.*`. Integration uses
   `Mnestix.AasGenerator.Integration.*`. OK?
3. **Core uses a separate `SubmodelGenerationResult` with `Submodel` (JObject)** so
   Core can return the produced object instead of persisting it, while Integration
   keeps the existing REST-facing `AasGeneratorResult` shape. OK?
4. **Public-api contract doc** is added or rewritten to the new narrow Core
   surface + Integration surface (acceptable since still 0.1.0 pre-release).

## Risks / notes

- Big diff; mitigated by step-wise commits and the unchanged `Web.Tests` gate.
- Package projects target `net8.0;net9.0;net10.0`, but current test projects are
  `net8.0`; do not claim net9/net10 test execution unless those test TFMs are
  added and the runtimes are installed.
- No local BaSyx → SC-001 manual validation still can't be done here.
- Byte-identical generated submodel JSON must hold (Constitution II): the mapping
  pipeline moves to Core *unchanged*, so output is preserved; `Web.Tests`
  integration tests assert end-to-end shape. The AAS shell path keeps returning
  the embedded template as `string` (`CreateAasShellJson`) instead of parsing and
  reserializing it, preserving today's shell payload formatting.
