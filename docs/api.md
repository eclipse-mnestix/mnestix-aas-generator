# AAS Generator API Documentation

This document describes the REST API endpoints for the AAS Generator service. The API enables creating and managing Asset Administration Shells (AAS), generating Submodels from structured data using templates, and managing templates/blueprints.
_This API-File should also be published within GitHub._

You can find an interactive version of this documentation in the Swagger UI at `http://localhost:5064/swagger`, which includes example requests and responses for each endpoint. 

## Base URL

```
/api/v2/
```

> **Note**: API v1 endpoints are deprecated. Use v2 endpoints for all new integrations.

## Authentication

All API endpoints require authentication. The API supports two authentication schemes:

1. **API Key Authentication** - Include the API key in the `X-API-KEY` header
2. **JWT Bearer Token** - Include a valid JWT token in the `Authorization: Bearer <token>` header

Configure authentication in `appsettings.json`:
- Set `Features__UseAuthentication` to `true` to enable authentication
- Set `CustomerEndpointsSecurity__ApiKey` for API key authentication
- Configure `AzureAd` or `OpenId` sections for OAuth/OIDC authentication

---

## AAS Creator

Create new Asset Administration Shells with optional auto-generated Submodels.

### Create AAS

Creates a new AAS for a given asset identifier. Optionally generates and attaches submodels if blueprint parameters are provided.

```http
POST /api/v2/AasCreator/{assetIdShort}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assetIdShort` | string | Yes | The short identifier for the asset (e.g., `machine-001`) |

#### Request Body (Optional)

If you want to create an AAS with submodels, include a JSON body:

```json
{
  "blueprintsIds": ["blueprint-id-1", "blueprint-id-2"],
  "data": {
    "serialNumber": "SN-12345",
    "manufacturer": "ACME Corp"
  },
  "language": "en",
  "debug": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `blueprintsIds` | string[] | No | List of blueprint IDs to use for submodel generation |
| `data` | object | No | JSON data to map into the submodel templates |
| `language` | string | No | Language code for MultiLanguageProperties (e.g., `"en"`, `"de"`) |
| `debug` | boolean | No | Include debug logs in response (default: `false`) |

#### Response

**Success (200 OK)**

```json
{
  "assetId": "https://example.com/assets/machine-001",
  "base64EncodedAssetId": "aHR0cHM6Ly9leGFtcGxlLmNvbS9hc3NldHMvbWFjaGluZS0wMDE=",
  "aasId": "https://example.com/aas/machine-001",
  "base64EncodedAasId": "aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvbWFjaGluZS0wMDE=",
  "aasRepoUrl": "http://localhost:8081",
  "submodelResults": [
    {
      "blueprintId": "nameplate-v1",
      "success": true,
      "message": "",
      "generatedSubmodelId": "https://example.com/submodels/nameplate-001"
    }
  ]
}
```

**Error (400 Bad Request)**

Returned when an AAS with the generated ID already exists or submodel generation fails.

---

## Data Ingest (Submodel Generation)

Generate and add Submodels to an existing AAS using the rules engine.

### Add Data to AAS

Takes blueprint templates and maps data from the provided JSON into them, then stores the generated submodels in the specified AAS.

```http
POST /api/v2/DataIngest/{base64EncodedAasId}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `base64EncodedAasId` | string | Yes | The AAS ID encoded in Base64 URL format |

#### Request Body

```json
{
  "blueprintsIds": ["contact-template-v1", "nameplate-template-v1"],
  "data": {
    "contacts": [
      {"name": "John Doe", "email": "john@example.com"},
      {"name": "Jane Smith", "email": "jane@example.com"}
    ],
    "manufacturer": "ACME Corp",
    "serialNumber": "SN-12345"
  },
  "language": "en",
  "debug": false
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `blueprintsIds` | string[] | Yes | List of blueprint IDs to generate submodels from |
| `data` | object | Yes | JSON data to map into templates. Use `{}` if no mapping is defined. |
| `language` | string | Yes | Language code for MultiLanguageProperties (e.g., `"en"`, `"de"`) |
| `debug` | boolean | No | Include debug logs in response (default: `false`) |

#### Response

**Success (200 OK)**

```json
{
  "results": [
    {
      "blueprintId": "contact-template-v1",
      "success": true,
      "message": "",
      "generatedSubmodelId": "https://example.com/submodels/contact-001",
      "debugInfo": {
        "logs": ["Step 1: DeepCloneTemplate completed", "..."]
      }
    }
  ]
}
```

**Error (400 Bad Request)**

```json
{
  "results": [
    {
      "blueprintId": "contact-template-v1",
      "success": false,
      "message": "Missing required data at path: contacts.name",
      "generatedSubmodelId": "",
      "errorInfo": {
        "logs": ["Error occurred during mapping"],
        "qualifier": "SMT/MappingInfo",
        "qualifierPath": "contacts.name"
      }
    }
  ]
}
```

---

## Blueprints

Blueprints are customized Submodel templates with embedded mapping rules. They define how structured data is transformed into AAS Submodels.

### Get All Blueprints

Returns all available blueprints.

```http
GET /api/v2/Blueprints
```

#### Response

**Success (200 OK)**

Returns an array of blueprint Submodels in JSON format.

### Get Blueprint by ID

Returns a specific blueprint by its ID.

```http
GET /api/v2/Blueprints/{base64EncodedBlueprintId}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `base64EncodedBlueprintId` | string | Yes | The blueprint ID encoded in Base64 URL format |

#### Response

**Success (200 OK)**

Returns the blueprint Submodel in JSON format.

**Error (400 Bad Request)**

Blueprint not found or invalid ID.

### Create Blueprint

Creates a new blueprint from a Submodel template.

```http
POST /api/v2/Blueprints
```

#### Request Body

A complete Submodel JSON object with `kind: "Template"` and embedded Template Qualifiers for mapping rules.

```json
{
  "idShort": "ContactInformation",
  "id": "https://example.com/blueprints/contact-v1",
  "kind": "Template",
  "semanticId": {
    "type": "ExternalReference",
    "keys": [{"type": "GlobalReference", "value": "https://admin-shell.io/zvei/nameplate/1/0/ContactInformations"}]
  },
  "submodelElements": [
    {
      "idShort": "ContactName",
      "modelType": "Property",
      "valueType": "xs:string",
      "qualifiers": [
        {
          "type": "SMT/MappingInfo",
          "value": "contact.name"
        }
      ]
    }
  ]
}
```

#### Response

**Success (200 OK)**

Returns the identifier of the newly created blueprint.

### Update Blueprint

Updates an existing blueprint.

```http
POST /api/v2/Blueprints/{submodelId}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `submodelId` | string | Yes | The ID of the blueprint to update |

#### Request Body

The updated blueprint Submodel as JSON.

#### Response

**Success (204 No Content)**

### Delete Blueprint

Deletes a blueprint.

```http
DELETE /api/v2/Blueprints/{base64EncodedBlueprintId}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `base64EncodedBlueprintId` | string | Yes | The blueprint ID encoded in Base64 URL format |

#### Response

- **204 No Content** - Deletion successful
- **404 Not Found** - Blueprint does not exist
- **400 Bad Request** - Invalid ID format

---

## Templates

Templates are standard Submodel templates from external sources or the templates AAS. Unlike blueprints, templates may be read-only if sourced from an external API.

### Get All Templates

Returns all available templates.

```http
GET /api/v2/Templates
```

#### Response

**Success (200 OK)**

Returns an array of template Submodels.

**Error (404 Not Found)**

Templates could not be retrieved.

### Create Template

Creates a new template in the local templates AAS.

```http
POST /api/v2/Templates
```

> **Note**: This endpoint is disabled when `SubmodelTemplatesApiUrl` is configured. In that case, use the remote templates API.

#### Request Body

A complete Submodel template as JSON.

#### Response

- **204 No Content** - Template created successfully
- **403 Forbidden** - Remote templates API is configured; use that instead
- **400 Bad Request** - Invalid template format

---

## ID Generator

Generate standardized identifiers for AAS and Submodels.

### Generate AAS IDs with Asset ID Short

Generates a complete set of IDs for creating a new AAS based on the provided asset identifier.

```http
GET /api/v2/IdGenerator/aasIds/{assetIdShort}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assetIdShort` | string | Yes | The short identifier for the asset |

#### Response

**Success (200 OK)**

```json
{
  "assetId": "https://example.com/assets/machine-001",
  "assetIdShort": "machine-001",
  "aasId": "https://example.com/aas/machine-001",
  "aasIdShort": "aas_machine-001"
}
```

### Generate AAS IDs (Auto-generated)

Generates a complete set of IDs with an auto-generated unique identifier.

```http
GET /api/v2/IdGenerator/aasIds/
```

#### Response

**Success (200 OK)**

```json
{
  "assetId": "https://example.com/assets/xdtzq0F",
  "assetIdShort": "xdtzq0F",
  "aasId": "https://example.com/aas/xdtzq0F",
  "aasIdShort": "aas_xdtzq0F"
}
```

### Generate Submodel IDs

Generates the specified number of unique Submodel IDs.

```http
GET /api/v2/IdGenerator/submodelIds/{count}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `count` | integer | Yes | Number of Submodel IDs to generate |

#### Response

**Success (200 OK)**

```json
[
  "https://example.com/submodels/abc123",
  "https://example.com/submodels/def456"
]
```

---

## Configuration

Manage ID generation configuration settings.

### Get ID Configuration

Retrieves the current ID generation configuration settings.

```http
GET /api/v2/Configuration
```

#### Response

**Success (200 OK)**

Returns the configuration settings as JSON.

**Error (404 Not Found)**

Configuration not found.

### Update ID Configuration

Applies a partial update to a specific ID generation setting.

```http
PATCH /api/v2/Configuration?idShortPath={path}&value={value}
```

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `idShortPath` | string | Yes | The path to the setting within the submodel elements |
| `value` | string | Yes | The new value to apply |

#### Response

- **204 No Content** - Update successful
- **404 Not Found** - Setting not found

---

## AAS Relationships

Navigate relationships between Asset Administration Shells.

### Get Derived From

Returns all AAS instances that have a direct `derivedFrom` relationship to the specified AAS.

```http
GET /api/v2/AasRelationship/GetDerivedFrom?aasId={aasId}
```

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `aasId` | string | Yes | The ID of the AAS to find inheritors for |

#### Response

**Success (200 OK)**

```json
[
  {
    "aasId": "https://example.com/aas/derived-001",
    "assetIdShort": "derived-asset-001"
  }
]
```

**Error (400 Bad Request)**

AAS ID is missing or invalid.

---

## Template Qualifiers (Rules Engine)

Blueprints use Template Qualifiers to define mapping rules. These qualifiers are embedded in Submodel elements.

### Qualifier Format

```json
{
  "type": "SMT/<RuleType>",
  "value": "<rule-configuration>"
}
```

### Available Rule Types

| Qualifier Type | Purpose | Example Value |
|----------------|---------|---------------|
| `SMT/MappingInfo` | Map JSON path or Jsonata expression to element value | `"car.serialNo"` or `"$string(quantity)"` |
| `SMT/CollectionMappingInfo` | Duplicate elements for arrays | `"car.contacts[*]"` |
| `SMT/FilterMappingInfo` | Conditional element creation using boolean expressions | `"car.engineType = 'electric'"` |
| `SMT/Cardinality` | Define required/optional data | `"One"` or `"ZeroToOne"` |

### Path Expression Syntax

Path mappings support both simple JSON paths and advanced Jsonata expressions:

**Simple Paths:**
- `data.field` - Simple field access
- `data.nested.field` - Nested object access
- `data.array[*]` - Array iteration (for collections)
- `data.array[0]` - Specific array index

**Jsonata Expressions** (for `SMT/MappingInfo`):
- `$length(data.field)` - String/array length
- `$substring(data.field, 0, 3)` - Extract substring
- `data.field ~> $contains('text')` - Check if contains (returns boolean)
- `$string(data.number)` - Convert number to string
- `data.numA > data.numB` - Numeric comparison (returns boolean)
- `$uppercase($substring(data.code, 0, 3))` - Chained operations

See [generator-rules.md](generator-rules.md#jsonata-expressions-in-mapping-rules) for comprehensive Jsonata function reference.

### Cardinality Values

| Value | Behavior |
|-------|----------|
| `One` | Mandatory - throws error if data is missing |
| `ZeroToOne` | Optional - sets empty value if data is missing |

---

## Error Handling

All endpoints return standard HTTP status codes:

| Status Code | Description |
|-------------|-------------|
| 200 OK | Request successful |
| 204 No Content | Request successful, no content returned |
| 400 Bad Request | Invalid request parameters or body |
| 401 Unauthorized | Missing or invalid authentication |
| 403 Forbidden | Insufficient permissions |
| 404 Not Found | Resource not found |
| 500 Internal Server Error | Server-side error |

### Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Detailed error message"
}
```

---

## Examples

### Complete Workflow: Create AAS with Submodels

1. **Create a blueprint** with mapping rules:

```http
POST /api/v2/Blueprints
Content-Type: application/json

{
  "idShort": "Nameplate",
  "id": "https://example.com/blueprints/nameplate-v1",
  "kind": "Template",
  "submodelElements": [
    {
      "idShort": "ManufacturerName",
      "modelType": "Property",
      "valueType": "xs:string",
      "qualifiers": [{"type": "SMT/MappingInfo", "value": "manufacturer.name"}]
    },
    {
      "idShort": "SerialNumber",
      "modelType": "Property",
      "valueType": "xs:string",
      "qualifiers": [{"type": "SMT/MappingInfo", "value": "serialNumber"}]
    }
  ]
}
```

2. **Create an AAS with auto-generated submodel**:

```http
POST /api/v2/AasCreator/my-machine
Content-Type: application/json

{
  "blueprintsIds": ["https://example.com/blueprints/nameplate-v1"],
  "data": {
    "manufacturer": {"name": "ACME Corp"},
    "serialNumber": "SN-12345"
  },
  "language": "en"
}
```

### Add Submodel to Existing AAS

```http
POST /api/v2/DataIngest/aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvbXktbWFjaGluZQ==
Content-Type: application/json

{
  "blueprintsIds": ["contact-template-v1"],
  "data": {
    "contacts": [
      {"name": "Support", "email": "support@acme.com"}
    ]
  },
  "language": "en"
}
```

### Collection Mapping Example

Blueprint with collection mapping:

```json
{
  "idShort": "ContactPerson",
  "modelType": "SubmodelElementCollection",
  "qualifiers": [{"type": "SMT/CollectionMappingInfo", "value": "contacts[*]"}],
  "value": [
    {
      "idShort": "Name",
      "modelType": "Property",
      "qualifiers": [{"type": "SMT/MappingInfo", "value": "contacts[*].name"}]
    },
    {
      "idShort": "Email", 
      "modelType": "Property",
      "qualifiers": [{"type": "SMT/MappingInfo", "value": "contacts[*].email"}]
    }
  ]
}
```

Input data:
```json
{
  "contacts": [
    {"name": "John", "email": "john@example.com"},
    {"name": "Jane", "email": "jane@example.com"}
  ]
}
```

Generated output creates `ContactPerson_0` and `ContactPerson_1` collections with mapped values.

---

## Interactive Documentation

For interactive API exploration, access the Swagger UI at:

```
/swagger
```

This provides a complete OpenAPI specification with the ability to test endpoints directly.
