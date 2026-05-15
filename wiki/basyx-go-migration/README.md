# BaSyx Go Migration

This document describes the migration from BaSyx Java (MongoDB-backed) to BaSyx Go (PostgreSQL-backed).

## Rationale

BaSyx Go enforces stricter AAS v3 compliance than the Java implementation. Moving to Go provides:
- Better performance and lower resource usage
- Stricter AAS Metamodel v3 validation
- PostgreSQL as a more conventional database backend
- Separate AAS Repository, Submodel Repository, and Discovery services

## Architecture Changes

### Before (BaSyx Java)
```
MongoDB ← aas-environment (combined AAS + Submodel) ← mnestix-aas-generator
           aas-discovery                               ↑
```

### After (BaSyx Go)
```
PostgreSQL ← aas-repository-go    ← mnestix-aas-generator
             submodel-repository-go  ↑
             aas-discovery-go        ↑
```

## Changes Overview

### 1. RepoProxyClient JSON Normalization

`MnestixCore/RepoProxyClient/RepoProxyClient.cs` now applies `NormalizeJsonForRepository()` to all outbound JSON payloads before sending to BaSyx. This handles the stricter v3 compliance at runtime:

| Rule | Description |
|------|-------------|
| Strip nulls | Remove null-valued properties recursively |
| Remove `dataSpecification` / `hasDataSpecification` | Deprecated v2 fields |
| Strip `kind` from non-Submodel elements | Only Submodel-level `kind` is valid in v3 |
| Strip `parent` back-references | v2 field not in v3 |
| Strip `local`, `idType`, `index` | v2 Key fields |
| Strip `ordered`, `allowDuplicates` | v2 collection fields |
| Normalize `valueType` | Canonical XSD case (e.g., `xs:Date` → `xs:date`) |
| Inject `valueType: xs:string` | On qualifiers missing it |
| Coerce `Property.value` | Non-string values (numbers/booleans) → string |

Additionally, all HTTP methods switched from RestSharp's typed methods (`.PostAsync()`, `.PutAsync()`) to `.ExecuteAsync()` for better error diagnostics.

### 2. Required Shell Resources

All 27+ embedded JSON resources under `RequiredShellsResources/` were cleaned up:
- UTF-8 BOM removed
- v2 fields stripped (see table above)
- `"type": "ExternalReference"` injected on References missing it
- Numeric `Property.value` coerced to strings
- Single-object `MultiLanguageProperty.value` wrapped in arrays

Use `fix_resources.py` to apply these same fixes to any additional resources.

### 3. MapDataToInstanceStep

Added tolerance for empty-string `value` fields on `Property` elements. BaSyx Go strips empty-string `value` fields on round-trip, so no empty-string placeholder is injected — the field is simply absent when no data is mapped.

### 4. AAS Inheritance — Removed

The `AasInheritanceService` and the `AasRelationshipController` (`GET /api/v2/AasRelationship/GetDerivedFrom`) have been removed entirely. BaSyx Go is expected to expose `derivedFrom` filtering natively in a future release.

### 5. Docker Compose

- MongoDB replaced with PostgreSQL 16 (`postgres:16-alpine`)
- BaSyx Java services replaced with Go equivalents (all `1.0.0-rc.1`):
  - `eclipsebasyx/aasrepository-go` (port 8081)
  - `eclipsebasyx/submodelrepository-go` (port 8082)
  - `eclipsebasyx/aasdiscovery-go` (port 8083)
- Health checks updated (wget-based instead of curl/actuator)

### 6. Dockerfile

- Removed `libssl1.1` installation block (was only needed for EphemeralMongo tests)
- Tests now run on all architectures
- Removed `EphemeralMongo6` test package

## Helper Scripts

- `fix_resources.py <directory>` — Rewrite legacy AAS v2 JSON resources for v3 compliance
- `verify_resources.py <directory> <basyx-url>` — Validate resources against a live BaSyx Go repository

## Verification

1. `dotnet build` — Compilation succeeds
2. `dotnet test` — All tests pass
3. `docker compose -f docker-compose/compose.dev.yml up` — All Go services start healthy
4. POST AAS + submodel through API → round-trips correctly
