# Contract: Template Qualifier Schema — Multi-Field MappingInfo

**Feature**: 006-multi-modeltype-mapping  
**Date**: 2026-04-09  
**Type**: Blueprint qualifier contract (template authoring interface)

## Qualifier Format

### Legacy Format (Backwards Compatible)

```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/MappingInfo",
  "value": "<jsonata-expression>",
  "valueType": "xs:string"
}
```

**Behavior**: Maps the evaluated expression result to the element's `value` field. Identical to current behavior.

### New Multi-Field Format

```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/MappingInfo/<FieldName>",
  "value": "<jsonata-expression>",
  "valueType": "xs:string"
}
```

**Behavior**: Maps the evaluated expression result to the element's `<FieldName>` field.

## Supported Field Names

| FieldName | Target | Applicable Model Types | Expression Returns |
|-----------|--------|----------------------|-------------------|
| `value` | Element value | Property, Blob, MultiLanguageProperty | For Property/Range/Blob: valueType-compatible data; for MultiLanguageProperty: scalar (`string`, `number`, `boolean`, `null`) converted to text |
| `idShort` | Element identifier | All | String (auto-sanitized to `[a-zA-Z][a-zA-Z0-9_]*`) |
| `globalAssetId` | Entity asset reference | Entity | String (URI) |
| `entityType` | Entity type enum | Entity | String (`SelfManagedEntity` or `CoManagedEntity`) |
| `displayName` | Display name text | All | String (sets `text` for current generation language) |
| `first` | Relationship first ref | RelationshipElement, AnnotatedRelationshipElement | AAS Reference JSON object |
| `second` | Relationship second ref | RelationshipElement, AnnotatedRelationshipElement | AAS Reference JSON object |

## Examples

### Entity with globalAssetId + idShort mapping

```json
{
  "idShort": "PartTemplate",
  "entityType": "SelfManagedEntity",
  "modelType": "Entity",
  "qualifiers": [
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/idShort",
      "value": "component.partNumber",
      "valueType": "xs:string"
    },
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/globalAssetId",
      "value": "'https://asset.example.com/' & component.partNumber",
      "valueType": "xs:string"
    }
  ]
}
```

### RelationshipElement with dynamic references

```json
{
  "idShort": "HasPart",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [{ "type": "GlobalReference", "value": "https://admin-shell.io/idta/HierarchicalStructures/HasPart/1/0" }]
  },
  "first": {},
  "second": {},
  "modelType": "RelationshipElement",
  "qualifiers": [
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/first",
      "value": "{'type': 'ModelReference', 'keys': [{'type': 'Submodel', 'value': submodelId}, {'type': 'Entity', 'value': 'CableSet'}]}",
      "valueType": "xs:string"
    },
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo/second",
      "value": "{'type': 'ModelReference', 'keys': [{'type': 'Submodel', 'value': submodelId}, {'type': 'Entity', 'value': 'CableSet'}, {'type': 'Entity', 'value': component.idShort}]}",
      "valueType": "xs:string"
    }
  ]
}
```

### Mixed legacy and new qualifiers on different elements

```json
{
  "modelType": "SubmodelElementCollection",
  "idShort": "ComponentInfo",
  "value": [
    {
      "modelType": "Property",
      "idShort": "SerialNumber",
      "valueType": "xs:string",
      "qualifiers": [
        { "type": "SMT/MappingInfo", "value": "product.serial" }
      ]
    },
    {
      "modelType": "Entity",
      "idShort": "PartTemplate",
      "entityType": "SelfManagedEntity",
      "qualifiers": [
        { "type": "SMT/MappingInfo/globalAssetId", "value": "product.assetUri" },
        { "type": "SMT/MappingInfo/idShort", "value": "product.partId" }
      ]
    }
  ]
}
```

## Error Conditions

| Condition | Error Message Pattern |
|-----------|----------------------|
| Unknown field name | `"Unsupported MappingInfo field '<FieldName>'. Allowed: value, idShort, globalAssetId, entityType, displayName, first, second"` |
| Field-modeltype mismatch | `"Field '<FieldName>' is not applicable to model type '<ModelType>'"` |
| Duplicate field on element | `"Duplicate mapping for field '<FieldName>' on element '<idShort>'"` |
| ValueType mismatch | `"Mapped value '<value>' does not conform to valueType '<valueType>'"` |
| MultiLanguageProperty non-scalar value | `"MultiLanguageProperty expects a string, number, boolean, or null value, but got <JTokenType>"` |

## REST API Impact

**No changes** to the REST API contract (`POST /api/v1/DataIngest`). The request/response format is unchanged. The new functionality is entirely within the blueprint qualifier schema — template authors use the new qualifier types in their blueprints, and the generator processes them transparently.
