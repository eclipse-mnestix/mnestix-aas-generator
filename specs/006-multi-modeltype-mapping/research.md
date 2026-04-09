# Research: Multi-Modeltype Mapping

**Feature**: 006-multi-modeltype-mapping  
**Date**: 2026-04-09

## 1. Qualifier Type Parsing Strategy

**Decision**: Parse the qualifier `type` field by splitting on `/` to extract the field name suffix.

**Rationale**: The existing code queries qualifiers via `$..qualifiers[?(@.type=='SMT/MappingInfo')]`. To support `SMT/MappingInfo/<FieldName>`, we change the query to match qualifiers whose type starts with `SMT/MappingInfo`. Then extract the field name from the third segment after splitting by `/`.

- `SMT/MappingInfo` → no third segment → target field = `value` (legacy)
- `SMT/MappingInfo/value` → third segment = `value` → target field = `value`
- `SMT/MappingInfo/globalAssetId` → third segment = `globalAssetId` → target field = `globalAssetId`

**Alternatives considered**:
- Using a separate qualifier type per field (e.g., `SMT/MappingInfoGlobalAssetId`) — Rejected: would require new JSONPath queries per field and doesn't scale.
- Using the qualifier `value` field to embed both field name and expression (e.g., `globalAssetId:component.assetId`) — Rejected: breaks Jsonata expression parsing and is confusing.

## 2. Field Allowlist & Model Type Applicability

**Decision**: Maintain a static dictionary mapping each allowed field name to the set of model types on which it is valid.

| Field | Applicable Model Types | Value Type |
|-------|----------------------|------------|
| `value` | Property, Range, Blob, MultiLanguageProperty | string (or MLP array) |
| `idShort` | All submodel element types | string |
| `globalAssetId` | Entity | string |
| `entityType` | Entity | string (enum: SelfManagedEntity, CoManagedEntity) |
| `displayName` | All submodel element types | MLP array (language-aware) |
| `first` | RelationshipElement, AnnotatedRelationshipElement | Reference object (JSON object) |
| `second` | RelationshipElement, AnnotatedRelationshipElement | Reference object (JSON object) |

**Rationale**: A static dictionary is simple, explicit, and easy to extend. It avoids reflection or runtime schema introspection. The applicability matrix is derived from the IDTA AAS Metamodel v3.0 specification.

**Alternatives considered**:
- Dynamic field discovery via JToken introspection — Rejected: would allow mapping to any JSON property, bypassing AAS schema validation and creating security/correctness risks.

## 3. Reference-Type Field Mapping (`first`/`second`)

**Decision**: For `first` and `second` fields, the Jsonata expression must return a complete AAS Reference JSON object (with `type` and `keys` array). The mapped value replaces the existing reference structure.

**Rationale**: AAS Reference objects have a complex nested structure (`type`, `keys[]` with `type`+`value`). Trying to map individual sub-fields would require multiple qualifiers per reference and create confusing template authoring. Instead, the Jsonata expression constructs the full reference object from data.

**Example blueprint qualifier**:
```json
{
  "type": "SMT/MappingInfo/second",
  "value": "{'type': 'ModelReference', 'keys': [{'type': 'Submodel', 'value': submodelId}, {'type': 'Entity', 'value': 'CableSet'}, {'type': 'Entity', 'value': component.idShort}]}"
}
```

**Alternative approach**: For simpler use cases, the Jsonata expression can return a string that replaces specific key values within a template reference structure. However, the full-object-replacement approach was chosen for its generality and to avoid creating a reference-specific mini-template language.

**Alternatives considered**:
- Sub-field mapping with `SMT/MappingInfo/second/keys/2/value` — Rejected: overly complex qualifier paths, hard to author, and fragile to reference structure changes.
- Template-based string interpolation within existing reference structures — Rejected: would require a different expression engine and doesn't leverage existing Jsonata capabilities.

## 4. displayName Mapping (Multi-Language Property)

**Decision**: Reuse the existing language parameter. The Jsonata expression returns a plain string. The step finds the matching language entry in the existing `displayName` array and sets its `text` field.

**Rationale**: Consistent with how `MultiLanguageProperty` value mapping already works in the generator. The template defines the language structure; the mapping only fills in text for the generation language.

**Alternatives considered**:
- Mapping the entire displayName array from data — Rejected: would require Jsonata to output a complex array structure, harder to author, and the generator already has a single-language-per-call paradigm.

## 5. idShort Sanitization

**Decision**: Apply regex-based sanitization after mapping: replace characters not in `[a-zA-Z0-9_]` with `_`, prepend `_` if the result starts with a digit. Log a warning when sanitization changes the value.

**Rationale**: AAS Metamodel v3.0 defines idShort as `[a-zA-Z][a-zA-Z0-9_]*`. VEC data commonly contains hyphens (e.g., `TE_ConnectorHousing_2470646-9`). Auto-sanitizing provides a better developer experience than error-on-invalid, since the sanitization rules are deterministic and predictable.

**Alternatives considered**:
- Failing on invalid characters — Rejected: too strict for the VEC use case where hyphens are ubiquitous. Would force every template to include `$replace()` Jsonata expressions.
- Passing through as-is — Rejected: violates Constitution Principle I (AAS Specification Conformance).

## 6. Value Type Validation

**Decision**: Validate mapped values against the element's `valueType` using a static set of known XSD types. For known types, apply format validation. For unknown types, pass through and log a warning.

Known types and validation rules:
| XSD Type | Validation |
|----------|------------|
| `xs:string` | Always passes (any value is a valid string) |
| `xs:boolean` | Must be `true` or `false` (case-insensitive) |
| `xs:integer`, `xs:int`, `xs:long`, `xs:short` | Must parse as integer |
| `xs:decimal`, `xs:double`, `xs:float` | Must parse as numeric |
| `xs:dateTime` | Must match ISO 8601 datetime format |
| `xs:date` | Must match ISO 8601 date format |
| `xs:anyURI` | Must be a non-empty string (basic URI validation) |

**Rationale**: Prevents silently producing non-compliant AAS instances. Warning-on-unknown is forward-compatible with future XSD types.

**Alternatives considered**:
- Full XSD schema validation library — Rejected: too heavy a dependency for the limited set of types used in practice.
- No validation — Rejected: contradicts user story 6 requirements.

## 7. Duplicate Qualifier Detection

**Decision**: Before processing, group all `SMT/MappingInfo*` qualifiers on an element by their resolved target field. If any field has more than one qualifier, throw `SubmodelDataToInstanceMapperException` with a clear message listing the element's `idShort` and the duplicate field name.

**Rationale**: Duplicate mapping is always a template authoring error. Fail-fast is the safest default per the clarification session.

**Alternatives considered**:
- Last-wins — Rejected: non-deterministic from the template author's perspective.
- Warning-only — Rejected: would produce unpredictable output.
