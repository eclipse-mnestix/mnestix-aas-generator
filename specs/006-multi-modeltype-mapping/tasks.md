# Tasks: Multi-Modeltype Mapping

**Input**: Design documents from `/specs/006-multi-modeltype-mapping/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Unit tests are REQUIRED per Constitution Principle IV (NON-NEGOTIABLE for AAS Generator changes).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No project setup needed — this is a modification to an existing codebase. Verify the baseline.

- [x] T001 Run `dotnet test` to confirm all existing tests pass as baseline in Core.Tests/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core refactoring of `MapDataToInstanceStep.cs` to support multi-field qualifier parsing. ALL user stories depend on this.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T002 Refactor qualifier query in `MapDataToInstance()` to match all qualifiers whose type starts with `SMT/MappingInfo` (instead of exact match `SMT/MappingInfo`) in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T003 Add field name parsing logic: split qualifier `type` on `/` to extract the target field name (3rd segment). Default to `value` when no 3rd segment exists (legacy format) in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T004 Add static field allowlist dictionary with field name → (applicable model types, field category) mapping in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T005 Add allowlist validation: reject qualifiers with unknown field names with a clear error message in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T006 Add model-type applicability validation: reject qualifiers targeting fields incompatible with the element's modelType in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T007 Add duplicate field detection: group all MappingInfo qualifiers per element by resolved target field and fail if duplicates exist in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T008 Refactor `AssignJsonValueToInstance()` to dispatch based on target field name instead of always writing to `value` in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T009 Run `dotnet test` to confirm all existing tests still pass after foundational refactoring (backwards compatibility gate)

**Checkpoint**: Foundation ready — qualifier parsing is multi-field-aware, legacy behavior preserved, validation in place.

---

## Phase 3: User Story 1 — Map Data to Entity globalAssetId (Priority: P1) 🎯 MVP

**Goal**: Enable `SMT/MappingInfo/globalAssetId` qualifier to populate an Entity's `globalAssetId` field from input data.

**Independent Test**: Blueprint Entity with `SMT/MappingInfo/globalAssetId` qualifier → generated Entity has correct `globalAssetId`.

### Tests for User Story 1

- [x] T010 [P] [US1] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldGlobalAssetId/TemplateSubmodel.json` with an Entity carrying `SMT/MappingInfo/globalAssetId` qualifier
- [x] T011 [P] [US1] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldGlobalAssetId/Data.json` with component asset ID data
- [x] T012 [P] [US1] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldGlobalAssetId/ExpectedResult.json` with Entity having `globalAssetId` populated
- [x] T013 [US1] Add test method `AddDataToAasAsync_InputMultiFieldGlobalAssetId_Success` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 1

- [x] T014 [US1] Add `globalAssetId` field assignment case in the dispatch logic: write Jsonata result to the element's `globalAssetId` JSON property in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T015 [US1] Run test `AddDataToAasAsync_InputMultiFieldGlobalAssetId_Success` to verify it passes

**Checkpoint**: Entity `globalAssetId` can be dynamically populated from input data.

---

## Phase 4: User Story 2 — Map Data to Element idShort (Priority: P1)

**Goal**: Enable `SMT/MappingInfo/idShort` qualifier to dynamically set any element's `idShort` with auto-sanitization.

**Independent Test**: Blueprint Entity with `SMT/MappingInfo/idShort` → generated Entity has sanitized `idShort` from data.

### Tests for User Story 2

- [x] T016 [P] [US2] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputMultiFieldIdShort/` with TemplateSubmodel.json, Data.json, ExpectedResult.json for basic idShort mapping
- [x] T017 [P] [US2] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputIdShortSanitization/` with TemplateSubmodel.json, Data.json, ExpectedResult.json containing a value with hyphens (e.g., `TE-Housing-123`) and expected sanitized output (`TE_Housing_123`)
- [x] T018 [US2] Add test methods `AddDataToAasAsync_InputMultiFieldIdShort_Success` and `AddDataToAasAsync_InputIdShortSanitization_Success` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 2

- [x] T019 [US2] Add `idShort` field assignment case in the dispatch logic: write Jsonata result to the element's `idShort` JSON property in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T020 [US2] Add idShort sanitization method: replace characters not matching `[a-zA-Z0-9_]` with `_`, prepend `i` if result starts with a digit, log warning when sanitization changes the value in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T021 [US2] Run tests `AddDataToAasAsync_InputMultiFieldIdShort_Success` and `AddDataToAasAsync_InputIdShortSanitization_Success` to verify they pass

**Checkpoint**: Element `idShort` can be dynamically set with AAS-conformant sanitization.

---

## Phase 5: User Story 3 — Backwards-Compatible Legacy MappingInfo (Priority: P1)

**Goal**: Confirm all existing blueprints with `SMT/MappingInfo` (no suffix) produce identical output.

**Independent Test**: All ~20 existing tests pass unchanged + explicit legacy test fixture.

### Tests for User Story 3

- [x] T022 [P] [US3] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputMultiFieldMappingLegacy/` with TemplateSubmodel.json using legacy `SMT/MappingInfo` alongside new-format qualifiers on different elements, Data.json, and ExpectedResult.json
- [x] T023 [US3] Add test method `AddDataToAasAsync_InputMultiFieldMappingLegacy_Success` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Verification for User Story 3

- [x] T024 [US3] Run full `dotnet test` suite to confirm zero regressions across all existing tests

**Checkpoint**: Legacy backwards compatibility fully verified.

---

## Phase 6: User Story 4 — Map Data to Additional Allowed Fields (Priority: P2)

**Goal**: Enable `entityType`, `displayName`, `first`, and `second` field mapping, plus allowlist rejection for invalid fields.

**Independent Test**: Blueprint Entity with `SMT/MappingInfo/entityType` → correct `entityType` populated; unknown field → error.

### Tests for User Story 4

- [x] T025 [P] [US4] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputMultiFieldEntityType/` with TemplateSubmodel.json (Entity with `SMT/MappingInfo/entityType`), Data.json, ExpectedResult.json
- [x] T026 [P] [US4] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputMultiFieldDisplayName/` with TemplateSubmodel.json (Entity with `SMT/MappingInfo/displayName` and pre-defined displayName MLP array), Data.json, ExpectedResult.json
- [x] T027 [P] [US4] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputMultiFieldRelationship/` with TemplateSubmodel.json (RelationshipElement with `SMT/MappingInfo/first` and `SMT/MappingInfo/second`), Data.json, ExpectedResult.json
- [x] T028 [P] [US4] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldInvalidField/TemplateSubmodel.json` with a qualifier `SMT/MappingInfo/notAllowedField` and `Core.Tests/AasGenerator/TestJsons/InputMultiFieldInvalidField/Data.json`
- [x] T029 [P] [US4] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldTypeMismatch/TemplateSubmodel.json` with `SMT/MappingInfo/globalAssetId` on a Property (field-modeltype mismatch) and `Core.Tests/AasGenerator/TestJsons/InputMultiFieldTypeMismatch/Data.json`
- [x] T030 [P] [US4] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputMultiFieldDuplicate/TemplateSubmodel.json` with both `SMT/MappingInfo` and `SMT/MappingInfo/value` on the same element and `Core.Tests/AasGenerator/TestJsons/InputMultiFieldDuplicate/Data.json`
- [x] T031 [US4] Add test methods in Core.Tests/AasGenerator/AasGeneratorTests.cs: `AddDataToAasAsync_InputMultiFieldEntityType_Success`, `AddDataToAasAsync_InputMultiFieldDisplayName_Success`, `AddDataToAasAsync_InputMultiFieldRelationship_Success`, `AddDataToAasAsync_InputMultiFieldInvalidField_ShouldFail`, `AddDataToAasAsync_InputMultiFieldTypeMismatch_ShouldFail`, `AddDataToAasAsync_InputMultiFieldDuplicate_ShouldFail`

### Implementation for User Story 4

- [x] T032 [US4] Add `entityType` field assignment case (simple string replacement) in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T033 [US4] Add `displayName` field assignment case: find matching language entry in the displayName MLP array and set its `text` field in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T034 [US4] Add `first` and `second` field assignment cases: replace the element's reference JSON object with the Jsonata result in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T035 [US4] Run all US4 tests to verify they pass

**Checkpoint**: All 7 allowlist fields are mappable. Validation errors are clear for invalid/mismatched/duplicate qualifiers.

---

## Phase 7: User Story 5 — Dynamic HierarchicalStructures from VEC Data (Priority: P2)

**Goal**: End-to-end integration: generate a complete HierarchicalStructures submodel with Entities and RelationshipElements from VEC input data.

**Independent Test**: HierarchicalStructures blueprint with collection + multi-field mapping → valid BOM submodel from VEC data.

### Tests for User Story 5

- [x] T036 [P] [US5] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputHierarchicalStructures/TemplateSubmodel.json` — HierarchicalStructures blueprint with an Entity template inside `SMT/CollectionMappingInfo` carrying `SMT/MappingInfo/idShort`, `SMT/MappingInfo/globalAssetId`, and `SMT/MappingInfo/entityType` qualifiers, plus a RelationshipElement template with `SMT/MappingInfo/first` and `SMT/MappingInfo/second`
- [x] T037 [P] [US5] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputHierarchicalStructures/Data.json` with VEC-derived component array data
- [x] T038 [P] [US5] Create test fixture `Core.Tests/AasGenerator/TestJsons/InputHierarchicalStructures/ExpectedResult.json` with the expected HierarchicalStructures submodel containing dynamically generated Entities and HasPart RelationshipElements
- [x] T039 [US5] Add test method `AddDataToAasAsync_InputHierarchicalStructures_Success` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Verification for User Story 5

- [x] T040 [US5] Run test `AddDataToAasAsync_InputHierarchicalStructures_Success` and verify the generated submodel matches the expected output

**Checkpoint**: Full HierarchicalStructures BOM generation from VEC data works end-to-end.

---

## Phase 8: User Story 6 — Value Type Validation (Priority: P2)

**Goal**: Validate mapped values against the element's declared `valueType` and raise errors on known-type mismatches.

**Independent Test**: Blueprint Property with `valueType: xs:integer` and non-numeric data → validation error.

### Tests for User Story 6

- [x] T041 [P] [US6] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputValueTypeValidationSuccess/` with TemplateSubmodel.json (Property with `valueType: xs:integer` and `SMT/MappingInfo/value`), Data.json (integer value), ExpectedResult.json
- [x] T042 [P] [US6] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputValueTypeValidationFailure/` with TemplateSubmodel.json (Property with `valueType: xs:integer` and `SMT/MappingInfo/value`), Data.json (string value)
- [x] T043 [P] [US6] Create test fixture directory `Core.Tests/AasGenerator/TestJsons/InputValueTypeUnknown/` with TemplateSubmodel.json (Property with `valueType: xs:customType` and `SMT/MappingInfo/value`), Data.json, ExpectedResult.json (value passes through)
- [x] T044 [US6] Add test methods `AddDataToAasAsync_InputValueTypeValidationSuccess_Success`, `AddDataToAasAsync_InputValueTypeValidationFailure_ShouldFail`, `AddDataToAasAsync_InputValueTypeUnknown_Success` in Core.Tests/AasGenerator/AasGeneratorTests.cs

### Implementation for User Story 6

- [x] T045 [US6] Add valueType validation method with static known-type lookup table (xs:string, xs:boolean, xs:integer, xs:int, xs:long, xs:short, xs:decimal, xs:double, xs:float, xs:dateTime, xs:date, xs:anyURI) in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T046 [US6] Integrate valueType validation call into `value` field assignment path: validate after Jsonata evaluation, before assignment in MnestixCore/AASGenerator/SubmodelDataToInstanceMapper/Steps/MapDataToInstanceStep.cs
- [x] T047 [US6] Run all US6 tests to verify they pass

**Checkpoint**: Value type validation catches type mismatches and warns on unknown types.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, final validation, and code quality

- [x] T048 [P] Update wiki/Blueprints-and-Rules.md with documentation for the new `SMT/MappingInfo/<FieldName>` qualifier format, supported fields table, examples, and error conditions
- [x] T049 [P] Run quickstart.md validation: verify the example in specs/006-multi-modeltype-mapping/quickstart.md works against the implementation
- [x] T050 Run full `dotnet test` suite as final regression check
- [x] T051 Run `dotnet build` and confirm zero compiler warnings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — baseline verification
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **User Stories (Phases 3–8)**: All depend on Phase 2 completion
  - **US1 (Phase 3)** and **US2 (Phase 4)**: Can proceed in parallel after Phase 2
  - **US3 (Phase 5)**: Can proceed in parallel — just verification of existing behavior
  - **US4 (Phase 6)**: Can proceed after Phase 2 (independent of US1/US2/US3)
  - **US5 (Phase 7)**: Depends on US1 + US2 + US4 (integration of all field types)
  - **US6 (Phase 8)**: Can proceed after Phase 2 (independent of US1-US5)
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

```
Phase 2 (Foundational) ──┬──→ US1 (globalAssetId) ──┐
                         ├──→ US2 (idShort)     ────┤
                         ├──→ US3 (Legacy compat)    ├──→ US5 (HierarchicalStructures E2E)
                         ├──→ US4 (Additional fields)┘
                         └──→ US6 (ValueType validation) ──→ Phase 9 (Polish)
```

### Within Each User Story

1. Create test fixtures first (can be parallel within a story)
2. Add test methods
3. Implement the feature code
4. Run tests to verify

### Parallel Opportunities

**Within Phase 2**: T002–T008 are sequential (each builds on the previous refactoring)
**Within Phase 3–8**: Test fixture creation tasks marked [P] can run in parallel within each story
**Across stories**: US1, US2, US3, US4, US6 can all proceed in parallel after Phase 2

---

## Implementation Strategy

### MVP Scope

**User Story 1 (globalAssetId)** is the MVP. After completing Phase 2 + Phase 3, the system can map Entity `globalAssetId` from data — the single most valuable capability for the HierarchicalStructures use case.

### Incremental Delivery

1. **Phase 1–2**: Foundation (qualifier parsing, validation, dispatch)
2. **Phase 3**: MVP — globalAssetId mapping
3. **Phase 4**: idShort mapping with sanitization
4. **Phase 5**: Backwards compatibility confirmation
5. **Phase 6**: Remaining fields (entityType, displayName, first, second)
6. **Phase 7**: End-to-end HierarchicalStructures integration
7. **Phase 8**: Value type validation
8. **Phase 9**: Documentation and polish
