# Quickstart: Generator Workflow Logging

**Feature**: 007-generator-workflow-logging

## What Changed

The AAS Generator now produces a complete log trail for the entire `AddDataToAasAsync` workflow — not just the data mapping step. Logs cover blueprint retrieval, ID generation, data mapping (with all existing pipeline step logs preserved), and repository persistence.

## How to Use

### Debug Mode (success path)

Send a request with `debug: true`:

```json
POST /api/v1/data-ingest/{base64AasId}
{
  "blueprintsIds": ["urn:example:nameplate"],
  "data": { ... },
  "language": "en",
  "debug": true
}
```

Response includes full workflow logs:

```json
{
  "results": [
    {
      "blueprintId": "urn:example:nameplate",
      "success": true,
      "generatedSubmodelId": "urn:example:sm-123",
      "debugInfo": {
        "logs": [
          "INFO [2026-04-23T14:30:00.1234567Z] - Fetching blueprint: urn:example:nameplate",
          "INFO [2026-04-23T14:30:00.2345678Z] - Blueprint fetched successfully",
          "INFO [2026-04-23T14:30:00.2456789Z] - Generating submodel ID",
          "INFO [2026-04-23T14:30:00.3456789Z] - Submodel ID generated: urn:example:sm-123",
          "INFO [2026-04-23T14:30:00.3567890Z] - Starting data mapping",
          "INFO [2026-04-23T14:30:00.3567890Z] - Started DeepCloneBlueprintStep",
          "... (DataMapper pipeline logs) ...",
          "INFO [2026-04-23T14:30:00.4567890Z] - Data mapping completed",
          "INFO [2026-04-23T14:30:00.4567890Z] - Posting submodel to repository",
          "INFO [2026-04-23T14:30:00.5567890Z] - Submodel reference added to shell"
        ]
      }
    }
  ]
}
```

### Error Path (logs always included)

When an error occurs, `errorInfo.logs` contains the workflow log trail up to the point of failure — regardless of the `debug` flag:

```json
{
  "results": [
    {
      "blueprintId": "urn:example:nameplate",
      "success": false,
      "message": "Failed to fetch blueprint from blueprint provider: Not Found",
      "errorInfo": {
        "logs": [
          "INFO [2026-04-23T14:30:00.1234567Z] - Fetching blueprint: urn:example:nameplate",
          "ERROR [2026-04-23T14:30:00.2345678Z] - Blueprint fetch failed: Not Found"
        ]
      }
    }
  ]
}
```

## No Breaking Changes

- Response DTOs (`AasGeneratorResult`, `AasGeneratorDebugInfo`, `AasGeneratorErrorInfo`) are structurally unchanged.
- `debug: false` (or omitted) + successful generation still returns no logs — behavior unchanged.
- `errorInfo.qualifier` and `errorInfo.qualifierPath` remain mapping-error-specific — they are `null` for non-mapping errors.
