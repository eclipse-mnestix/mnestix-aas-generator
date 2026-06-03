# Migrate to AAS Generator 1.x

<!-- TODO: replace 1.x with actual version number once release is tagged -->
<!-- TODO: Add all the other (breaking) changes, that will be introduced in the new version -->


## Blueprint Validation at Generation-Time

Starting with this version, the AAS Generator **validates blueprints at generation-time** in addition to the existing save-time validation. This is a defense-in-depth measure that ensures externally modified or un-migrated blueprints are rejected before producing invalid AAS JSON.

### What Changed

Previously, blueprint validation only ran when creating or updating blueprints via the API (`POST /blueprints`, `POST /blueprints/{id}`). Blueprints that were imported directly into the repository or modified externally were not validated during generation.

Now, the generation pipeline includes a validation step that runs the same rules as the save-time validator. If the blueprint fails validation, the generation pipeline will return a **500 Internal Server Error** with structured validation errors in the same format as the save-time 422 response.

### Enforced Rules

The following rules are now enforced at generation-time:

| Rule | Description |
|------|-------------|
| `UnknownFieldName` | `SMT/MappingInfo/{field}` uses a field name not in the allowed set |
| `FieldNotApplicableToModelType` | Field is not valid for the element's model type (e.g., `multiLanguage` on a `Property`) |
| `MlpValueAndMultiLanguageConflict` | A `MultiLanguageProperty` has both `value` and `multiLanguage` mappings |
| `EmptyMappingExpression` | Mapping expression is null, empty, or whitespace-only |
| `InvalidJsonataSyntax` | Mapping expression has invalid JSONata syntax |
| `DuplicateMappingField` | Same field mapped twice on the same element |
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
      "message": "Field 'notAllowedField' is not a recognized mapping field. Allowed: value, idShort, displayName, multiLanguage, globalAssetId, entityType, first, second."
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
      "message": "Field 'multiLanguage' is not valid on model type 'Property'. Allowed fields: value, idShort, displayName."
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
