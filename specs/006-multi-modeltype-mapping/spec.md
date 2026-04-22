# Feature Specification: Multi-Modeltype Mapping

**Feature Branch**: `006-multi-modeltype-mapping`  
**Created**: 2025-04-09  
**Status**: Draft  
**Input**: User description: "Extend SMT/MappingInfo qualifiers to support mapping data into model-type-specific fields (idShort, globalAssetId, and other allowed fields) beyond just the value field, enabling dynamic creation of HierarchicalStructures submodels from VEC input data."

## Clarifications

### Session 2026-04-09

- Q: Should RelationshipElement `first`/`second` reference mapping be in-scope for this feature? → A: In scope — add `first` and `second` to the allowlist as reference-type fields with their own mapping logic.
- Q: When a qualifier targets a field incompatible with the element's model type, should the generator fail or silently ignore? → A: Fail with a clear error message at generation time (strict validation).
- Q: How should `SMT/MappingInfo/displayName` populate the displayName field? → A: Map only the `text` value for the current generation language (uses existing language parameter; template pre-defines the language array structure).
- Q: Should the generator sanitize mapped idShort values to conform to AAS rules, or leave validation to the caller? → A: Auto-sanitize — replace invalid characters (e.g., hyphens) with underscores and log a warning.
- Q: When duplicate qualifiers target the same field on a single element, what should happen? → A: Fail with an error indicating duplicate field mapping detected.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Map Data to Entity globalAssetId (Priority: P1)

As a template author, I want to use a `SMT/MappingInfo/globalAssetId` qualifier on an Entity element so that the generator populates the entity's `globalAssetId` field from input data, enabling me to dynamically link entities to their real asset identifiers.

**Why this priority**: Without `globalAssetId` mapping, Entity elements in a HierarchicalStructures submodel cannot reference actual assets. This is the core enabler for dynamic bill-of-materials generation from VEC data.

**Independent Test**: Can be fully tested by providing a blueprint with an Entity element carrying a `SMT/MappingInfo/globalAssetId` qualifier and verifying the generated instance has the correct `globalAssetId` populated from input data.

**Acceptance Scenarios**:

1. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/globalAssetId` set to `component.assetId`, **When** input data contains `{"component": {"assetId": "https://asset.te.com/housing_123"}}`, **Then** the generated Entity has `globalAssetId` set to `"https://asset.te.com/housing_123"`.
2. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/globalAssetId` and `SMT/Cardinality` set to `One`, **When** input data does not contain the referenced path, **Then** the generator raises a mapping error indicating the mandatory field is missing.
3. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/globalAssetId` and `SMT/Cardinality` set to `ZeroToOne`, **When** input data does not contain the referenced path, **Then** the generated Entity has an empty or absent `globalAssetId`.

---

### User Story 2 - Map Data to Element idShort (Priority: P1)

As a template author, I want to use a `SMT/MappingInfo/idShort` qualifier on any submodel element so that the generator dynamically sets the element's `idShort` from input data, enabling me to create uniquely named elements based on component identifiers.

**Why this priority**: Dynamic `idShort` assignment is essential for distinguishing duplicated collection elements (e.g., each Entity in a HierarchicalStructures BOM needs a unique identifier derived from the source data).

**Independent Test**: Can be fully tested by providing a blueprint Property or Entity with a `SMT/MappingInfo/idShort` qualifier and verifying the generated element has the `idShort` populated from input data.

**Acceptance Scenarios**:

1. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/idShort` set to `part.partNumber`, **When** input data contains `{"part": {"partNumber": "TE_2470646-9"}}`, **Then** the generated Entity has `idShort` set to `"TE_2470646-9"`.
2. **Given** a blueprint with a duplicated collection containing elements with `SMT/MappingInfo/idShort`, **When** the collection expands for 3 array items, **Then** each generated element has a distinct `idShort` derived from its corresponding data item.
3. **Given** a blueprint Entity with `SMT/MappingInfo/idShort` set to a Jsonata expression `$replace(part.name, " ", "_")`, **When** input data contains a part name with spaces, **Then** the generated `idShort` has spaces replaced with underscores.

---

### User Story 3 - Backwards-Compatible Legacy MappingInfo (Priority: P1)

As an existing user of the AAS Generator, I want my current blueprints using `SMT/MappingInfo` (without a field suffix) to continue working exactly as they do today, so that the new multi-modeltype feature does not break any existing templates.

**Why this priority**: Backwards compatibility is critical. Existing blueprints in production must not break when the system is updated.

**Independent Test**: Can be fully tested by running all existing blueprint tests and verifying identical output. A legacy `SMT/MappingInfo` qualifier must behave identically to `SMT/MappingInfo/value`.

**Acceptance Scenarios**:

1. **Given** a blueprint Property with qualifier `SMT/MappingInfo` (legacy format) set to `product.serialNumber`, **When** input data contains the serial number, **Then** the generated Property has its `value` set to the serial number — identical to current behavior.
2. **Given** a blueprint MultiLanguageProperty with qualifier `SMT/MappingInfo` (legacy format), **When** the generator processes the template, **Then** the behavior is identical to the current implementation.
3. **Given** a mixed blueprint with both legacy `SMT/MappingInfo` and new `SMT/MappingInfo/globalAssetId` qualifiers on different elements, **When** the generator processes the template, **Then** each qualifier is processed according to its type — legacy maps to value, new maps to the specified field.

---

### User Story 4 - Map Data to Additional Allowed Fields (Priority: P2)

As a template author, I want to use `SMT/MappingInfo/<FieldName>` qualifiers for a defined set of allowed fields beyond `value`, `idShort`, and `globalAssetId`, so that I can populate model-type-specific properties dynamically.

**Why this priority**: Extends the system's flexibility for future use cases beyond the immediate HierarchicalStructures requirement.

**Independent Test**: Can be fully tested by providing a blueprint Entity with a `SMT/MappingInfo/entityType` qualifier and verifying the generated element has the correct `entityType` populated from input data.

**Acceptance Scenarios**:

1. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/entityType` set to `component.entityType`, **When** input data contains `{"component": {"entityType": "SelfManagedEntity"}}`, **Then** the generated Entity has `entityType` set to `"SelfManagedEntity"`.
2. **Given** a blueprint element with a qualifier `SMT/MappingInfo/notAllowedField` referencing a field not in the allowed set, **When** the generator processes the template, **Then** the generator raises a validation error indicating the field is not supported.
3. **Given** a blueprint Entity with qualifier `SMT/MappingInfo/displayName` set to a data path and a template `displayName` array with `[{"language": "en", "text": ""}, {"language": "de", "text": ""}]`, **When** input data contains the display name value and the generation language is `en`, **Then** the generated Entity has the `en` entry's `text` field populated from data while the `de` entry remains unchanged.

---

### User Story 5 - Dynamic HierarchicalStructures from VEC Data (Priority: P2)

As a system integrator, I want to create a HierarchicalStructures blueprint that uses collection mapping combined with multi-modeltype mapping, so that I can generate a complete bill-of-materials submodel from VEC input data in a single generation call.

**Why this priority**: This is the end-to-end integration scenario that validates all individual mapping capabilities working together. It depends on User Stories 1–4.

**Independent Test**: Can be fully tested by providing a HierarchicalStructures blueprint with collection and multi-field mapping qualifiers, along with VEC-derived input data, and verifying the generated submodel contains correctly structured Entity and RelationshipElement hierarchies.

**Acceptance Scenarios**:

1. **Given** a HierarchicalStructures blueprint with an Entity template inside a collection mapping over VEC components, **When** VEC input data contains 5 components, **Then** the generated submodel contains 5 Entity elements, each with correct `idShort`, `globalAssetId`, and `entityType` populated from the corresponding VEC component.
2. **Given** a HierarchicalStructures blueprint with RelationshipElement templates using multi-field mapping, **When** the generator processes VEC input data, **Then** the generated RelationshipElements have correct `first` and `second` references linking parent and child entities.

---

### User Story 6 - Value Type Validation for Mapped Values (Priority: P2)

As a template author, I want the generator to validate that mapped values conform to the element's declared `valueType` (e.g., `xs:string`, `xs:dateTime`, `xs:integer`), so that I get early feedback when input data doesn't match the expected type rather than producing silently invalid AAS instances.

**Why this priority**: Type mismatches between input data and declared valueTypes produce non-compliant AAS instances that may fail downstream validation or cause unexpected behavior in AAS consumers.

**Independent Test**: Can be fully tested by providing a blueprint Property with `valueType: xs:integer` and a `SMT/MappingInfo/value` qualifier pointing to a string value, and verifying the generator raises a validation error.

**Acceptance Scenarios**:

1. **Given** a blueprint Property with `valueType: xs:integer` and qualifier `SMT/MappingInfo/value` set to `product.quantity`, **When** input data contains `{"product": {"quantity": 42}}`, **Then** the generated Property has `value` set to `"42"` and validation passes.
2. **Given** a blueprint Property with `valueType: xs:integer` and qualifier `SMT/MappingInfo/value` set to `product.name`, **When** input data contains `{"product": {"name": "Widget"}}`, **Then** the generator raises a validation error indicating the mapped value does not conform to `xs:integer`.
3. **Given** a blueprint Property with `valueType: xs:dateTime` and qualifier `SMT/MappingInfo/value` set to `event.timestamp`, **When** input data contains a valid ISO 8601 datetime string, **Then** validation passes.
4. **Given** a blueprint Property with an unknown `valueType` (e.g., `xs:customType`) and qualifier `SMT/MappingInfo/value`, **When** the generator processes the template, **Then** the value is passed through without validation and a warning is logged indicating the valueType is not recognized.

---

### Edge Cases

- What happens when a `SMT/MappingInfo/<FieldName>` qualifier references a field name that does not exist on the target element's model type (e.g., `globalAssetId` on a Property)? → Generator MUST fail with a clear error message.
- What happens when multiple `SMT/MappingInfo/<FieldName>` qualifiers exist on the same element targeting the same field? → Generator MUST fail with an error indicating duplicate field mapping detected.
- What happens when an `idShort` mapping produces a value with characters invalid in AAS idShort (e.g., containing dots or slashes)? → Auto-sanitize: replace invalid characters with underscores, prepend `i` if the result starts with a non-letter character (e.g., `"123abc"` → `"i123abc"`, `"_value"` → `"i_value"`), and log a warning.
- How does the system behave when a `SMT/MappingInfo/value` qualifier coexists with a legacy `SMT/MappingInfo` qualifier on the same element? → Generator MUST fail with an error indicating duplicate field mapping detected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support the qualifier format `SMT/MappingInfo/<FieldName>` where `<FieldName>` identifies the target field on the submodel element to populate.
- **FR-002**: System MUST treat legacy `SMT/MappingInfo` (without field suffix) as equivalent to `SMT/MappingInfo/value` to ensure full backwards compatibility.
- **FR-003**: System MUST support `SMT/MappingInfo/value` as an explicit qualifier for mapping data to an element's value field.
- **FR-004**: System MUST support `SMT/MappingInfo/idShort` for mapping data to an element's `idShort` field.
- **FR-005**: System MUST support `SMT/MappingInfo/globalAssetId` for mapping data to an Entity element's `globalAssetId` field.
- **FR-006**: System MUST support `SMT/MappingInfo/entityType` for mapping data to an Entity element's `entityType` field.
- **FR-007**: System MUST maintain a defined allowlist of supported field names for `SMT/MappingInfo/<FieldName>` qualifiers.
- **FR-008**: System MUST reject qualifiers referencing field names not in the allowlist with a clear error message.
- **FR-009**: All `SMT/MappingInfo/<FieldName>` qualifiers MUST support the same Jsonata expression capabilities as the current `SMT/MappingInfo` qualifier (paths, functions, chained operations).
- **FR-010**: System MUST respect existing `SMT/Cardinality` qualifier behavior for all `SMT/MappingInfo/<FieldName>` variants (mandatory vs. optional handling).
- **FR-011**: System MUST process all `SMT/MappingInfo/<FieldName>` qualifiers on a given element, allowing multiple fields to be mapped simultaneously.
- **FR-012**: System MUST validate that the target field is applicable to the element's model type (e.g., `globalAssetId` only on Entity elements) and MUST fail with a clear error message if mismatched — regardless of cardinality.
- **FR-013**: Existing blueprints using `SMT/MappingInfo` MUST produce identical output after this change — no behavioral regression.
- **FR-014**: System MUST support `SMT/MappingInfo/first` for mapping data to a RelationshipElement's `first` reference field.
- **FR-015**: System MUST support `SMT/MappingInfo/second` for mapping data to a RelationshipElement's `second` reference field.
- **FR-016**: For `SMT/MappingInfo/displayName`, the system MUST populate the `text` value of the entry matching the current generation language within the element's `displayName` array. If no matching language entry exists, a new entry MUST be added. If no `displayName` array exists, one MUST be created.
- **FR-017**: For `SMT/MappingInfo/idShort`, the system MUST auto-sanitize the mapped value to conform to AAS idShort rules (`[a-zA-Z][a-zA-Z0-9_]*`): replace invalid characters with underscores, prepend `i` if the result starts with a non-letter character, and log a warning when sanitization occurs.
- **FR-018**: System MUST fail with a clear error if multiple qualifiers on the same element resolve to the same target field (e.g., both `SMT/MappingInfo` and `SMT/MappingInfo/value` on one element).
- **FR-019**: For `SMT/MappingInfo/value`, the system MUST validate that the mapped value conforms to the element's declared `valueType` (e.g., `xs:string`, `xs:integer`, `xs:dateTime`, `xs:boolean`, `xs:double`) and raise a validation error on type mismatch.
- **FR-020**: If the element's `valueType` is not recognized by the validator, the system MUST pass the value through without validation and log a warning.

### Key Entities

- **MappingInfo Qualifier**: A template qualifier that defines how input data maps to a specific field of a submodel element. Extended from single-field (`value`) to multi-field with the format `SMT/MappingInfo/<FieldName>`.
- **Field Allowlist**: The set of permitted target field names. Initial set: `value`, `idShort`, `globalAssetId`, `entityType`, `displayName`, `first`, `second`.
- **Submodel Element**: Any AAS metamodel element (Property, Entity, SubmodelElementCollection, RelationshipElement, etc.) that can carry mapping qualifiers.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing unit and integration tests pass without modification, confirming backwards compatibility.
- **SC-002**: A HierarchicalStructures blueprint with multi-field mapping qualifiers generates a complete, valid submodel from VEC input data in a single generation call.
- **SC-003**: Template authors can populate any field from the allowlist on any applicable element type using the new qualifier format.
- **SC-004**: Invalid field names produce user-friendly error messages within the generator's standard error reporting structure.
- **SC-005**: The feature introduces no performance regression — generation of submodels with the same complexity completes in comparable time.

## Assumptions

- The existing pipeline architecture (Pipes-and-Filters with `MapDataToInstanceStep`) is sufficient to implement multi-field mapping without adding new pipeline steps. The change is localized to how `SMT/MappingInfo` qualifiers are parsed and applied.
- The initial allowlist of supported fields (`value`, `idShort`, `globalAssetId`, `entityType`, `displayName`, `first`, `second`) covers the immediate HierarchicalStructures use case. The allowlist can be extended in future iterations without architectural changes.
- VEC input data is pre-processed into a flat/accessible JSON structure before being passed to the AAS Generator (consistent with current behavior via `VEC-Input.json`).
- RelationshipElement `first` and `second` reference fields are in scope. They are included in the field allowlist and will support dynamic reference construction from mapped data paths.
- The `SMT/CollectionMappingInfo` qualifier already handles element duplication correctly and does not need modification for this feature.
