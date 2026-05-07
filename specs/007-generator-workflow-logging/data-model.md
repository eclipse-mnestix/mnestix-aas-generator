# Data Model: Generator Workflow Logging

**Feature**: 007-generator-workflow-logging  
**Date**: 2026-04-23

## New Entities

### WorkflowLogger

A lightweight helper that accumulates in-memory log entries while also forwarding them to the structured `ILogger` infrastructure.

| Field / Method | Type | Description |
|---|---|---|
| `Logs` | `IList<string>` | Ordered collection of formatted log entries |
| `LogInfo(message)` | `void` | Appends `INFO [UTC timestamp] - {message}` to `Logs` and calls `ILogger.LogInformation` |
| `LogWarning(message)` | `void` | Appends `WARNING [UTC timestamp] - {message}` to `Logs` and calls `ILogger.LogWarning` |
| `LogError(message)` | `void` | Appends `ERROR [UTC timestamp] - {message}` to `Logs` and calls `ILogger.LogError` |
| `AddRange(entries)` | `void` | Merges external log entries (e.g., from `DataMappingContext.Logs`) into `Logs` |

**Construction**: Takes `ILogger` (the existing `ILogger<AasGenerator>` from DI).

**Lifecycle**: One instance per blueprint processing within `AddDataToAasAsync`. Created at the start of the per-blueprint lambda, passed through each Try* method.

**Relationship to DataMappingContext.Logs**: After the mapping step completes (success or failure), `DataMappingContext.Logs` entries are merged into `WorkflowLogger.Logs` via `AddRange`. The DataMappingContext continues to manage its own logs independently during pipeline execution.

## Modified Entities

### AasGeneratorResult (existing)

No structural changes. Existing properties used differently:

| Property | Current Behavior | New Behavior |
|---|---|---|
| `DebugInfo.Logs` | Only populated from DataMapper logs on success with `debug=true` | Populated from `WorkflowLogger.Logs` (which includes DataMapper logs) on success with `debug=true` |
| `ErrorInfo.Logs` | Only populated on `SubmodelDataToInstanceMapperException` | Populated from `WorkflowLogger.Logs` on **any** error |
| `ErrorInfo.Qualifier` | Populated on mapping error | Unchanged — still only populated on mapping error |
| `ErrorInfo.QualifierPath` | Populated on mapping error | Unchanged — still only populated on mapping error |

### AasGeneratorErrorInfo (existing)

No structural changes. `Logs` field will now be populated for all error types, not just mapping errors.

### AasGeneratorDebugInfo (existing)

No structural changes. `Logs` field will now contain the full workflow log trail instead of only DataMapper logs.

### DataMappingContext (existing)

**No changes.** The DataMappingContext and its `Logs` property remain untouched. The integration point is at the AasGenerator level, which reads `context.Logs` after mapping and merges them into the `WorkflowLogger`.

## Entity Relationships

```
AddDataToAasAsync (per blueprint)
  └── WorkflowLogger (1 per blueprint)
        ├── LogInfo/LogWarning/LogError entries from:
        │     ├── TryGetBlueprintFromBlueprintProviderAsync
        │     ├── TryGetIdShortFromBlueprint
        │     ├── TryGenerateSubmodelIdAsync
        │     ├── TryAddSubmodelToAasAsync
        │     └── (merged) DataMappingContext.Logs
        └── Feeds into:
              ├── AasGeneratorResult.DebugInfo.Logs (on success + debug=true)
              └── AasGeneratorResult.ErrorInfo.Logs (on any error)
```

## Log Entry Format Convention

All entries follow the existing format established by `DataMappingContext`:

```
SEVERITY [YYYY-MM-DDTHH:MM:SS.FFFFFFFZ] - Message text
```

Where:
- `SEVERITY` = `INFO`, `WARNING`, or `ERROR`
- Timestamp = `DateTime.UtcNow` in ISO 8601 format
- Message = free-form descriptive text

Examples:
```
INFO [2026-04-23T14:30:00.1234567Z] - Fetching blueprint: urn:example:nameplate
INFO [2026-04-23T14:30:00.2345678Z] - Blueprint fetched successfully
INFO [2026-04-23T14:30:00.2456789Z] - Generating submodel ID
INFO [2026-04-23T14:30:00.3456789Z] - Submodel ID generated: urn:example:sm-123
INFO [2026-04-23T14:30:00.3567890Z] - Starting data mapping
INFO [2026-04-23T14:30:00.3567890Z] - Started DeepCloneBlueprintStep  <-- from DataMappingContext
...
INFO [2026-04-23T14:30:00.4567890Z] - Data mapping completed
INFO [2026-04-23T14:30:00.4567890Z] - Posting submodel to repository
INFO [2026-04-23T14:30:00.5567890Z] - Submodel reference added to shell
```
