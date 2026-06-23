# Migrate to AAS Generator 1.3.0

This guide describes the breaking changes introduced in AAS Generator 1.3.0 and the steps required to upgrade.


## Backend Migration to BaSyx Go (PostgreSQL)

The most significant change in 1.3.0 is the migration from **BaSyx Java (MongoDB-backed)** to **BaSyx Go (PostgreSQL-backed)**. BaSyx Go enforces stricter AAS Metamodel v3 compliance, offers better performance, and uses PostgreSQL as a more conventional database backend.

### What Changed

- **MongoDB has been replaced with PostgreSQL 16** (`postgres:16-alpine`). Existing MongoDB data is **not** automatically migrated.
- The BaSyx Java `aas-environment` is replaced by the **unified BaSyx Go environment** (`eclipsebasyx/aasenvironment-go:1.0.0-rc.5`, port `8081`), which serves the AAS repository, Submodel repository, Concept Description repository, registries, and discovery from a single service.
- A one-shot **configuration service** (`eclipsebasyx/basyxconfigurationservice-go:1.0.0-rc.5`) initializes the PostgreSQL schema before the environment starts.
- The development compose file has been renamed to **`docker-compose/compose.dev.go.yml`**.
- Health checks now use the bundled `/bin/healthprobe` binary instead of curl/actuator endpoints.

### Migration Steps

1. Start the new stack with the Go compose file:
   ```
   docker compose -f docker-compose/compose.dev.go.yml up
   ```
2. Re-import any AAS and Submodels into the new PostgreSQL-backed repositories (MongoDB data does not carry over).
3. If you maintain your own deployment manifests, update them to point at the unified Go environment, the configuration service, and the PostgreSQL database.

> See [BaSyx Go Migration](basyx-go-migration/README) for the full technical breakdown.


## Stricter AAS v3 Compliance (JSON Normalization)

BaSyx Go rejects several legacy AAS v2 constructs that the Java implementation previously tolerated. The `RepoProxyClient` now normalizes all outbound JSON to AAS v3 before sending it to the repository. If you manage blueprints or shell resources **externally**, they must be v3-compliant.

The following normalizations are applied automatically at runtime:

- Null-valued properties are removed recursively.
- Deprecated `dataSpecification` / `hasDataSpecification` fields are stripped.
- `kind` is stripped from all non-Submodel elements (only Submodel-level `kind` is valid in v3).
- `parent` back-references are stripped.
- Legacy Key fields (`local`, `idType`, `index`) are stripped.
- Legacy collection fields (`ordered`, `allowDuplicates`) are stripped.
- `valueType` is normalized to canonical XSD case (e.g., `xs:Date` → `xs:date`).
- `valueType: xs:string` is injected on qualifiers missing it.
- Non-string `Property.value` values (numbers/booleans) are coerced to strings.

For externally managed v2 resources, use the helper scripts shipped with the migration docs:

- `fix_resources.py <directory>` — rewrite legacy AAS v2 JSON resources for v3 compliance.
- `verify_resources.py <directory> <basyx-url>` — validate resources against a live BaSyx Go repository.


## Removal of the AAS Relationship (`derivedFrom`) Endpoint

The `AasInheritanceService` and the `AasRelationshipController` have been **removed entirely**. The endpoint:

```
GET /api/v2/AasRelationship/GetDerivedFrom
```

is no longer available. Any client relying on it must stop calling it. BaSyx Go is expected to expose `derivedFrom` filtering natively in a future release.


## Blueprint Validation at Generation-Time

Starting with this version, the AAS Generator **validates blueprints at generation-time** in addition to the existing save-time validation. This is a defense-in-depth measure that ensures externally modified or un-migrated blueprints are rejected before producing invalid AAS JSON.

### What Changed

Previously, blueprint validation only ran when creating or updating blueprints via the API (`POST /blueprints`, `POST /blueprints/{id}`). Blueprints that were imported directly into the repository or modified externally were not validated during generation.

Now, the generation pipeline includes a validation step that runs the same rules as the save-time validator. If the blueprint fails validation, the generation pipeline will return a **500 Internal Server Error** with structured validation errors in the same format as the save-time 422 response.

### Enforced Rules

The generation-time validator runs the **same rule set as save-time** validation. All of the following rules are enforced:

| Rule | Description |
|------|-------------|
| `InvalidQualifierSegmentCount` | `SMT/MappingInfo` qualifier type has more than 3 segments (e.g., `SMT/MappingInfo/value/extra`) |
| `EmptyMappingExpression` | Mapping expression (qualifier value) is null, empty, or whitespace-only |
| `UnknownFieldName` | `SMT/MappingInfo/{field}` uses a field name not in the allowed set |
| `FieldNotApplicableToModelType` | Field is not valid for the element's model type (e.g., `multiLanguage` on a `Property`) |
| `UnsupportedModelType` | The element's model type does not support mapping qualifiers |
| `DuplicateMappingField` | Same field mapped twice on the same element |
| `MlpValueAndMultiLanguageConflict` | A `MultiLanguageProperty` has both `value` and `multiLanguage` mappings |
| `InvalidJsonataSyntax` | Mapping expression has invalid JSONata syntax |
| `EmptyFilterExpression` | `SMT/FilterMappingInfo` qualifier value is empty or missing |
| `InvalidFilterJsonataSyntax` | Filter expression has invalid JSONata syntax |
| `EmptyCollectionPath` | `SMT/CollectionMappingInfo` qualifier value is empty or missing |
| `CollectionPathMissingWildcard` | Collection mapping path does not end with `[*]` |
| `InvalidCollectionJsonPath` | Collection mapping path has invalid JSONPath syntax |
| `InvalidCollectionParentModelType` | Collection mapping is on an element whose parent is not a valid container (`SubmodelElementCollection`, `SubmodelElementList`, `Entity`) |
| `InvalidCardinalityValue` | `SMT/Cardinality` value is not one of: `One`, `ZeroToOne`, `OneToMany`, `ZeroToMany` |

Additionally, **OneToMany cardinality is now strictly enforced**: if the data contains an empty array for a mandatory collection (`OneToMany`), generation will fail with an error instead of silently removing the element.

### How to Test Existing Blueprints

You can validate your existing blueprints **before** relying on generation by simply PUT or POST-ing them to the blueprint API:

```
POST /api/v2/Blueprints
Content-Type: application/json

{ ...your blueprint JSON... }
```

If the blueprint has issues, the API will return a **422 Unprocessable Entity** response with structured validation errors:

```json
{
  "errors": [
    {
      "rule": "UnknownFieldName",
      "path": "submodelElements[0].qualifiers[1]",
      "message": "Field 'notAllowedField' is not a recognized mapping field. Allowed: contentType, displayName, entityType, first, globalAssetId, idShort, multiLanguage, second, value."
    }
  ]
}
```

### Recommended Migration Steps

1. **Re-save all existing blueprints** via the blueprint API (`POST /api/v2/Blueprints/{submodelId}`) to trigger save-time validation
2. Fix any validation errors reported in the 422 responses
3. Verify generation works by running a test data ingest for each blueprint
4. If blueprints are managed externally (e.g., imported directly into the repository), ensure they pass validation before deploying

### Error Response at Generation-Time

If a blueprint fails validation during generation, the response will include:

```json
{
  "errors": [
    {
      "rule": "FieldNotApplicableToModelType",
      "path": "submodelElements[0].qualifiers[1]",
      "message": "Field 'multiLanguage' is not valid on model type 'Property'. Allowed fields: displayName, idShort, value."
    }
  ],
  "results": [
    {
      "blueprintId": "urn:example:blueprint",
      "success": false,
      "message": "Blueprint validation failed. The blueprint may have been modified externally or was not migrated.",
      "validationErrors": [...]
    }
  ]
}
```

HTTP Status: **500 Internal Server Error** (indicates system state corruption, not a user input error)
