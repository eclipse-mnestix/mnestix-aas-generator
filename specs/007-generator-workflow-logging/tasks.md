# Tasks: Generator Workflow Logging

**Input**: Design documents from `/specs/007-generator-workflow-logging/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Required by Constitution Principle IV (NON-NEGOTIABLE for AASGenerator changes).

**Organization**: Tasks grouped by user story. US1 and US2 are both P1 but share the same implementation surface — they are organized sequentially since US2 builds on the same code paths as US1.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `MnestixCore/AASGenerator/`
- **Tests**: `Core.Tests/AasGenerator/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new WorkflowLogger helper class that all user stories depend on

- [X] T001 Create WorkflowLogger class with LogInfo, LogWarning, LogError, and AddRange methods in MnestixCore/AASGenerator/WorkflowLogger.cs
- [X] T002 [P] Create WorkflowLogger unit tests verifying log format, dual-write to ILogger, and AddRange merge behavior in Core.Tests/AasGenerator/WorkflowLoggerTests.cs

---

## Phase 2: User Story 1 — Full Workflow Logs on Debug Request (Priority: P1) MVP

**Goal**: When `debug=true`, the response includes a complete chronological log trail spanning all four workflow phases for each blueprint.

**Independent Test**: Send a request with `debug=true`, verify the successful response contains log entries from blueprint retrieval, ID generation, data mapping (including existing DataMapper pipeline logs), and repository persistence.

### Tests for User Story 1

- [X] T003 [US1] Add test: successful generation with `debug=true` returns DebugInfo.Logs containing entries from all four workflow phases in Core.Tests/AasGenerator/AasGeneratorTests.cs
- [X] T004 [US1] Add test: successful generation with `debug=false` returns null DebugInfo (no logs) in Core.Tests/AasGenerator/AasGeneratorTests.cs
- [X] T005 [US1] Add test: multiple blueprints with `debug=true` returns independent log trails per blueprint in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 1

- [X] T006 [US1] Modify AddDataToAasAsync to create a WorkflowLogger instance per blueprint at the start of the per-blueprint lambda in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T007 [US1] Add workflow log entries to TryGetBlueprintFromBlueprintProviderAsync (start/success) and pass WorkflowLogger parameter in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T008 [US1] Add workflow log entries to TryGetIdShortFromBlueprint (extracted idShort) and pass WorkflowLogger parameter in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T009 [US1] Add workflow log entries to TryGenerateSubmodelIdAsync (start/generated ID) and pass WorkflowLogger parameter in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T010 [US1] Add workflow log entries around TryMapDataToInstance (start/end) and merge DataMappingContext.Logs into WorkflowLogger via AddRange in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T011 [US1] Add workflow log entries to TryAddSubmodelToAasAsync (post submodel/post reference) and pass WorkflowLogger parameter in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T012 [US1] Populate AasGeneratorResult.DebugInfo.Logs from WorkflowLogger.Logs on success when debug=true in MnestixCore/AASGenerator/AASGenerator.cs

**Checkpoint**: Debug responses now contain full workflow logs. Existing DataMapper pipeline logs are preserved within the trail.

---

## Phase 3: User Story 2 — Workflow Logs on Error (Priority: P1)

**Goal**: When any workflow phase fails, the error result includes all accumulated log entries up to and including the failure — regardless of the debug flag.

**Independent Test**: Trigger failures at each workflow stage and verify ErrorInfo.Logs contains the log trail up to that point.

### Tests for User Story 2

- [X] T013 [US2] Add test: blueprint fetch failure returns ErrorInfo.Logs with retrieval attempt entry in Core.Tests/AasGenerator/AasGeneratorTests.cs
- [X] T014 [US2] Add test: ID generation failure returns ErrorInfo.Logs with blueprint success + ID failure entries in Core.Tests/AasGenerator/AasGeneratorTests.cs
- [X] T015 [US2] Add test: data mapping failure returns ErrorInfo.Logs with preceding steps + mapping error, and preserves existing ErrorInfo.Qualifier/QualifierPath behavior in Core.Tests/AasGenerator/AasGeneratorTests.cs
- [X] T016 [US2] Add test: repo persistence failure returns ErrorInfo.Logs with all preceding step entries in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 2

- [X] T017 [US2] Modify TryGetBlueprintFromBlueprintProviderAsync to log error entry and attach WorkflowLogger.Logs to ErrorInfo.Logs on failure in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T018 [US2] Modify TryGetIdShortFromBlueprint to attach WorkflowLogger.Logs to ErrorInfo.Logs on missing idShort in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T019 [US2] Modify TryGenerateSubmodelIdAsync to log error entry and attach WorkflowLogger.Logs to ErrorInfo.Logs on failure in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T020 [US2] Modify TryMapDataToInstance to merge DataMappingContext.Logs from exception into WorkflowLogger, then attach WorkflowLogger.Logs to ErrorInfo.Logs on failure in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T021 [US2] Modify TryAddSubmodelToAasAsync to log error entry and attach WorkflowLogger.Logs to ErrorInfo.Logs on failure (both validation and RepoProxyException) in MnestixCore/AASGenerator/AASGenerator.cs
- [X] T022 [US2] Ensure DebugInfo.Logs is also populated from WorkflowLogger.Logs on error when debug=true in MnestixCore/AASGenerator/AASGenerator.cs

**Checkpoint**: All error paths now include workflow logs. Mapping errors preserve existing Qualifier/QualifierPath alongside the new workflow log trail.

---

## Phase 4: User Story 3 — Consistent Log Format (Priority: P2)

**Goal**: All workflow log entries use the same `SEVERITY [timestamp] - message` format established by DataMappingContext.

**Independent Test**: Send a debug request and verify every log entry matches the format pattern `^(INFO|WARNING|ERROR) \[.+\] - .+$`.

### Tests for User Story 3

- [X] T023 [US3] Add test: all log entries in a debug response match the format pattern `SEVERITY [timestamp] - message` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 3

> Note: If T001 (WorkflowLogger) correctly implements the format in LogInfo/LogWarning/LogError methods, this is already satisfied. T023 serves as the validation.

- [X] T024 [US3] Verify WorkflowLogger format matches DataMappingContext format (same severity prefixes, same timestamp format) in MnestixCore/AASGenerator/WorkflowLogger.cs

**Checkpoint**: All log entries — both from the workflow layer and the DataMapper pipeline — follow identical formatting conventions.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [X] T025 Run all existing tests via `dotnet test` to verify no regressions in Core.Tests/
- [X] T026 Run quickstart.md validation: manually verify response format matches documented examples in specs/007-generator-workflow-logging/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — can start immediately
  - T001 (WorkflowLogger class) BLOCKS all Phase 2+ implementation tasks
  - T002 (WorkflowLogger tests) can run in parallel with T001
- **Phase 2 (US1)**: Depends on T001 completion
- **Phase 3 (US2)**: Depends on Phase 2 (builds on same code paths with WorkflowLogger already wired in)
- **Phase 4 (US3)**: Depends on T001 (verifies format); can run in parallel with Phase 2/3 test tasks
- **Phase 5 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Depends only on T001 (WorkflowLogger). Core implementation.
- **US2 (P1)**: Depends on US1 implementation (WorkflowLogger already passed to Try* methods). Adds error-path log propagation.
- **US3 (P2)**: Depends on T001. Format validation — no additional code if WorkflowLogger format is correct.

### Within Each User Story

- Tests written first (fail before implementation)
- Workflow logger creation in AddDataToAasAsync before per-method changes
- Per-method changes can be done in any order within a phase
- Result population after all per-method changes

### Parallel Opportunities

Within Phase 1:
- T001 and T002 can be developed in parallel (test against expected interface)

Within Phase 2 (after T006):
- T007, T008, T009, T010, T011 modify independent private methods — can be done in parallel

Within Phase 3 (after Phase 2):
- T017, T018, T019, T020, T021 modify independent error paths — can be done in parallel

---

## Parallel Example: Phase 2 Implementation

```
# After T006 (WorkflowLogger wired into AddDataToAasAsync):

# These modify independent methods and can run in parallel:
Task T007: "Add log entries to TryGetBlueprintFromBlueprintProviderAsync"
Task T008: "Add log entries to TryGetIdShortFromBlueprint"
Task T009: "Add log entries to TryGenerateSubmodelIdAsync"
Task T010: "Add log entries around TryMapDataToInstance + merge"
Task T011: "Add log entries to TryAddSubmodelToAasAsync"

# Then T012 populates the result (depends on T007-T011):
Task T012: "Populate DebugInfo.Logs from WorkflowLogger"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Create WorkflowLogger (T001-T002)
2. Complete Phase 2: Wire up debug logging (T003-T012)
3. **STOP and VALIDATE**: Run tests, verify debug responses contain full workflow logs
4. This alone delivers the primary value

### Incremental Delivery

1. Phase 1 → WorkflowLogger ready
2. Phase 2 (US1) → Debug responses have full logs → **MVP!**
3. Phase 3 (US2) → Error responses have full logs → **Complete error diagnostics**
4. Phase 4 (US3) → Format consistency verified → **Polish**
5. Phase 5 → Regression check → **Ship**
