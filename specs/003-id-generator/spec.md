# Feature Specification: ID Generator

**Feature Branch**: `dev`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: Specify existing ID Generator functionality as currently implemented on `dev` branch.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Generate AAS & Asset Identifiers (Priority: P1)

An application developer or system integrator generates standardised identifiers for a new AAS and its associated asset. They provide an optional human-readable asset short name (e.g., `machine-001`), and the system produces a complete set of four IDs — asset ID, asset ID short, AAS ID, and AAS ID short — according to configurable rules that define the prefix and dynamic part for each ID component.

**Why this priority**: Every AAS creation depends on deterministic, conflict-free identifiers. The ID generator is the foundation of the AAS lifecycle.

**Independent Test**: Can be fully tested by calling `GET /api/v2/IdGenerator/aasIds/machine-001` and verifying the returned IDs match the expected format based on the current generation settings.

**Acceptance Scenarios**:

1. **Given** the ID generation settings configure all dynamic parts to use `AssetIdShort`, **When** the user calls `GET /api/v2/IdGenerator/aasIds/machine-001`, **Then** all four IDs contain `machine-001` as the dynamic component (e.g., `https://example.com/machine-001` for asset ID).

2. **Given** the ID generation settings configure all dynamic parts to use `GUID`, **When** the user calls `GET /api/v2/IdGenerator/aasIds/machine-001`, **Then** each ID contains a unique 32-character GUID as the dynamic component, and each GUID differs from the others.

3. **Given** the AAS ID dynamic part is configured to `AASidShort`, **When** the user generates IDs, **Then** the AAS ID is composed of the AAS ID prefix plus the generated AAS ID short value (chaining the two).

4. **Given** no asset ID short is provided, **When** the user calls `GET /api/v2/IdGenerator/aasIds/` (without parameter), **Then** the system generates a 32-character GUID as the asset ID short and uses it wherever `AssetIdShort` is referenced in the settings.

---

### User Story 2 — Generate Submodel Identifiers (Priority: P2)

An application developer generates one or more unique submodel identifiers in bulk. These IDs are used when creating new submodels, ensuring globally unique identification.

**Why this priority**: Submodel ID generation is simpler (always GUID-based) but essential for programmatic creation of multiple submodels.

**Independent Test**: Can be tested by calling `GET /api/v2/IdGenerator/submodelIds/5` and verifying 5 unique IDs are returned, each with the configured prefix and a 32-character GUID suffix.

**Acceptance Scenarios**:

1. **Given** the submodel ID prefix is `https://example.com/sm/`, **When** the user calls `GET /api/v2/IdGenerator/submodelIds/3`, **Then** the system returns a list of 3 IDs, each in the format `https://example.com/sm/{32-char-GUID}`, and all GUIDs are unique.

2. **Given** the user requests 1 submodel ID, **When** the call completes, **Then** exactly 1 ID is returned.

---

### User Story 3 — Configurable ID Patterns (Priority: P3)

A system integrator customises the ID generation rules — prefixes and dynamic parts — to match their organisation's naming conventions. The settings are stored as a submodel in the AAS repository, so they persist across restarts and can be updated at runtime without redeployment.

**Why this priority**: Default settings work out of the box for most users, but enterprise deployments require organisation-specific URI patterns. This is a setup-once concern.

**Independent Test**: Can be tested by patching the Configuration submodel's ID generation settings via `PATCH /api/v2/Configuration` and then verifying that subsequent `GET /api/v2/IdGenerator/aasIds/test` calls produce IDs with the updated prefix.

**Acceptance Scenarios**:

1. **Given** the asset ID prefix is currently `https://old.com/`, **When** the integrator patches it to `https://new.com/assets/`, **Then** subsequent ID generation calls produce asset IDs starting with `https://new.com/assets/`.

2. **Given** incorrect settings are stored (e.g., an unrecognised enum value for a dynamic part), **When** ID generation is requested, **Then** the system falls back to GUID as the default dynamic part and logs a warning.

---

### Edge Cases

- What happens when the Configuration submodel is missing from the repository? The system cannot generate IDs; an error is returned.
- What happens when the configured prefix is an empty string? IDs consist of just the dynamic part (GUID or asset ID short) with no prefix.
- What happens when `count` is 0 for submodel ID generation? The system returns an empty list.
- What happens when the generated GUID collides with an existing ID? GUIDs are 128-bit random values; collisions are statistically negligible. The system does not check for uniqueness in the repository.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST generate a complete set of four identifiers (asset ID, asset ID short, AAS ID, AAS ID short) from a single request, using configurable prefix + dynamic-part rules.
- **FR-002**: System MUST support the following dynamic part options per ID component: `GUID` (all components), `AssetIdShort` (asset ID, AAS ID, AAS ID short), and `AASidShort` (AAS ID only).
- **FR-003**: When no asset ID short parameter is provided, the system MUST generate a 32-character GUID as the default asset ID short.
- **FR-004**: Generated GUIDs MUST be 32-character hexadecimal strings (standard GUID format without hyphens or special characters).
- **FR-005**: System MUST generate submodel IDs in bulk, each composed of the configured submodel ID prefix and a unique 32-character GUID.
- **FR-006**: System MUST load ID generation settings dynamically from the Configuration submodel in the AAS repository on each request.
- **FR-007**: When a dynamic part enum value cannot be parsed from the stored settings, the system MUST fall back to `GUID` and log a warning.
- **FR-008**: System MUST expose ID generation via `GET /api/v2/IdGenerator/aasIds/{assetIdShort}`, `GET /api/v2/IdGenerator/aasIds/`, and `GET /api/v2/IdGenerator/submodelIds/{count}`.
- **FR-009**: All ID generation endpoints MUST disable HTTP caching (no-store).
- **FR-010**: All endpoints MUST require authentication.

### Key Entities

- **AasIds**: The output of AAS ID generation — a set of four identifiers: `assetId`, `assetIdShort`, `aasId`, `aasIdShort`.
- **IdGenerationSettings**: The configurable rules governing ID creation — 5 prefix strings and 5 dynamic part enums (one pair per ID component).
- **Configuration Submodel**: The AAS submodel in the repository that stores the ID generation settings as structured submodel elements.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given the same asset ID short and the same settings, the system produces identical AAS IDs across repeated calls (deterministic when dynamic parts use `AssetIdShort`).
- **SC-002**: When dynamic parts use `GUID`, each call produces unique, non-repeating identifiers.
- **SC-003**: ID generation requests complete within 2 seconds, including settings retrieval from the repository.
- **SC-004**: Generated GUIDs contain exactly 32 characters with no hyphens or special characters.
- **SC-005**: Bulk submodel ID generation (`count = N`) returns exactly N unique IDs.

## Assumptions

- The Configuration submodel containing ID generation settings is pre-created in the repository (handled by Required Shells startup, specified separately).
- Settings are read fresh from the repository on each request; no in-memory caching of configuration.
- GUID generation uses the platform's standard random GUID implementation; cryptographic uniqueness guarantees are not required.
- The `AASidShort` dynamic part for AAS ID creates a derived dependency: AAS ID depends on AAS ID short, which must be computed first.
- This spec covers ID generation and its endpoints only; the Configuration CRUD API is specified in [004-configuration-service](../004-configuration-service/spec.md).
