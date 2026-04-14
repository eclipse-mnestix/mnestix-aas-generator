# AAS Generator - Blueprint & Rules Documentation
_This API-File should also be published within GitHub._
This document explains how to create and configure Blueprints for automated Submodel generation using the AAS Generator rules engine.

> **For German readers**: A detailed article about this system is available at [XITASO: Automatisierte Erstellung von AAS](https://xitaso.com/automatisierte-erstellung-von-aas/) (German language). Note that the article may reference an older version of the AAS Generator, so please refer to this documentation for the latest features and API details.

> **Tip**: While this documentation covers the API and JSON structure in detail, the [Mnestix Browser](https://github.com/eclipse-mnestix/mnestix-browser) provides a visual "Templates and Blueprints" section that simplifies blueprint creation and management.

## Overview

The AAS Generator transforms structured JSON data into AAS-compliant Submodel instances using a three-tier architecture:

![Template → Blueprint → Instance](images/template-blueprint-instance.png)

### The Three Tiers

| Tier | Description | Created By |
|------|-------------|------------|
| **Template** | A base Submodel schema defining the structure. Often based on IDTA standards (e.g., Nameplate, ContactInformation). Has `kind: "Template"` and placeholder fields. | API endpoint or imported from standards |
| **Blueprint** | A Template with added mapping rules (qualifiers) that define how JSON data maps to each field. | Users via Mnestix Browser UI or API |
| **Instance** | The final generated Submodel with `kind: "Instance"` and actual data values populated from the input JSON. | AAS Generator (automatically) |

## Supported Submodel Element Types

The following element types support data mapping via qualifiers:

| Element Type | Mapping Support | Notes |
|--------------|-----------------|-------|
| `Property` | ✅ Full | Standard value mapping |
| `MultiLanguageProperty` | ⚠️ Limited | Single language per generation call |
| `SubmodelElementCollection` | ✅ Full | Supports collection duplication |
| `SubmodelElementList` | ✅ Full | Supports collection duplication |

Elements that are **not** directly mapped but are preserved in the output:

| Element Type | Behavior |
|--------------|----------|
| `Blob` | Copied unchanged from blueprint |
| `File` | Copied unchanged from blueprint |
| `ReferenceElement` | Copied unchanged from blueprint |
| `Entity` | Supports multi-field mapping (`globalAssetId`, `entityType`, `idShort`, `displayName`) |
| `RelationshipElement` | Supports multi-field mapping (`first`, `second`, `idShort`, `displayName`) |
| `AnnotatedRelationshipElement` | Supports multi-field mapping (`first`, `second`, `idShort`, `displayName`) |

## Template Qualifiers (Mapping Rules)

Mapping rules are embedded as **qualifiers** within Submodel elements. The AAS Generator reads these qualifiers during processing to determine how to populate values.

### Qualifier Format

```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/<RuleType>",
  "value": "<rule-configuration>",
  "valueType": "xs:string"
}
```

### Available Rule Types

| Qualifier Type | Purpose |
|----------------|---------|
| `SMT/MappingInfo` | Map a JSON path or Jsonata expression to an element's value (legacy format) |
| `SMT/MappingInfo/<FieldName>` | Map data to a specific element field (e.g., `idShort`, `globalAssetId`) |
| `SMT/CollectionMappingInfo` | Duplicate an element for each array item |
| `SMT/FilterMappingInfo` | Conditionally include/exclude an element based on a boolean expression |
| `SMT/Cardinality` | Define whether the data is required or optional |

---

## Rule Type Details

### 1. Static Values (No Qualifier)

For fields with constant values that don't change between instances, simply set the value directly in the blueprint without any mapping qualifier.

**Blueprint:**
```json
{
  "modelType": "Property",
  "idShort": "ManufacturerCountry",
  "valueType": "xs:string",
  "value": "Germany"
}
```

**Result:** The value `"Germany"` is copied unchanged to every generated instance.

---

### 2. Path Mapping (`SMT/MappingInfo`)

Maps a JSON path from the input data to an element's value. This is the most common rule type.

**Qualifier:**
```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/MappingInfo",
  "value": "product.serialNumber",
  "valueType": "xs:string"
}
```

**Blueprint Element:**
```json
{
  "modelType": "Property",
  "idShort": "SerialNumber",
  "valueType": "xs:string",
  "value": "",
  "qualifiers": [
    {
      "kind": "TemplateQualifier",
      "type": "SMT/MappingInfo",
      "value": "product.serialNumber",
      "valueType": "xs:string"
    }
  ]
}
```

**Input Data:**
```json
{
  "product": {
    "serialNumber": "SN-12345-XYZ"
  }
}
```

**Generated Instance:**
```json
{
  "modelType": "Property",
  "idShort": "SerialNumber",
  "valueType": "xs:string",
  "value": "SN-12345-XYZ"
}
```

#### Path Expression Syntax

| Expression | Description | Example |
|------------|-------------|---------|
| `field` | Top-level field | `serialNumber` |
| `parent.child` | Nested object access | `product.details.name` |
| `array[0]` | Specific array index | `contacts[0].name` |
| `array[*]` | Array wildcard (for collections) | `contacts[*].email` |

---

### 2b. Multi-Field Mapping (`SMT/MappingInfo/<FieldName>`)

Extends path mapping to target specific fields on an element beyond just `value`. The `SMT/MappingInfo` (legacy) qualifier maps to `value` by default. The new format `SMT/MappingInfo/<FieldName>` allows mapping to any supported field.

**Supported Fields:**

| FieldName | Target | Applicable Model Types | Notes |
|-----------|--------|----------------------|-------|
| `value` | Element value | Property, Blob, MultiLanguageProperty | Default (same as legacy `SMT/MappingInfo`) |
| `idShort` | Element identifier | All | Auto-sanitized to `[a-zA-Z][a-zA-Z0-9_]*` |
| `globalAssetId` | Entity asset reference | Entity | String (URI) |
| `entityType` | Entity type enum | Entity | `SelfManagedEntity` or `CoManagedEntity` |
| `displayName` | Display name text | All | Sets `text` for current generation language |
| `first` | Relationship first reference | RelationshipElement, AnnotatedRelationshipElement | AAS Reference JSON object |
| `second` | Relationship second reference | RelationshipElement, AnnotatedRelationshipElement | AAS Reference JSON object |

#### Example: Entity with globalAssetId + idShort

**Blueprint:**
```json
{
  "modelType": "Entity",
  "idShort": "PartTemplate",
  "entityType": "SelfManagedEntity",
  "globalAssetId": "",
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

**Input Data:**
```json
{
  "component": {
    "partNumber": "Housing_001"
  }
}
```

**Generated Instance:**
```json
{
  "modelType": "Entity",
  "idShort": "Housing_001",
  "entityType": "SelfManagedEntity",
  "globalAssetId": "https://asset.example.com/Housing_001"
}
```

#### Example: RelationshipElement with first/second

**Blueprint:**
```json
{
  "modelType": "RelationshipElement",
  "idShort": "HasPart",
  "first": {},
  "second": {},
  "qualifiers": [
    {
      "type": "SMT/MappingInfo/first",
      "value": "relationship.parentRef",
      "valueType": "xs:string"
    },
    {
      "type": "SMT/MappingInfo/second",
      "value": "relationship.childRef",
      "valueType": "xs:string"
    }
  ]
}
```

#### idShort Sanitization

When mapping to `idShort`, the generator auto-sanitizes the value to conform to AAS naming rules (`[a-zA-Z][a-zA-Z0-9_]*`):
- Characters not matching `[a-zA-Z0-9_]` are replaced with `_`
- If the result does not start with a letter (`[a-zA-Z]`), `i` is prepended
- A warning is logged when sanitization changes the value

**Examples:**
- Input `"TE-Housing-123"` → idShort `"TE_Housing_123"`
- Input `"123abc"` → idShort `"i123abc"`
- Input `"_value"` → idShort `"i_value"`

#### Value Type Validation

When mapping to `value`, the generator validates that the mapped value conforms to the element's declared `valueType`:

| valueType | Validation |
|-----------|-----------|
| `xs:string` | Always passes |
| `xs:integer`, `xs:int`, `xs:long`, `xs:short` | Must be a valid integer |
| `xs:decimal`, `xs:double`, `xs:float` | Must be a valid number |
| `xs:boolean` | Must be `true`, `false`, `0`, or `1` |
| `xs:dateTime` | Must be a valid date-time string |
| `xs:date` | Must be a valid date string |
| `xs:anyURI` | Always passes |
| Unknown types | Value passes through with a warning |

#### Error Conditions

| Condition | Behavior |
|-----------|----------|
| Unknown field name (not in allowlist) | Error: `"Unsupported MappingInfo field '<FieldName>'"` |
| Field not applicable to model type | Error: `"Field '<FieldName>' is not applicable to model type '<ModelType>'"` |
| Duplicate field mapping on same element | Error: `"Duplicate mapping for field '<FieldName>' on element '<idShort>'"` |
| Value type mismatch | Error: `"Mapped value '<value>' does not conform to valueType '<valueType>'"` |

#### Combining with Collection Mapping

Multi-field mapping works with `SMT/CollectionMappingInfo` for dynamic element creation:

```json
{
  "modelType": "Entity",
  "idShort": "ComponentTemplate",
  "entityType": "CoManagedEntity",
  "globalAssetId": "",
  "qualifiers": [
    { "type": "SMT/CollectionMappingInfo", "value": "vec.components[*]" },
    { "type": "SMT/MappingInfo/idShort", "value": "vec.components[*].partNumber" },
    { "type": "SMT/MappingInfo/globalAssetId", "value": "vec.components[*].assetId" },
    { "type": "SMT/MappingInfo/entityType", "value": "vec.components[*].entityType" }
  ]
}
```

Each duplicated element gets its own `idShort`, `globalAssetId`, and `entityType` from the corresponding array item.

---

### 3. Collection Mapping (`SMT/CollectionMappingInfo`)

Duplicates a Submodel element for each item in an array. This enables dynamic list generation.

**Use Case:** You have an array of contacts in your data and want to create a `ContactPerson` collection for each one.

**Qualifier:**
```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/CollectionMappingInfo",
  "value": "sourceData.contactPersons[*]",
  "valueType": "xs:string"
}
```

**Blueprint Element:**
```json
{
  "modelType": "SubmodelElementCollection",
  "idShort": "contactPerson",
  "qualifiers": [
    {
      "type": "SMT/CollectionMappingInfo",
      "value": "sourceData.contactPersons[*]"
    }
  ],
  "value": [
    {
      "modelType": "Property",
      "idShort": "Name",
      "valueType": "xs:string",
      "qualifiers": [
        {
          "type": "SMT/MappingInfo",
          "value": "sourceData.contactPersons[*].name"
        }
      ]
    },
    {
      "modelType": "Property",
      "idShort": "Email",
      "valueType": "xs:string",
      "qualifiers": [
        {
          "type": "SMT/MappingInfo",
          "value": "sourceData.contactPersons[*].email"
        }
      ]
    }
  ]
}
```

**Input Data:**
```json
{
  "sourceData": {
    "contactPersons": [
      { "name": "John Doe", "email": "john@example.com" },
      { "name": "Jane Smith", "email": "jane@example.com" }
    ]
  }
}
```

**Generated Instance:**
```json
{
  "value": [
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "contactPerson_0",
      "value": [
        { "idShort": "Name", "value": "John Doe" },
        { "idShort": "Email", "value": "john@example.com" }
      ]
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "contactPerson_1",
      "value": [
        { "idShort": "Name", "value": "Jane Smith" },
        { "idShort": "Email", "value": "jane@example.com" }
      ]
    }
  ]
}
```

#### How Collection Processing Works

1. The generator finds all `SMT/CollectionMappingInfo` qualifiers
2. Qualifiers are sorted by **nesting depth** (shallowest first) - counted by `[*]` occurrences
3. For each array item in the data, the element is duplicated
4. The `idShort` is suffixed with an index (`_0`, `_1`, `_2`, ...)
5. Child element paths have `[*]` replaced with the actual index (`[0]`, `[1]`, ...)
6. The process repeats recursively for nested collections

#### Nested Collections

The generator supports nested collections (arrays within arrays). The algorithm processes collections from shallowest to deepest.

**Example:** Contact persons with multiple phone numbers each.

**Blueprint paths:**
```
sourceData.contactPersons[*]                    # Outer collection
sourceData.contactPersons[*].phone_numbers[*]   # Nested collection
sourceData.contactPersons[*].phone_numbers[*].value  # Value in nested collection
```

**Input Data:**
```json
{
  "sourceData": {
    "contactPersons": [
      {
        "name": "SpongeBob",
        "phone_numbers": [
          { "name": "Office", "value": "+1 234 56789" }
        ]
      },
      {
        "name": "Squidward",
        "phone_numbers": [
          { "name": "Office", "value": "+2 234 56789" },
          { "name": "Cell", "value": "+2 1907 1234" }
        ]
      }
    ]
  }
}
```

**Result:** Creates `contactPerson_0` with one phone number, and `contactPerson_1` with two phone numbers (`phone_numbers_0`, `phone_numbers_1`).

---

### 4. Filter Rules (`SMT/FilterMappingInfo`)

Conditionally includes or excludes an element from generation based on a boolean expression. Evaluated against the input data using Jsonata expressions.

**Use Case:** You want to include contact information only for certain types of contacts, or include battery specifications only for electric vehicles.

**Qualifier:**
```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/FilterMappingInfo",
  "value": "vehicle.engineType = 'electric'",
  "valueType": "xs:string"
}
```

**Blueprint Element:**
```json
{
  "modelType": "SubmodelElementCollection",
  "idShort": "BatteryInfo",
  "qualifiers": [
    {
      "kind": "TemplateQualifier",
      "type": "SMT/FilterMappingInfo",
      "value": "vehicle.engineType = 'electric'",
      "valueType": "xs:string"
    }
  ],
  "value": [
    {
      "modelType": "Property",
      "idShort": "CapacityKWh",
      "valueType": "xs:integer",
      "qualifiers": [
        {
          "type": "SMT/MappingInfo",
          "value": "vehicle.battery.capacityKWh"
        }
      ]
    }
  ]
}
```

**Input Data:**
```json
{
  "vehicle": {
    "engineType": "electric",
    "battery": { "capacityKWh": 85 }
  }
}
```

**Result:** Since `vehicle.engineType = 'electric'` evaluates to `true`, the `BatteryInfo` collection is included in the generated instance.

If the condition evaluated to `false`, the `BatteryInfo` element would be omitted entirely.

#### Filter Expression Syntax

Filter expressions use **Jsonata syntax** and must evaluate to a boolean value:

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equals | `type = 'electric'` |
| `!=` | Not equals | `status != 'inactive'` |
| `>` | Greater than | `quantity > 10` |
| `<` | Less than | `price < 100` |
| `>=` | Greater than or equal | `rating >= 4.5` |
| `<=` | Less than or equal | `age <= 65` |
| `and` | Logical AND | `type = 'electric' and year >= 2020` |
| `or` | Logical OR | `status = 'active' or status = 'pending'` |
| `in` | Value in array | `region in ['EU', 'NA']` |

---

### 5. Jsonata Expressions in Mapping Rules

Mapping rules (`SMT/MappingInfo`) support Jsonata expression syntax, enabling advanced data transformations beyond simple path navigation. Refer to the [Jsonata documentation](https://jsonata.org/docs/) and to the [Jsonata.Net.Native GitHub repository](https://github.com/mikhail-barg/jsonata.net.native) for a complete list of functions and operators.
Note that not all exotic Jsonata features may be supported. We recommend constructing your input data as close as possible to the desired blueprint structure to minimize the need for complex transformations.

#### Built-in Jsonata Functions

**String Functions:**

| Function | Description | Example |
|----------|-------------|---------|
| `$length(str)` | Returns the number of characters | `$length(name)` |
| `$substring(str, start[, length])` | Extracts substring | `$substring(code, 0, 3)` |
| `$substringBefore(str, chars)` | Text before first occurrence | `$substringBefore(email, '@')` |
| `$substringAfter(str, chars)` | Text after first occurrence | `$substringAfter(email, '@')` |
| `$contains(str, pattern)` | Tests if pattern exists (returns boolean) | `$contains(description, 'waterproof')` |
| `$split(str, separator)` | Splits string into array | `$split(tags, ',')` |
| `$join(array, separator)` | Joins array elements | `$join(parts, '-')` |
| `$uppercase(str)` | Converts to uppercase | `$uppercase(code)` |
| `$lowercase(str)` | Converts to lowercase | `$lowercase(code)` |
| `$trim(str)` | Removes whitespace | `$trim(input)` |
| `$replace(str, pattern, replacement)` | Replaces occurrences | `$replace(text, 'old', 'new')` |

**Numeric Functions:**

| Function | Description | Example |
|----------|-------------|---------|
| `$number(value)` | Converts to number | `$number('42')` |
| `$string(value)` | Converts to string | `$string(42)` |
| `$abs(number)` | Absolute value | `$abs(-5)` returns `5` |
| `$floor(number)` | Rounds down | `$floor(3.7)` returns `3` |
| `$ceil(number)` | Rounds up | `$ceil(3.2)` returns `4` |
| `$round(number[, precision])` | Rounds to decimal places | `$round(3.14159, 2)` returns `3.14` |
| `$power(base, exponent)` | Base raised to power | `$power(2, 3)` returns `8` |
| `$sqrt(number)` | Square root | `$sqrt(16)` returns `4` |

#### Jsonata Expression Examples

**Example 1: Extract part of a code**
```json
{
  "type": "SMT/MappingInfo",
  "value": "$substring(sku, 0, 3)"
}
```
Input: `"sku": "ABC-12345"` → Output: `"ABC"`

**Example 2: Convert number to string**
```json
{
  "type": "SMT/MappingInfo",
  "value": "$string(quantity)"
}
```
Input: `"quantity": 42` → Output: `"42"`

**Example 3: Check if text contains substring (returns boolean)**
```json
{
  "type": "SMT/MappingInfo",
  "value": "description ~> $contains('wireless')"
}
```
Input: `"description": "wireless charging pad"` → Output: `true`

**Example 4: Compare numeric values (returns boolean)**
```json
{
  "type": "SMT/MappingInfo",
  "value": "price > 1000"
}
```
Input: `"price": 1500` → Output: `true`

**Example 5: Combine string operations**
```json
{
  "type": "SMT/MappingInfo",
  "value": "$uppercase($substringBefore(email, '@'))"
}
```
Input: `"email": "john.doe@example.com"` → Output: `"JOHN.DOE"`

#### Pipe Operator (`~>`)

The pipe operator passes the left operand as the argument to the right function:

```jsonata
"John Smith" ~> $split(' ') ~> $join('-')
```
Equivalent to: `$join($split("John Smith", ' '), '-')` → Result: `"John-Smith"`

---

### 6. Cardinality (`SMT/Cardinality`)

Defines whether a mapped value is required or optional.

| Value | Behavior |
|-------|----------|
| `One` | **Mandatory** - Generation fails with error if data is missing |
| `ZeroToOne` | **Optional** - Empty value is set if data is missing |

**Qualifier:**
```json
{
  "kind": "TemplateQualifier",
  "type": "SMT/Cardinality",
  "value": "One",
  "valueType": "xs:string"
}
```

**Complete Element with Cardinality:**
```json
{
  "modelType": "Property",
  "idShort": "SerialNumber",
  "valueType": "xs:string",
  "value": "",
  "qualifiers": [
    {
      "type": "SMT/Cardinality",
      "value": "One"
    },
    {
      "type": "SMT/MappingInfo",
      "value": "product.serialNumber"
    }
  ]
}
```

If `product.serialNumber` is missing in the input data:
- With `"One"`: Error thrown, generation fails
- With `"ZeroToOne"`: Element created with empty value

---

## Complete Blueprint Example

Here's a complete blueprint demonstrating filters, collections, and Jsonata expressions:

```json
{
  "modelType": "Submodel",
  "kind": "Template",
  "id": "https://example.com/blueprints/product-info-v2",
  "idShort": "ProductInformation",
  "submodelElements": [
    {
      "modelType": "Property",
      "idShort": "ProductName",
      "valueType": "xs:string",
      "value": "",
      "qualifiers": [
        {
          "kind": "TemplateQualifier",
          "type": "SMT/Cardinality",
          "value": "One",
          "valueType": "xs:string"
        },
        {
          "kind": "TemplateQualifier",
          "type": "SMT/MappingInfo",
          "value": "product.name",
          "valueType": "xs:string"
        }
      ]
    },
    {
      "modelType": "Property",
      "idShort": "ProductCode",
      "valueType": "xs:string",
      "value": "",
      "qualifiers": [
        {
          "kind": "TemplateQualifier",
          "type": "SMT/MappingInfo",
          "value": "$uppercase($substring(product.sku, 0, 3))",
          "valueType": "xs:string"
        }
      ],
      "description": [
        {
          "language": "en",
          "text": "First 3 characters of SKU in uppercase, using Jsonata transformation"
        }
      ]
    },
    {
      "modelType": "Property",
      "idShort": "HasWarranty",
      "valueType": "xs:boolean",
      "value": "",
      "qualifiers": [
        {
          "kind": "TemplateQualifier",
          "type": "SMT/MappingInfo",
          "value": "$string(product.warranty.monthsIncluded) ~> $contains('24')",
          "valueType": "xs:string"
        }
      ],
      "description": [
        {
          "language": "en",
          "text": "Returns true if warranty includes 24 months"
        }
      ]
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "BatteryInfo",
      "qualifiers": [
        {
          "kind": "TemplateQualifier",
          "type": "SMT/FilterMappingInfo",
          "value": "product.type = 'battery-powered'",
          "valueType": "xs:string"
        }
      ],
      "value": [
        {
          "modelType": "Property",
          "idShort": "CapacityMah",
          "valueType": "xs:integer",
          "qualifiers": [
            {
              "type": "SMT/MappingInfo",
              "value": "product.battery.capacityMah"
            }
          ]
        },
        {
          "modelType": "Property",
          "idShort": "ChargingTimeHours",
          "valueType": "xs:decimal",
          "qualifiers": [
            {
              "type": "SMT/MappingInfo",
              "value": "$string(product.battery.chargingTimeMinutes) ~> $number($) / 60",
              "valueType": "xs:string"
            }
          ]
        }
      ]
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "CertificationInfo",
      "qualifiers": [
        {
          "kind": "TemplateQualifier",
          "type": "SMT/FilterMappingInfo",
          "value": "product.certifications ~> $length($) > 0",
          "valueType": "xs:string"
        }
      ],
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Certification",
          "qualifiers": [
            {
              "kind": "TemplateQualifier",
              "type": "SMT/CollectionMappingInfo",
              "value": "product.certifications[*]",
              "valueType": "xs:string"
            }
          ],
          "value": [
            {
              "modelType": "Property",
              "idShort": "Name",
              "valueType": "xs:string",
              "qualifiers": [
                {
                  "type": "SMT/MappingInfo",
                  "value": "product.certifications[*].name"
                }
              ]
            },
            {
              "modelType": "Property",
              "idShort": "IsValid",
              "valueType": "xs:boolean",
              "qualifiers": [
                {
                  "type": "SMT/MappingInfo",
                  "value": "product.certifications[*].expiryDate > '2025-01-01'",
                  "valueType": "xs:string"
                }
              ]
            }
          ]
        }
      ]
    },
    {
      "modelType": "SubmodelElementCollection",
      "idShort": "Contacts",
      "value": [
        {
          "modelType": "SubmodelElementCollection",
          "idShort": "Contact",
          "qualifiers": [
            {
              "kind": "TemplateQualifier",
              "type": "SMT/CollectionMappingInfo",
              "value": "company.employees[*]",
              "valueType": "xs:string"
            }
          ],
          "value": [
            {
              "modelType": "Property",
              "idShort": "FullName",
              "valueType": "xs:string",
              "qualifiers": [
                {
                  "type": "SMT/MappingInfo",
                  "value": "company.employees[*].fullName"
                }
              ]
            },
            {
              "modelType": "Property",
              "idShort": "Email",
              "valueType": "xs:string",
              "qualifiers": [
                {
                  "type": "SMT/MappingInfo",
                  "value": "company.employees[*].email"
                }
              ]
            },
            {
              "modelType": "Property",
              "idShort": "EmailDomain",
              "valueType": "xs:string",
              "qualifiers": [
                {
                  "type": "SMT/MappingInfo",
                  "value": "company.employees[*].email ~> $substringAfter('@')",
                  "valueType": "xs:string"
                }
              ],
              "description": [
                {
                  "language": "en",
                  "text": "Extracted domain from email using Jsonata"
                }
              ]
            },
            {
              "modelType": "Property",
              "idShort": "Phone",
              "valueType": "xs:string",
              "qualifiers": [
                {
                  "type": "SMT/Cardinality",
                  "value": "ZeroToOne"
                },
                {
                  "type": "SMT/MappingInfo",
                  "value": "company.employees[*].phone"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

**Corresponding Input Data:**
```json
{
  "product": {
    "name": "PowerPack Pro",
    "type": "battery-powered",
    "sku": "ABC-12345",
    "warranty": {
      "monthsIncluded": 24
    },
    "battery": {
      "capacityMah": 5000,
      "chargingTimeMinutes": 120
    },
    "certifications": [
      { "name": "CE", "expiryDate": "2026-06-15" },
      { "name": "FCC", "expiryDate": "2025-12-31" }
    ]
  },
  "company": {
    "employees": [
      {
        "fullName": "Alice Johnson",
        "email": "alice@example.com",
        "phone": "+1-555-0101"
      },
      {
        "fullName": "Bob Williams",
        "email": "bob@example.com"
      }
    ]
  }
}
```

**Key Features Demonstrated:**

1. **Filter Rules**: `BatteryInfo` and `CertificationInfo` are only included when conditions are met
2. **Jsonata Functions**: String transformations (`$uppercase`, `$substring`, `$substringAfter`) and numeric operations
3. **Collections with Filtering**: Certifications collection with filter checking if array is not empty
4. **Nested Collections**: Contact collection with multiple properties including computed fields
5. **Invalid Date Comparison**: `expiryDate > '2025-01-01'` shows filtering by date

---

## Conditional Element Inclusion

### How Filters Work

When a filter rule is evaluated:

1. The **Jsonata expression** is evaluated against the input data
2. If the expression evaluates to **`true`** (or truthy), the element is **included** in the output
3. If the expression evaluates to **`false`** (or falsy), the element is **excluded** from the output

This is useful for:
- Only including optional sections when data is present
- Showing specifications only for relevant product types
- Creating conditional sections based on status or configuration

### Filter Examples

**Example: Include only for specific type**
```json
{
  "type": "SMT/FilterMappingInfo",
  "value": "product.type = 'electrical'"
}
```

**Example: Check if array has items**
```json
{
  "type": "SMT/FilterMappingInfo", 
  "value": "certifications ~> $length(certifications) > 0"
}
```

**Example: Multiple conditions**
```json
{
  "type": "SMT/FilterMappingInfo",
  "value": "product.type = 'battery' and product.warranty.monthsIncluded >= 12"
}
```

---

---

## Data Preparation

The AAS Generator expects input data as a **homogeneous JSON structure**. Before calling the API, transform your source data appropriately.

### Common Data Sources

| Source | Transformation Needed |
|--------|----------------------|
| ERP Systems (SAP, etc.) | Export to JSON, flatten nested structures |
| Excel/CSV | Convert to JSON array of objects |
| Engineering Tools (PLM, CAD) | Use tool's JSON export or build integration |
| Databases | Query and serialize to JSON |

### Data Structure Best Practices

1. **Flatten deeply nested structures** where possible
2. **Use consistent field names** across all records
3. **Ensure arrays contain homogeneous objects** (same fields in each item)
4. **Handle null/missing values** before submission (or rely on cardinality rules)

**Example transformation:**

```
Excel Row:
| Product | Serial | Contact1_Name | Contact1_Email | Contact2_Name | Contact2_Email |

Transformed JSON:
{
  "product": "Widget",
  "serial": "W-001",
  "contacts": [
    { "name": "...", "email": "..." },
    { "name": "...", "email": "..." }
  ]
}
```

---

## MultiLanguageProperty Limitation

Currently, `MultiLanguageProperty` elements support only **one language per generation call**. The language is specified in the API request body.

**Request:**
```json
{
  "blueprintsIds": ["my-blueprint"],
  "data": { "description": "Product description text" },
  "language": "en"
}
```

**Generated MLP:**
```json
{
  "modelType": "MultiLanguageProperty",
  "idShort": "Description",
  "value": [
    { "language": "en", "text": "Product description text" }
  ]
}
```

To add multiple languages, you would need to make separate API calls or edit the generated submodel afterward.

---

## API Usage

### Create a Blueprint

```http
POST /api/v2/Blueprints
Content-Type: application/json

{
  // Your blueprint JSON (see example above)
}
```

### Generate Submodel from Blueprint

```http
POST /api/v2/DataIngest/{base64EncodedAasId}
Content-Type: application/json

{
  "blueprintsIds": ["https://example.com/blueprints/contact-info-v1"],
  "data": {
    "company": {
      "name": "ACME Corporation",
      "employees": [...]
    }
  },
  "language": "en",
  "debug": true
}
```

### Debug Mode

Set `"debug": true` to receive detailed logs about the generation process. This helps diagnose mapping issues.

**Response with debug info:**
```json
{
  "results": [
    {
      "blueprintId": "contact-info-v1",
      "success": true,
      "generatedSubmodelId": "https://example.com/submodels/abc123",
      "debugInfo": {
        "logs": [
          "Started DuplicateCollectionsStep",
          "Processing collection at path 'company.employees[*]' (depth: 1, mandatory: false, elements: 2)",
          "Successfully duplicated 2 elements for collection...",
          "Finished DuplicateCollectionsStep",
          "Started MapDataToInstanceStep",
          "Successfully mapped value 'ACME Corporation' from path 'company.name'",
          "..."
        ]
      }
    }
  ]
}
```

---

## Error Handling

### Common Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `Mandatory mapping 'path' not found` | Required field missing in input data | Add data or change cardinality to `ZeroToOne` |
| `could not find matching value field` | Malformed qualifier or element structure | Verify blueprint JSON structure |
| `parent must be SubmodelElementCollection or SubmodelElementList` | Collection qualifier on wrong element type | Move qualifier to SMC or SML element |

### Error Response Format

```json
{
  "results": [
    {
      "blueprintId": "my-blueprint",
      "success": false,
      "message": "Mandatory mapping 'product.serialNumber' not found.",
      "errorInfo": {
        "logs": ["...processing steps before error..."],
        "qualifier": "SMT/MappingInfo",
        "qualifierPath": "product.serialNumber"
      }
    }
  ]
}
```

---

## Further Reading

- [API Documentation](api.md) - Complete REST API reference
- [Rules Engine Architecture](rules-engine.md) - Internal pipeline architecture (for developers)
- [XITASO Article](https://xitaso.com/automatisierte-erstellung-von-aas/) - Detailed explanation (German)
