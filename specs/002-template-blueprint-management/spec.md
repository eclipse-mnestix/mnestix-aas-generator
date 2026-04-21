# Feature Specification: Template & Blueprint Management

**Feature Branch**: `dev`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: Specify existing Template & Blueprint management functionality as currently implemented on `dev` branch.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Manage Blueprints (Priority: P1)

An application developer or industrial engineer manages blueprints — the mapping-rule-enriched submodel definitions that drive the AAS Generator. They create blueprints from existing templates, retrieve them for review, update mapping rules, and delete obsolete blueprints. All operations are available through a REST API and persist in the Blueprints AAS stored in the connected repository (or a remote API when configured).

**Why this priority**: Blueprints are the prerequisite for all submodel generation. Without the ability to create, view, update, and delete blueprints, the AAS Generator has no mapping rules to work with.

**Independent Test**: Can be fully tested by performing CRUD operations on `/api/v2/Blueprints` and verifying the blueprint state in the repository.

**Acceptance Scenarios**:

1. **Given** an authenticated user with `admin.write` scope, **When** they POST a valid submodel JSON to `/api/v2/Blueprints`, **Then** the system stores the blueprint in the Blueprints AAS with a newly generated ID (`{idShort}_Template_{GUID}`), sets the `kind` to `"Instance"`, adds a `displayName` qualifier with the current timestamp, and returns the generated blueprint ID.

2. **Given** blueprints exist in the Blueprints AAS, **When** an authenticated user GETs `/api/v2/Blueprints`, **Then** the system returns a JSON array of all blueprints.

3. **Given** a blueprint with a known ID exists, **When** an authenticated user GETs `/api/v2/Blueprints/{base64EncodedBlueprintId}`, **Then** the system returns the blueprint as a JSON object.

4. **Given** a blueprint with a known ID exists, **When** an authenticated user POSTs an updated submodel JSON to `/api/v2/Blueprints/{submodelId}`, **Then** the system replaces the blueprint content in the repository.

5. **Given** a blueprint with a known ID exists, **When** an authenticated user DELETEs `/api/v2/Blueprints/{base64EncodedBlueprintId}`, **Then** the system removes both the submodel reference from the Blueprints AAS and the submodel itself from the repository.

6. **Given** a remote Blueprints API URL is configured (`SubmodelBlueprintsApiUrl`), **When** any blueprint CRUD operation is performed, **Then** the system routes the operation to the remote API instead of the local repository.

7. **Given** a user without `admin.write` scope, **When** they attempt any blueprint operation, **Then** the system returns 401 Unauthorized or 403 Forbidden.

---

### User Story 2 — Browse and Import Templates (Priority: P2)

An application developer or industrial engineer browses available templates — the base submodel schemas, often based on IDTA standards (e.g., Nameplate, ContactInformation) — to understand what submodel structures are available before creating blueprints from them. They can also import new templates into the system.

**Why this priority**: Templates are the foundation that blueprints are built upon, but they are typically managed less frequently (often pre-loaded from IDTA standards). Browsing is essential; creation is secondary.

**Independent Test**: Can be tested by GETting `/api/v2/Templates` and verifying the returned templates match what is stored in the Templates AAS.

**Acceptance Scenarios**:

1. **Given** templates exist in the Templates AAS, **When** an authenticated user GETs `/api/v2/Templates`, **Then** the system returns a JSON array of all templates.

2. **Given** a remote Templates API URL is configured (`SubmodelTemplatesApiUrl`), **When** the user GETs `/api/v2/Templates`, **Then** the system fetches templates from the remote API instead of the local repository.

3. **Given** no remote Templates API URL is configured, **When** an authenticated user POSTs a valid submodel JSON to `/api/v2/Templates`, **Then** the system stores the template in the Templates AAS, setting or prepending its `semanticId` with a `ConceptDescription` key pointing to the template's own ID, and returns 204 No Content.

4. **Given** a remote Templates API URL IS configured, **When** the user POSTs to `/api/v2/Templates`, **Then** the system returns 403 Forbidden with a message directing the user to the remote templates API.

5. **Given** a template JSON is posted without an `id` field, **When** the system processes the request, **Then** it returns an error indicating the template ID is required.

---

### User Story 3 — Dual-Mode Repository Access (Priority: P3)

A system integrator configures the AAS Generator to operate in either local mode (templates and blueprints stored in the same BaSyx repository as the generated AAS) or remote mode (templates and/or blueprints managed by a separate, dedicated API). This allows flexible deployment topologies where a central template/blueprint registry serves multiple generator instances.

**Why this priority**: Most deployments start with local mode. Remote mode is an advanced deployment pattern, but important for multi-instance or centralised template management scenarios.

**Independent Test**: Can be tested by toggling `SubmodelTemplatesApiUrl` and `SubmodelBlueprintsApiUrl` configuration values and verifying that operations route to the correct backend.

**Acceptance Scenarios**:

1. **Given** `SubmodelBlueprintsApiUrl` is empty in configuration, **When** blueprint operations are performed, **Then** the system uses the local repository via the repository proxy client (fetching submodel references from the Blueprints AAS, then individual submodels).

2. **Given** `SubmodelBlueprintsApiUrl` is set to a valid URL, **When** blueprint operations are performed, **Then** the system routes all HTTP calls to that URL, supporting both direct array responses (`[{...}]`) and wrapped responses (`{"result": [{...}]}`).

3. **Given** `SubmodelTemplatesApiUrl` is empty, **When** template retrieval is performed, **Then** the system fetches from the local Templates AAS via the repository proxy.

4. **Given** `SubmodelTemplatesApiUrl` is set to a valid URL, **When** template retrieval is performed, **Then** the system fetches from the remote API, expecting a `{"result": [...]}` response format.

5. **Given** a remote API is configured but returns an error (non-2xx status, empty body, or invalid JSON), **When** a template or blueprint operation is attempted, **Then** the system raises an error with a descriptive message including the HTTP status code or failure reason.

---

### Edge Cases

- What happens when the repository is unreachable? The system propagates the HTTP error from the repository proxy client.
- What happens when a blueprint ID to delete is not found? The system returns 404 Not Found.
- What happens when the remote API returns an unexpected response format (neither array nor `{"result": [...]}`? The system raises an error describing the unexpected format.
- What happens when a remote API returns an empty response body? The system raises an error indicating the endpoint returned an empty response.
- What happens when a template is POSTed with duplicate `semanticId` keys? The system prepends the new key without deduplication; the template is stored as-is.
- What happens when a blueprint is created from a template without any mapping qualifiers? The blueprint is stored successfully; it simply produces a Submodel with no dynamic values when used by the generator.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a REST API for creating, retrieving, updating, and deleting blueprints via `/api/v2/Blueprints`.
- **FR-002**: System MUST provide a REST API for retrieving templates via `GET /api/v2/Templates` and creating templates via `POST /api/v2/Templates`.
- **FR-003**: When creating a blueprint, the system MUST generate a unique ID in the format `{idShort}_Template_{GUID}`, set `kind` to `"Instance"`, and add a `displayName` qualifier with an ISO 8601 timestamp.
- **FR-004**: When creating a template, the system MUST validate that the `id` field is present and non-empty, and MUST set or prepend the `semanticId` with a `ConceptDescription` key referencing the template's own ID.
- **FR-005**: System MUST support dual-mode operation for both templates and blueprints: local mode (via repository proxy to BaSyx) and remote mode (via configurable external API URLs).
- **FR-006**: Mode selection MUST be determined by the presence of `SubmodelTemplatesApiUrl` and `SubmodelBlueprintsApiUrl` configuration values — empty string means local mode; a URL means remote mode.
- **FR-007**: When a remote Templates API URL is configured, the system MUST reject local template creation with 403 Forbidden and direct the user to the remote API.
- **FR-008**: In local mode, blueprint creation MUST persist both the submodel and its reference in the Blueprints AAS. Blueprint deletion MUST remove both the reference and the submodel.
- **FR-009**: In remote mode, the system MUST route CRUD operations as HTTP calls to the configured remote URL, supporting standard REST verbs (GET, POST, PUT, DELETE).
- **FR-010**: The blueprint retrieval endpoint MUST handle two response formats from remote APIs: direct JSON arrays (`[{...}]`) and wrapped arrays (`{"result": [{...}]}`).
- **FR-011**: The template retrieval endpoint MUST handle the wrapped response format (`{"result": [{...}]}`) from remote APIs.
- **FR-012**: All blueprint and template management endpoints MUST require authentication (JWT Bearer token or API key) with `admin.write` scope for modifying operations.
- **FR-013**: System MUST return structured error responses: 400 for processing errors, 403 for forbidden operations, 404 for missing resources.

### Key Entities

- **Template**: A base Submodel schema with `kind: "Template"`, typically based on IDTA standards. Stored in the Templates AAS. Has a `semanticId` referencing a ConceptDescription. Read-mostly (create and retrieve only, no update or delete via API).
- **Blueprint**: A Template enriched with mapping qualifiers (`SMT/*` types) that instruct the AAS Generator how to map data. Stored in the Blueprints AAS with `kind: "Instance"`, a generated ID, and a `displayName` qualifier. Full CRUD supported.
- **Templates AAS**: A dedicated AAS shell that contains references to all template submodels. Identified by `TemplatesAasId` configuration.
- **Blueprints AAS**: A dedicated AAS shell that contains references to all blueprint submodels. Identified by `BlueprintsAasId` configuration.
- **Submodel Reference**: A `ModelReference` with a key of type `Submodel` pointing to a submodel's ID. Used to link submodels to their parent AAS.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a blueprint from a template and retrieve it within 3 seconds for typical submodel sizes (< 500 KB).
- **SC-002**: Switching between local and remote mode requires only a configuration change — no code changes or redeployment beyond restarting the service.
- **SC-003**: All CRUD operations on blueprints (create, read, update, delete) complete successfully and the resulting state is verifiable by re-reading the resource.
- **SC-004**: Template retrieval returns all templates from either local or remote source without data loss or transformation artefacts.
- **SC-005**: Unauthorized users are denied access to all modifying operations; the system never exposes blueprint or template data to unauthenticated requests on secured deployments.
- **SC-006**: Remote API errors (unreachable, invalid response) produce actionable error messages that help the operator diagnose the issue.

## Assumptions

- The Blueprints AAS and Templates AAS shells are pre-created in the repository (handled by the Required Shells startup assertion, specified separately).
- Templates are typically imported from IDTA standard definitions or created once; they are not frequently modified.
- Blueprints are the primary artefact users interact with when configuring the generator.
- The remote API, when configured, implements a RESTful interface compatible with BaSyx submodel endpoints.
- In local mode, the repository proxy client handles authentication to BaSyx (API key forwarding) transparently.
- Template update and delete operations are not exposed via the REST API; templates are considered write-once in this system.
- This spec covers template and blueprint management only; the use of blueprints by the AAS Generator pipeline is specified in [001-aas-generator-rules-engine](../001-aas-generator-rules-engine/spec.md).
