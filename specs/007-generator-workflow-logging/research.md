# Research: Generator Workflow Logging

**Feature**: 007-generator-workflow-logging  
**Date**: 2026-04-23

## Research Question 1: Use Global ILogger vs ServiceProvider Pattern

### Context

The user asked whether the workflow-level logging should use the `ILogger<AasGenerator>` already injected in the constructor, or adopt the `IServiceProvider` pattern used by `DataMapper` to resolve a dedicated logger category.

### Current Patterns

**AasGenerator (global logger):**
- Constructor-injected `ILogger<AasGenerator>` — already present.
- Used only in `catch` blocks for `_logger.LogError(...)`.
- No in-memory log accumulation; error messages go into `AasGeneratorResult.Message`.

**DataMapper (ServiceProvider pattern):**
- Injects `IServiceProvider` in constructor.
- Per-call: resolves `ILogger<DataMappingContext>` and passes it to a new `DataMappingContext`.
- `DataMappingContext.LogInfo()/LogWarning()` dual-writes to both `IList<string> Logs` and `ILogger`.
- The DataMapper needs this pattern because `DataMappingContext` is instantiated per-call with its own logger category, and pipeline steps (parameterless via `Activator.CreateInstance`) cannot receive injected dependencies.

### Option A: Use Existing ILogger<AasGenerator> (RECOMMENDED)

Create a lightweight `WorkflowLogger` helper that holds an `IList<string>` and wraps calls to the already-injected `_logger`. No new DI registrations needed.

```
// Pseudo-code
var workflowLogs = new WorkflowLogger(_logger);
workflowLogs.LogInfo("Fetching blueprint...");
// _logger.LogInformation also called internally
```

**Pros:**
- Simplest approach — no DI changes, no `IServiceProvider` injection.
- `ILogger<AasGenerator>` is already wired up and matches the class responsibility.
- All workflow-level log entries share the same logger category (`AasGenerator`), which is correct — these are generator-level concerns, not data-mapping concerns.
- Follows constitution Principle VI (avoid unnecessary abstractions).

**Cons:**
- Logger category is `AasGenerator` rather than a dedicated `WorkflowLogger` category. This is fine since the logs are generator-level.

### Option B: Inject IServiceProvider and Resolve Per-Call Logger

Add `IServiceProvider` to `AasGenerator` constructor. Resolve `ILogger<WorkflowContext>` per call, creating a context similar to `DataMappingContext`.

**Pros:**
- Separate logger category for filtering in structured logging systems.

**Cons:**
- Adds `IServiceProvider` dependency to `AasGenerator` (service locator anti-pattern).
- More complex than needed — the DataMapper only uses this pattern because pipeline steps are parameterless and need a context object to carry state. The generator's workflow steps are private methods that can accept parameters directly.
- Violates constitution Principle VI (avoid unnecessary abstractions).

### Decision: Option A — Use Existing ILogger<AasGenerator>

**Rationale:** AasGenerator already has the correct logger injected. The workflow logging is a generator-level concern. Adding ServiceProvider injection would introduce unnecessary complexity for zero functional benefit. The dual-write pattern (in-memory list + ILogger) can be implemented with a simple helper class that takes the existing `ILogger<AasGenerator>`.

**Alternative considered:** Option B was rejected because it introduces the service locator anti-pattern and an unnecessary architectural layer for what is fundamentally a simple list accumulation.

---

## Research Question 2: How to Merge DataMapper Logs into Workflow Logs

### Context

The DataMapper pipeline produces its own `IList<string> Logs` via `DataMappingContext`. These logs need to be integrated into the overall workflow log trail.

### Options

**Option A: Merge after mapping (RECOMMENDED)**
After `TryMapDataToInstance` returns successfully, append `context.Logs` into the workflow log list. This preserves the existing DataMapper behavior untouched.

**Option B: Pass workflow log list into DataMappingContext**
Modify `DataMappingContext` to accept an external `IList<string>` and write directly into it. This couples the data mapping layer to the workflow logging layer.

### Decision: Option A — Post-mapping merge

**Rationale:** Minimal change to existing code. The DataMapper and its pipeline steps continue to work exactly as before. The workflow logger simply absorbs their output after the mapping step completes. On mapping error, the exception already carries `Context?.Logs` which can be merged in the catch handler.

---

## Research Question 3: Where to Put the WorkflowLogger Class

### Options

- `MnestixCore/AASGenerator/WorkflowLogger.cs` — alongside `AASGenerator.cs`
- `MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/` — alongside DataMappingContext

### Decision: `MnestixCore/AASGenerator/WorkflowLogger.cs`

**Rationale:** The workflow logger is a generator-level concern, not a data-mapping concern. It lives at the same level as `AasGenerator` itself. Follows existing folder conventions.

---

## Research Question 4: How Error Results Should Carry Workflow Logs

### Current Behavior

- **Blueprint fetch error**: `AasGeneratorResult` with `Message` only — no logs.
- **ID generation error**: `AasGeneratorResult` with `Message` only — no logs.
- **Mapping error**: `AasGeneratorResult` with `Message`, `ErrorInfo.Logs` (from DataMappingContext), and optionally `DebugInfo.Logs`.
- **Repo persistence error**: `AasGeneratorResult` with `Message` only — no logs.

### Proposed Behavior

All error results should include the accumulated workflow logs in `ErrorInfo.Logs` (always on error) and `DebugInfo.Logs` (when debug=true). The `ErrorInfo.Qualifier` and `ErrorInfo.QualifierPath` fields remain mapping-specific and are only populated on mapping errors.

This is backward-compatible: `ErrorInfo.Logs` was previously only populated on mapping errors. Non-mapping errors had `null` ErrorInfo. Now all errors will have `ErrorInfo` with at least the workflow logs.

### Decision: Populate ErrorInfo.Logs on all error types

All Try* methods will accept and populate the workflow log list. On any error, the accumulated logs are placed in `ErrorInfo.Logs`. The `Qualifier`/`QualifierPath` fields remain null for non-mapping errors, which is a natural extension of the existing schema.
