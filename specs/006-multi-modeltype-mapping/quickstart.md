# Quickstart: Multi-Modeltype Mapping

**Feature**: 006-multi-modeltype-mapping

## What Changed

The AAS Generator now supports `SMT/MappingInfo/<FieldName>` qualifiers that map input data to specific fields on submodel elements — not just the `value` field.

## Supported Fields

| Qualifier Type | Maps To | Example Use Case |
|----------------|---------|-----------------|
| `SMT/MappingInfo` | `value` (legacy, unchanged) | Property values |
| `SMT/MappingInfo/value` | `value` (explicit) | Same as legacy |
| `SMT/MappingInfo/idShort` | `idShort` | Dynamic element naming |
| `SMT/MappingInfo/globalAssetId` | `globalAssetId` | Entity asset links |
| `SMT/MappingInfo/entityType` | `entityType` | Entity type setting |
| `SMT/MappingInfo/displayName` | `displayName` (text for current language) | Human-readable labels |
| `SMT/MappingInfo/first` | `first` (Reference object) | Relationship source |
| `SMT/MappingInfo/second` | `second` (Reference object) | Relationship target |

## Quick Example: Dynamic Entity in a Blueprint

```json
{
  "idShort": "ComponentTemplate",
  "entityType": "SelfManagedEntity",
  "modelType": "Entity",
  "qualifiers": [
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/idShort",
      "value": "$replace(component.name, '-', '_')",
      "valueType": "xs:string"
    },
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/globalAssetId",
      "value": "'https://asset.example.com/' & component.id",
      "valueType": "xs:string"
    }
  ]
}
```

**Input data**: `{"component": {"name": "Housing-123", "id": "H123"}}`

**Generated output**:
```json
{
  "idShort": "Housing_123",
  "entityType": "SelfManagedEntity",
  "modelType": "Entity",
  "globalAssetId": "https://asset.example.com/H123"
}
```

## Key Behaviors

- **Backwards compatible**: Existing `SMT/MappingInfo` qualifiers work exactly as before.
- **idShort sanitization**: Invalid characters are auto-replaced with `_` (warning logged).
- **Strict validation**: Using an unsupported field name or applying a field to an incompatible model type produces a clear error.
- **Value type validation**: Mapped values are checked against the element's `valueType` (e.g., `xs:integer`). Unknown types pass through with a warning.

## Running Tests

```bash
dotnet test
```

All existing tests must pass unchanged. New test fixtures are in `Core.Tests/AasGenerator/TestJsons/InputMultiField*`.
