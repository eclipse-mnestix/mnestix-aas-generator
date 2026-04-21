# Data Model: Multi-Modeltype Mapping

**Feature**: 006-multi-modeltype-mapping  
**Date**: 2026-04-09

## Entities

### MappingQualifierDescriptor

Represents a parsed `SMT/MappingInfo/<FieldName>` qualifier with resolved target field.

| Field | Type | Description |
|-------|------|-------------|
| `TargetField` | `string` | Resolved target field name (e.g., `value`, `idShort`, `globalAssetId`). Defaults to `value` for legacy `SMT/MappingInfo`. |
| `Expression` | `string` | Jsonata expression or data path from the qualifier's `value` property. |
| `IsMandatory` | `bool` | Derived from sibling `SMT/Cardinality` qualifier. `true` if cardinality starts with `"One"`. |
| `QualifierToken` | `JToken` | Reference to the original qualifier JToken (for error reporting context). |

### FieldAllowlistEntry

Static configuration defining which fields can be mapped and to which model types.

| Field | Type | Description |
|-------|------|-------------|
| `FieldName` | `string` | The target field name (e.g., `globalAssetId`). |
| `ApplicableModelTypes` | `HashSet<string>` | Set of model type strings where this field is valid (e.g., `{"Entity"}`). `null` means all model types. |
| `FieldCategory` | `FieldCategory` | Enum: `Simple` (string replacement), `MultiLanguage` (MLP-aware), `Reference` (AAS Reference object). |

### Field Allowlist (Static Data)

| FieldName | ApplicableModelTypes | FieldCategory |
|-----------|---------------------|---------------|
| `value` | `Property`, `Range`, `Blob`, `MultiLanguageProperty` | `Simple` (or `MultiLanguage` for MLP) |
| `idShort` | all | `Simple` |
| `globalAssetId` | `Entity` | `Simple` |
| `entityType` | `Entity` | `Simple` |
| `displayName` | all | `MultiLanguage` |
| `first` | `RelationshipElement`, `AnnotatedRelationshipElement` | `Reference` |
| `second` | `RelationshipElement`, `AnnotatedRelationshipElement` | `Reference` |

### FieldCategory Enum

| Value | Description |
|-------|-------------|
| `Simple` | Direct string value replacement in the target JSON property. |
| `MultiLanguage` | Language-aware: finds the matching language entry in an MLP array and sets the `text` field. |
| `Reference` | Full AAS Reference object replacement (the Jsonata expression must return a valid Reference JSON structure). |

## Relationships

```
MapDataToInstanceStep
  ├── uses → FieldAllowlist (static dictionary)
  ├── parses → SMT/MappingInfo* qualifiers → MappingQualifierDescriptor[]
  ├── validates → duplicate field detection (group by TargetField per element)
  ├── validates → field-modeltype applicability (FieldAllowlistEntry.ApplicableModelTypes)
  ├── validates → valueType conformance (for TargetField == "value")
  └── dispatches → field assignment based on FieldCategory
        ├── Simple → direct JToken replacement
        ├── MultiLanguage → language-aware text insertion
        └── Reference → full reference object replacement
```

## Validation Rules

1. **Allowlist check**: `TargetField` must exist in the field allowlist. If not → `SubmodelDataToInstanceMapperException`.
2. **Model type check**: Element's `modelType` must be in `FieldAllowlistEntry.ApplicableModelTypes`. If not → `SubmodelDataToInstanceMapperException`.
3. **Duplicate check**: No two qualifiers on the same element may resolve to the same `TargetField`. If duplicate → `SubmodelDataToInstanceMapperException`.
4. **idShort sanitization**: If `TargetField == "idShort"`, apply regex sanitization after Jsonata evaluation. Log warning if value changed.
5. **valueType validation**: If `TargetField == "value"` and element has a `valueType`, validate the mapped value conforms. Error on known-type mismatch; warning on unknown type.

## State Transitions

No stateful entities. The pipeline is a single-pass transformation. Each qualifier is processed independently within the `MapDataToInstance` method.
