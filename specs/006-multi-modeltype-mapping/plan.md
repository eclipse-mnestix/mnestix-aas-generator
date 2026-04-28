# Implementation Plan: Multi-Modeltype Mapping

**Branch**: `ALS-49-aas-generator-multi-modeltype-mapping` | **Date**: 2026-04-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-multi-modeltype-mapping/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Extend the `SMT/MappingInfo` qualifier to support mapping input data into model-type-specific fields beyond just `value`. The new qualifier format `SMT/MappingInfo/<FieldName>` allows populating `idShort`, `globalAssetId`, `entityType`, `displayName`, `first`, and `second` fields on submodel elements. This enables dynamic creation of HierarchicalStructures submodels from VEC input data. The change is localized primarily to `MapDataToInstanceStep`, with a new field resolution and validation layer. Legacy `SMT/MappingInfo` (without suffix) remains fully backwards-compatible.

## Technical Context

**Language/Version**: C# / .NET 8 (LTS), nullable reference types enabled  
**Primary Dependencies**: Jsonata.Net.Native (expression evaluation), Newtonsoft.Json (AAS serialization), BaSyx v2 REST API (repo integration)  
**Storage**: N/A (stateless transformation pipeline; persistence via BaSyx repo proxy)  
**Testing**: NUnit + FluentAssertions + Moq, run via `dotnet test`, fixture-based (JSON triplets: Template → Data → ExpectedResult)  
**Target Platform**: Linux Docker container (.NET 8 runtime)  
**Project Type**: Web service (ASP.NET Core API) with internal rules engine library  
**Performance Goals**: No regression — current 10k-element test is the benchmark  
**Constraints**: Pipeline steps must be stateless; all mutable state in `DataMappingContext`  
**Scale/Scope**: Single pipeline step modification + new validation logic; ~20 existing tests must pass unchanged

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. AAS Specification Conformance** | ✅ PASS | idShort sanitization (FR-017) ensures conformance. Field allowlist restricts to valid AAS metamodel fields only. |
| **II. Deterministic Generation** | ✅ PASS | No non-deterministic behavior introduced. Qualifier processing order is document-order (deterministic). |
| **III. Backwards Compatibility** | ✅ PASS | Legacy `SMT/MappingInfo` treated as `SMT/MappingInfo/value` (FR-002). All existing tests must pass unchanged (SC-001). |
| **IV. Unit Testing** | ✅ PASS | New test fixtures required for each new field mapping. Existing tests remain unmodified. |
| **V. Open Source & Community** | ✅ PASS | No new dependencies. Docs must be updated (wiki/Blueprint-and-Rules.md). |
| **VI. Pipeline Extensibility & Simplicity** | ✅ PASS | No new pipeline steps added. Change is within existing `MapDataToInstanceStep`. No new abstractions. |
| **VII. Security by Default** | ✅ PASS | Field allowlist prevents arbitrary field injection. Jsonata expressions already sandboxed. |

## Project Structure

### Documentation (this feature)

```text
specs/006-multi-modeltype-mapping/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
MnestixCore/
├── AASGenerator/
│   └── SubmodelDataToInstanceMapper/
│       ├── DataMapper.cs                          # Pipeline orchestration (unchanged)
│       ├── DataMappingContext.cs                   # Context object (unchanged)
│       └── Steps/
│           └── MapDataToInstanceStep.cs            # PRIMARY CHANGE: multi-field qualifier parsing & dispatch
├── Shared/
│   └── Pipeline/                                  # Pipeline infrastructure (unchanged)
└── Errors/
    └── SubmodelDataToInstanceMapperException.cs    # Error type (unchanged)

Core.Tests/
├── AasGenerator/
│   ├── AasGeneratorTests.cs                       # Existing tests (unchanged, must pass)
│   └── TestJsons/
│       ├── InputMultiFieldGlobalAssetId/            # NEW: globalAssetId mapping test
│       ├── InputMultiFieldIdShort/                 # NEW: idShort mapping test
│       ├── InputIdShortSanitization/               # NEW: idShort auto-sanitize test
│       ├── InputMultiFieldMappingLegacy/           # NEW: backwards compatibility test
│       ├── InputMultiFieldEntityType/              # NEW: entityType mapping test
│       ├── InputMultiFieldDisplayName/             # NEW: displayName MLP mapping test
│       ├── InputMultiFieldRelationship/            # NEW: first/second reference mapping test
│       ├── InputMultiFieldDuplicate/               # NEW: duplicate field error test
│       ├── InputMultiFieldInvalidField/            # NEW: allowlist rejection test
│       ├── InputMultiFieldTypeMismatch/            # NEW: field-modeltype mismatch error test
│       ├── InputValueTypeValidationSuccess/        # NEW: valueType validation pass test
│       ├── InputValueTypeValidationFailure/        # NEW: valueType validation fail test
│       ├── InputValueTypeUnknown/                  # NEW: unknown valueType pass-through test
│       └── InputHierarchicalStructures/            # NEW: end-to-end HierarchicalStructures test

wiki/
└── Blueprint-and-Rules.md                          # UPDATED: document new SMT/MappingInfo/<FieldName> syntax
```

**Structure Decision**: No new files or architectural layers needed in production code. All changes are within the existing `MapDataToInstanceStep.cs`. Test coverage is added via new JSON fixture directories following the established pattern.

## Complexity Tracking

No constitution violations. All 7 principles pass. No justifications needed.
