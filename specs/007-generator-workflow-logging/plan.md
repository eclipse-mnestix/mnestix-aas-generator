# Implementation Plan: Generator Workflow Logging

**Branch**: `ALS-62-aas-generation-logs-in-der-erstellten-aas-hinzufugen` | **Date**: 2026-04-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-generator-workflow-logging/spec.md`

## Summary

Extend the AAS Generator's `AddDataToAasAsync` workflow to accumulate in-memory log entries across all four phases (blueprint retrieval, ID generation, data mapping, repository persistence). Use the existing constructor-injected `ILogger<AasGenerator>` with a lightweight `WorkflowLogger` helper for dual-write (in-memory list + structured ILogger). Merge DataMapper pipeline logs into the workflow trail post-mapping. Return logs in `DebugInfo` (on success + debug=true) and `ErrorInfo` (on any error).

## Technical Context

**Language/Version**: C# / .NET 8 (LTS), nullable reference types enabled  
**Primary Dependencies**: Newtonsoft.Json (AAS serialization), Jsonata.Net.Native (expression evaluation), BaSyx v2 REST API (repo integration)  
**Storage**: N/A (no storage changes)  
**Testing**: NUnit + FluentAssertions + Moq via `dotnet test`  
**Target Platform**: Linux server (Docker)  
**Project Type**: Web service (ASP.NET Core)  
**Performance Goals**: N/A (negligible overhead — string list accumulation)  
**Constraints**: No DI changes, no API contract breaking changes  
**Scale/Scope**: 1–20 blueprints per request typical; 4 workflow phases per blueprint

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. AAS Specification Conformance | **PASS** | No change to generated AAS/Submodel output. Logging is response-level only. |
| II. Deterministic Generation | **PASS** | Logs contain timestamps (non-deterministic) but are in `DebugInfo`/`ErrorInfo` only — not in the generated Submodel. Generated AAS output remains byte-identical. |
| III. Backwards Compatibility | **PASS** | No DTO structural changes. `ErrorInfo.Logs` was already `IList<string>?` — populating it for non-mapping errors is additive. |
| IV. Unit Testing (NON-NEGOTIABLE) | **REQUIRED** | New `WorkflowLogger` class needs unit tests. Modified `AasGenerator` behavior needs tests verifying log presence in debug/error results. |
| V. Open Source & Community First | **PASS** | No new dependencies. |
| VI. Pipeline Extensibility & Simplicity | **PASS** | `WorkflowLogger` is a simple helper, not a new architectural layer. No pipeline changes. DataMappingContext untouched. |
| VII. Security by Default | **PASS** | Logs contain blueprint IDs and step descriptions — no secrets, no user data. |

**Pre-Phase 0 Gate**: PASS  

### Post-Phase 1 Re-check

| Principle | Status | Notes |
|---|---|---|
| IV. Unit Testing | **PASS** | Test plan includes: WorkflowLogger unit tests, AasGenerator integration tests for debug/error log propagation. |
| VI. Simplicity | **PASS** | Single new file (`WorkflowLogger.cs`). No abstractions, no patterns, no DI changes. |

**Post-Phase 1 Gate**: PASS

## Project Structure

### Documentation (this feature)

```text
specs/007-generator-workflow-logging/
├── plan.md              # This file
├── research.md          # Phase 0: ILogger vs ServiceProvider decision
├── data-model.md        # Phase 1: WorkflowLogger entity + modified DTO usage
├── quickstart.md        # Phase 1: Usage examples
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
MnestixCore/
└── AASGenerator/
    ├── AASGenerator.cs              # MODIFY: Add WorkflowLogger usage in AddDataToAasAsync + all Try* methods
    ├── AasGeneratorResult.cs        # NO CHANGE (DTOs already support IList<string> logs)
    ├── WorkflowLogger.cs            # NEW: Lightweight dual-write logger helper
    └── SubmodelDataToInstanceMapper/
        ├── DataMapper.cs            # NO CHANGE
        └── DataMappingContext.cs    # NO CHANGE

Core.Tests/
└── AasGenerator/
    ├── AasGeneratorTests.cs         # MODIFY: Add test cases for workflow logging
    └── WorkflowLoggerTests.cs       # NEW: Unit tests for WorkflowLogger
```

**Structure Decision**: Follows existing project layout. `WorkflowLogger.cs` lives alongside `AASGenerator.cs` in `MnestixCore/AASGenerator/` since it's a generator-level concern. No new directories needed.

## Complexity Tracking

No constitution violations. No complexity justifications needed.
