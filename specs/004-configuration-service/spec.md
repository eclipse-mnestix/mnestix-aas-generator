# Feature Specification: Configuration Service

**Feature Branch**: `dev`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: Specify existing Configuration Service functionality as currently implemented on `dev` branch.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — View Current ID Generation Configuration (Priority: P1)

A system integrator or application developer retrieves the current ID generation configuration to understand how AAS and submodel identifiers are being generated. The configuration is stored as a submodel in the AAS repository, and the API returns the full submodel JSON.

**Why this priority**: Viewing the current configuration is essential before making changes, and is the most frequent configuration operation.

**Independent Test**: Can be fully tested by calling `GET /api/v2/Configuration` and verifying the returned JSON contains the expected submodel structure with ID generation settings.

**Acceptance Scenarios**:

1. **Given** the Configuration submodel exists in the repository with ID generation settings, **When** an authenticated user calls `GET /api/v2/Configuration`, **Then** the system returns a 200 OK with the full submodel JSON including all prefix and dynamic part values.

2. **Given** the Configuration submodel does not exist in the repository, **When** an authenticated user calls `GET /api/v2/Configuration`, **Then** the system returns 404 Not Found.

---

### User Story 2 — Update a Single Configuration Setting (Priority: P2)

A system integrator updates an individual ID generation setting — such as changing the AAS ID prefix or switching a dynamic part from `GUID` to `AssetIdShort` — without replacing the entire configuration. Changes take effect immediately for subsequent ID generation requests.

**Why this priority**: Granular updates are safer than full replacements and are the primary way integrators tune the generator to match their naming conventions.

**Independent Test**: Can be tested by calling `PATCH /api/v2/Configuration?idShortPath=AASID/Prefix&value=https://new.com/` and then verifying with a GET that the value was updated.

**Acceptance Scenarios**:

1. **Given** the current AAS ID prefix is `https://old.com/aas/`, **When** the user calls `PATCH /api/v2/Configuration?idShortPath=AASID/Prefix&value=https://new.com/aas/`, **Then** the system returns 204 No Content, and a subsequent GET shows the updated prefix.

2. **Given** a valid configuration exists, **When** the user patches a dynamic part value (e.g., `idShortPath=AssetID/DynamicPart&value=AssetIdShort`), **Then** the setting is updated in the repository and subsequent ID generation uses the new dynamic part.

3. **Given** the Configuration submodel does not exist, **When** the user attempts a PATCH, **Then** the system returns 404 Not Found.

4. **Given** the repository is unreachable, **When** the user attempts a PATCH, **Then** the system returns 404 Not Found (the operation fails gracefully).

---

### Edge Cases

- What happens when `idShortPath` references a non-existent submodel element? The repository rejects the PATCH; the system returns a failure.
- What happens when `value` is an empty string? The system stores the empty string; prefixes become empty and IDs will have no prefix.
- What happens when the Configuration submodel has malformed JSON? The GET endpoint returns 404 (unable to parse), and a warning is logged.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose `GET /api/v2/Configuration` to retrieve the full ID generation configuration as the raw submodel JSON from the repository.
- **FR-002**: System MUST expose `PATCH /api/v2/Configuration` to update a single configuration setting, accepting `idShortPath` (path to the submodel element) and `value` (new value) as query parameters.
- **FR-003**: The PATCH operation MUST update the value of the specified submodel element at the path `{submodelId}/submodel-elements/{idShortPath}/$value` in the repository.
- **FR-004**: Configuration changes MUST take effect immediately — subsequent ID generation requests MUST use the updated settings without requiring a restart.
- **FR-005**: When the Configuration submodel cannot be found or parsed, the GET endpoint MUST return 404 Not Found.
- **FR-006**: When the PATCH operation fails (repository error), the system MUST return 404 Not Found and log a warning.
- **FR-007**: All configuration endpoints MUST require authentication.

### Key Entities

- **Configuration Submodel**: An AAS submodel stored in the repository that holds the ID generation settings. Identified by the `ConfigurationSubmodelId` from application settings. Contains nested submodel elements for each ID component (AASID, AssetID, etc.), each with Prefix and DynamicPart child elements.
- **IdShortPath**: A slash-separated path identifying a specific submodel element within the Configuration submodel (e.g., `AASID/Prefix`, `AssetID/DynamicPart`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Configuration retrieval returns the complete submodel JSON within 2 seconds.
- **SC-002**: After a PATCH operation, the updated value is reflected in subsequent GET requests and ID generation calls without service restart.
- **SC-003**: Failed PATCH operations do not corrupt existing configuration — the original values remain intact.
- **SC-004**: Error responses include enough context for the operator to diagnose the issue (missing submodel, unreachable repository).

## Assumptions

- The Configuration submodel is created during application startup by the Required Shells assertion; this spec does not cover initial creation.
- Only one Configuration submodel exists per deployment; multi-tenant configuration is out of scope.
- The Configuration submodel currently stores only ID generation settings; future extensions may add other settings under the same submodel.
- The `idShortPath` parameter follows the AAS submodel element path convention used by BaSyx.
- This spec covers the Configuration CRUD API only; how the ID Generator consumes these settings is specified in [003-id-generator](../003-id-generator/spec.md).
