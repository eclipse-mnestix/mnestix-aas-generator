# Feature Specification: AAS Relationship Queries

**Feature Branch**: `dev`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: Specify existing AAS Relationship query functionality as currently implemented on `dev` branch.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Query Derived-From Relationships (Priority: P1)

An application developer or industrial engineer queries which AAS shells were derived from a given parent AAS. This enables navigation of product family hierarchies — for example, finding all product variants that inherit from a base product's digital twin. The query reads directly from the AAS repository's database to traverse the `derivedFrom` relationship field.

**Why this priority**: This is the only relationship query the system supports, and it serves the core use case of navigating AAS inheritance hierarchies.

**Independent Test**: Can be fully tested by creating AAS shells with `derivedFrom` references in the repository, then calling `GET /api/v2/AasRelationship/GetDerivedFrom?aasId={parentAasId}` and verifying the returned list matches the expected children.

**Acceptance Scenarios**:

1. **Given** three AAS shells exist where AAS-B and AAS-C both have `derivedFrom` pointing to AAS-A, **When** the user calls `GET /api/v2/AasRelationship/GetDerivedFrom?aasId={AAS-A-id}`, **Then** the system returns a list of two entries, each containing the AAS ID and asset ID short of the derived shells.

2. **Given** an AAS exists but no other AAS has a `derivedFrom` reference to it, **When** the user queries derived-from for that AAS, **Then** the system returns an empty list.

3. **Given** the `aasId` parameter contains URL-encoded characters (e.g., `https%3A%2F%2F...`), **When** the query is performed, **Then** the system decodes the ID before searching, correctly matching against stored values.

4. **Given** the `aasId` parameter is empty or missing, **When** the user calls the endpoint, **Then** the system returns 400 Bad Request.

---

### Edge Cases

- What happens when the AAS repository database is unreachable? The query fails with a server error.
- What happens when a derived AAS has no `specificAssetIds` entry with name `assetIdShort`? The current implementation returns the AAS ID with an empty-string asset ID short.
- What happens when the `derivedFrom` field has multiple keys? The system matches only on the first key's value (`keys[0].value`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose `GET /api/v2/AasRelationship/GetDerivedFrom` to query AAS shells that derive from a given parent AAS.
- **FR-002**: The endpoint MUST accept an `aasId` query parameter identifying the parent AAS.
- **FR-003**: System MUST URL-decode the `aasId` parameter before performing the lookup.
- **FR-004**: System MUST return a list of matching AAS entries, each containing the AAS identifier and the asset ID short (if available).
- **FR-005**: When `aasId` is null or empty, the system MUST return 400 Bad Request.
- **FR-006**: The query MUST match against the first key in the `derivedFrom.keys` array of each AAS in the repository.
- **FR-007**: System MUST read relationship data directly from the AAS repository's database, not via the repository's REST API.
- **FR-008**: The endpoint MUST require authentication.

### Key Entities

- **AAS (in relationship context)**: A record containing `AasId` (the full AAS identifier) and `AssetIdShort` (the human-readable short name, nullable). Represents a child in a derived-from relationship.
- **derivedFrom**: An AAS Metamodel v3.0 field that references the parent AAS from which this AAS was derived. Contains a `keys` array where the first key holds the parent AAS ID.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Derived-from queries return correct results within 2 seconds for repositories containing up to 1,000 AAS shells.
- **SC-002**: The system correctly handles URL-encoded AAS identifiers without double-encoding or matching failures.
- **SC-003**: Returns an empty list (not an error) when no derived AAS shells exist for the given parent.

## Assumptions

- The AAS repository's database is accessible via a direct database connection (not only via the repository's REST API).
- The database connection is configured via repository service settings.
- The `derivedFrom` field follows the AAS Metamodel v3.0 structure with a `keys` array containing reference entries.
- Only `derivedFrom` relationships are queryable; other AAS relationship types (e.g., `assetOf`, custom references) are out of scope.
- The database schema matches the BaSyx AAS server's storage format; changes to BaSyx's internal schema may require updates to this query.
- This is a read-only feature — the system does not create or modify `derivedFrom` relationships; those are set when AAS shells are created.
