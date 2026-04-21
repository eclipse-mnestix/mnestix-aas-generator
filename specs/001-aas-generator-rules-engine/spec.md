# Feature Specification: AAS Generator — Rules Engine & Data Ingest

**Feature Branch**: `dev`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: Specify existing AAS Generator functionality (rules engine, data ingest pipeline) as currently implemented on `dev` branch.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Generate Submodels from Structured Data (Priority: P1)

An application developer or industrial engineer sends structured JSON data together with one or more blueprint IDs to the Data Ingest endpoint. The AAS Generator transforms the data into AAS-compliant Submodel instances by evaluating the mapping rules embedded in each blueprint, then persists the generated submodels in the connected AAS repository under the specified AAS.

**Why this priority**: This is the core value proposition — automated, deterministic creation of standardised digital-twin data from arbitrary structured input. Without this, the generator has no purpose.

**Independent Test**: Can be fully tested by POSTing a JSON payload with a valid blueprint ID to `POST /api/v2/DataIngest/{base64EncodedAasId}` and verifying the generated Submodel in the repository matches the expected output.

**Acceptance Scenarios**:

1. **Given** a blueprint with `SMT/MappingInfo` path qualifiers exists in the repository, and an AAS shell with a known ID exists, **When** the user POSTs valid JSON data to `/api/v2/DataIngest/{base64EncodedAasId}` with that blueprint ID, **Then** the system returns a success response containing the generated submodel ID, and the submodel is persisted in the repository under the specified AAS with `kind: "Instance"` and all mapped values populated.

2. **Given** a blueprint with `SMT/CollectionMappingInfo` qualifiers referencing an array path (e.g., `contacts[*]`), **When** the user POSTs JSON data containing an array of 3 items at that path, **Then** the system duplicates the collection element 3 times (e.g., `contactPerson_0`, `contactPerson_1`, `contactPerson_2`) with each copy's child values mapped from the corresponding array item.

3. **Given** a blueprint with `SMT/FilterMappingInfo` containing a boolean expression (e.g., `car.engineType = 'electric'`), **When** the user POSTs data where the condition evaluates to `false`, **Then** the filtered element is excluded from the generated Submodel instance.

4. **Given** a blueprint with a `MultiLanguageProperty` element and a mapping qualifier, **When** the user POSTs data with `language: "de"`, **Then** the generated element contains the mapped value under the `"de"` language tag.

5. **Given** a blueprint with a `SMT/Cardinality` qualifier set to `"One"` (mandatory) on an element, **When** the user POSTs data that does not contain the referenced field, **Then** the system returns an error indicating the mandatory field is missing.

6. **Given** a blueprint with a `SMT/Cardinality` qualifier set to `"ZeroToOne"` (optional) on an element, **When** the user POSTs data that does not contain the referenced field, **Then** the element is included in the output with an empty value.

7. **Given** a blueprint with a `SMT/MappingInfo` qualifier containing a Jsonata expression (e.g., `$uppercase(car.code)`), **When** the user POSTs data with `car.code = "abc"`, **Then** the generated value is `"ABC"`.

---

### User Story 2 — Create AAS with Auto-Generated Submodels (Priority: P2)

An application developer creates a new AAS shell for a given asset identifier and optionally triggers submodel generation in a single API call. This combines AAS creation and data ingest into one atomic operation.

**Why this priority**: Streamlines the most common workflow — creating a complete digital twin in one call rather than two separate requests.

**Independent Test**: Can be fully tested by POSTing to `POST /api/v2/AasCreator/{assetIdShort}` with blueprint IDs and data, then verifying both the AAS shell and generated submodels exist in the repository.

**Acceptance Scenarios**:

1. **Given** no AAS exists for asset `machine-001`, **When** the user POSTs to `/api/v2/AasCreator/machine-001` without a request body, **Then** the system creates an AAS shell with deterministically generated IDs (assetId, aasId, assetIdShort, aasIdShort) based on the configured ID generation settings, and returns the IDs (including Base64-encoded variants) and repository URL.

2. **Given** no AAS exists for asset `machine-001`, **When** the user POSTs to `/api/v2/AasCreator/machine-001` with blueprint IDs and data, **Then** the system creates the AAS shell AND generates submodels for each blueprint, returning submodel generation results alongside the AAS IDs.

3. **Given** an AAS already exists for asset `machine-001`, **When** the user POSTs to `/api/v2/AasCreator/machine-001`, **Then** the system returns a 400 Bad Request error indicating the AAS already exists.

---

### User Story 3 — Debug Generation Pipeline (Priority: P3)

A developer or asset manager troubleshoots a failing or incorrect generation by enabling debug mode, which returns detailed pipeline logs alongside the generation result.

**Why this priority**: Essential for diagnosing mapping issues in complex blueprints, but not required for core generation functionality.

**Independent Test**: Can be tested by sending a Data Ingest request with `debug: true` and verifying the response includes pipeline step logs.

**Acceptance Scenarios**:

1. **Given** a valid blueprint and data, **When** the user POSTs to the Data Ingest endpoint with `debug: true`, **Then** the response includes a `debugInfo` object containing logs from each pipeline step.

2. **Given** a blueprint with an invalid Jsonata expression in a mapping qualifier, **When** the user POSTs data with `debug: true`, **Then** the response includes an `errorInfo` object with the failing qualifier, its path, and the pipeline logs leading up to the failure.

---

### Edge Cases

- What happens when a blueprint ID does not exist in the repository? The system returns a structured error in the per-blueprint result.
- What happens when the provided JSON data is malformed (not valid JSON)? The API returns a 400 Bad Request before entering the pipeline.
- What happens when a Jsonata expression references a path that does not exist in the data? The pipeline applies cardinality rules: mandatory fields error, optional fields produce empty values.
- What happens when duplicate collection mapping references nested arrays? The system processes shallowest-first, recursively expanding inner collections.
- What happens when elements like `Range`, `Blob`, `File`, `ReferenceElement`, or `Entity` appear in the blueprint? They are copied unchanged into the generated instance without mapping.
- What happens when multiple blueprints are provided in a single request and one fails? Each blueprint is processed independently; failures are reported per-blueprint while successful ones are still persisted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST transform structured JSON data into AAS Metamodel v3.0-compliant Submodel instances using blueprint-embedded mapping rules.
- **FR-002**: System MUST support four rule types as template qualifiers: `SMT/MappingInfo` (path/Jsonata mapping), `SMT/CollectionMappingInfo` (array duplication), `SMT/FilterMappingInfo` (conditional inclusion), and `SMT/Cardinality` (optional/mandatory behaviour).
- **FR-003**: System MUST support Jsonata expressions in `SMT/MappingInfo` qualifiers, including string functions (`$uppercase`, `$lowercase`, `$trim`, `$split`, `$join`, `$replace`, `$substring`, `$contains`, `$length`), numeric functions (`$number`, `$string`, `$abs`, `$floor`, `$ceil`, `$round`, `$power`, `$sqrt`), boolean expressions, and chained operations.
- **FR-004**: System MUST duplicate `SubmodelElementCollection` and `SubmodelElementList` elements for each item in a referenced array when `SMT/CollectionMappingInfo` is present, appending a zero-based index suffix to each copy's `idShort`.
- **FR-005**: System MUST exclude elements from the generated instance when a `SMT/FilterMappingInfo` boolean expression evaluates to `false`.
- **FR-006**: System MUST enforce cardinality constraints — raising an error for missing mandatory (`"One"`) fields and producing empty values for missing optional (`"ZeroToOne"`) fields.
- **FR-007**: System MUST set the generated Submodel's `kind` to `"Instance"` (changed from blueprint's `"Template"`).
- **FR-008**: System MUST remove all top-level template qualifiers (`SMT/*`) from the generated Submodel instance.
- **FR-009**: System MUST assign a unique, deterministically generated ID to each generated Submodel.
- **FR-010**: System MUST persist generated Submodels in the connected AAS repository and create the submodel reference in the parent AAS.
- **FR-011**: System MUST process each blueprint independently; a failure in one blueprint MUST NOT prevent processing of other blueprints in the same request.
- **FR-012**: System MUST support `MultiLanguageProperty` elements, mapping values under the language tag specified in the request.
- **FR-013**: System MUST copy non-mapped element types (`Range`, `Blob`, `File`, `ReferenceElement`, `Entity`) unchanged from blueprint to instance.
- **FR-014**: System MUST expose the Data Ingest operation via `POST /api/v2/DataIngest/{base64EncodedAasId}`.
- **FR-015**: System MUST expose AAS creation via `POST /api/v2/AasCreator/{assetIdShort}`, optionally triggering submodel generation when blueprint IDs and data are provided.
- **FR-016**: System MUST return structured results per blueprint, including success/failure status, generated submodel ID, error information, and debug logs (when requested).
- **FR-017**: System MUST process the 7-step pipeline in fixed order: deep-clone blueprint → set kind to instance → duplicate collections → filter elements → map data → remove qualifiers → replace identification.
- **FR-018**: System MUST support a debug mode (`debug: true`) that collects and returns pipeline processing logs.

### Key Entities

- **Template**: A base Submodel schema (`kind: "Template"`) following IDTA standards, with placeholder fields and no mapping rules. Stored in the Templates AAS.
- **Blueprint**: A Template enriched with mapping rules (qualifiers of kind `TemplateQualifier` with `SMT/*` types). Defines how input JSON maps to each Submodel element. Stored in the Blueprints AAS.
- **Instance**: The generated Submodel output (`kind: "Instance"`) with actual data values populated, qualifiers removed, and a unique ID assigned. Stored in the AAS repository under a parent AAS.
- **DataMappingContext**: The pipeline context object carrying immutable inputs (blueprint JSON, data JSON, language, new submodel ID) and mutable state (submodel instance, logs) through all pipeline steps.
- **AasGeneratorResult**: The per-blueprint outcome of a generation request, containing blueprint ID, success flag, generated submodel ID, error info, and debug info.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given identical inputs (blueprint, data, language), the system produces byte-identical Submodel output across repeated invocations (determinism).
- **SC-002**: Generated Submodels pass AAS Metamodel v3.0 structural validation (correct `kind`, valid `idShort` values, proper element types).
- **SC-003**: All four rule types (`MappingInfo`, `CollectionMappingInfo`, `FilterMappingInfo`, `Cardinality`) function correctly for their documented use cases.
- **SC-004**: Data Ingest requests with valid blueprints and data complete and persist results within 5 seconds for a single blueprint with typical payloads (< 1 MB data, < 100 elements).
- **SC-005**: When one blueprint fails in a multi-blueprint request, remaining blueprints are still processed and persisted successfully.
- **SC-006**: Debug mode provides sufficient information to diagnose common mapping issues (missing data paths, expression errors, cardinality violations) without requiring access to server logs.
- **SC-007**: 100% of new or modified files in `MnestixCore/AASGenerator/` have corresponding unit tests in `Core.Tests/AasGenerator/`.

## Assumptions

- An Eclipse BaSyx v2-compatible AAS repository is available and reachable at the configured `ServerUrls`.
- Blueprints referenced in requests have been previously created and stored in the Blueprints AAS.
- The parent AAS shell exists in the repository before calling the Data Ingest endpoint (for `POST /api/v2/DataIngest`).
- Input JSON data conforms to the structure expected by the blueprint's mapping qualifiers; no automatic schema validation of input data is performed.
- The system processes requests synchronously; no queuing or batch processing is in scope.
- ID generation settings are pre-configured in the Configuration submodel; the generator does not fall back to defaults if configuration is missing.
- Single language per generation call for `MultiLanguageProperty` elements; multi-language generation requires separate calls.
- This spec covers the Data Ingest and AAS Creator endpoints only; Template/Blueprint CRUD, ID Generator CRUD, Configuration CRUD, and AAS Relationship queries are separate features to be specified independently.
