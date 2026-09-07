# AAS Generator API Documentation

This document describes the REST API endpoints for the AAS Generator service. The API enables creating and managing Asset Administration Shells (AAS), generating Submodels from structured data using templates, and managing templates/blueprints.


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

> **⚠️ SECURITY WARNING — CHANGE THE API KEY BEFORE DEPLOYING**
>
> The API key is **no longer set** in `appsettings.json` (it ships empty) so that no predictable secret is
> committed to the repository. The development Docker Compose file still uses the well-known placeholder
> `verySecureApiKey`. **Do not deploy with this value** — anyone who knows it can access protected endpoints.
> At startup the application logs a **critical warning** when the key is empty or equals a known default.
>
> Set your own secret via the `CustomerEndpointsSecurity__ApiKey` environment variable (recommended), or by
> editing `CustomerEndpointsSecurity` in `appsettings.json`:
>
> ```bash
> # Linux / macOS / Docker Compose
> export CustomerEndpointsSecurity__ApiKey='generate-a-long-random-secret-here'
>
> # Windows PowerShell
> $env:CustomerEndpointsSecurity__ApiKey='generate-a-long-random-secret-here'
> ```

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
POST /api/v2/AasCreator/{assetIdShort}?overwrite={true|false}
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `assetIdShort` | string | Yes | The short identifier for the asset (e.g., `machine-001`) |

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `overwrite` | boolean | No | `false` | Controls what happens when an AAS with the generated ID already exists. See the detailed description below. |

##### How `overwrite` works

The creation flow always builds and validates every requested submodel **in memory first** (no repository writes), then persists submodels, then creates the shell with all submodel references baked in. The `overwrite` flag only changes how a **shell-id collision** is handled when the shell is POSTed:

- **`overwrite=false` (default)** — If a shell with the generated `aasId` already exists, the repository returns `409 Conflict`. The endpoint does **not** touch the existing shell. Any submodels that were already POSTed in this request are rolled back (best-effort delete), and the endpoint responds with `409 Conflict`. Submodels that could not be deleted are listed in `orphanedSubmodelIds`.
- **`overwrite=true`** — If the shell does not yet exist, behavior is identical to `overwrite=false`: the shell is created and the endpoint returns `201 Created` (no `previousAas`). If the shell **already exists**, the endpoint first **GETs the existing shell** (captured as `previousAas`), then **PUTs** the freshly built shell over it, and returns `200 OK` with the `previousAas` field.

Important semantics and trade-offs:

- **Overwrite is destructive for the shell, not the submodels.** PUT replaces the shell document, so the old shell's own submodel references are dropped. The submodels they pointed to are **not** deleted — they are orphaned, and the caller decides what to do with them using the returned `previousAas`.
- **`previousAas` is point-in-time, not transactional.** There is a small [TOCTOU](https://de.wikipedia.org/wiki/Time-of-Check-to-Time-of-Use-Problem) window between the GET (capture old shell) and the PUT (overwrite); a concurrent writer could change the shell in between. `previousAas` is best-effort.
- **The existing shell is never deleted.** On any failure (submodel persistence or shell PUT), only submodels created during *this* request are rolled back; a pre-existing shell is left intact.

#### Request Body (Optional)

If you want to create an AAS with submodels, include a JSON body.

**Minimal example:**

```json
{
  "blueprintsIds": ["blueprint-id-1", "blueprint-id-2"],
  "data": {
    "serialNumber": "SN-12345",
    "manufacturer": "ACME Corp"
  },
  "language": "en"
}
```

**Complete example with all optional fields:**

```json
{
  "blueprintsIds": ["blueprint-id-1", "blueprint-id-2"],
  "data": {
    "serialNumber": "SN-12345",
    "manufacturer": "ACME Corp"
  },
  "language": "en",
  "debug": false,
  "globalAssetId": "https://example.com/assets/my-custom-asset-id",
  "assetKind": "Instance",
  "extensions": {
    "manufacturer": "ACME Corp",
    "location": "Building A",
    "department": "Production"
  },
  "specificAssetIds": [
    {
      "name": "SerialNumber",
      "value": "SN-12345"
    },
    {
      "name": "PartNumber",
      "value": "PN-ABC-001"
    },
    {
      "name": "BatchNumber",
      "value": "BATCH-2024-001"
    }
  ],
  "administration": {
    "version": "1.0",
    "revision": "3"
  },
  "defaultThumbnail": {
    "path": "https://example.com/images/asset-thumbnail.png",
    "contentType": "image/png"
  },
  "derivedFrom": "https://example.com/aas/parent-machine-template",
  "submodelIds": [
    "https://example.com/submodels/existing-sm-1",
    "https://example.com/submodels/existing-sm-2"
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `blueprintsIds` | string[] | No | List of blueprint IDs to use for submodel generation |
| `data` | object | No | JSON data to map into the submodel templates |
| `language` | string | No | Language code for MultiLanguageProperties (e.g., `"en"`, `"de"`) |
| `debug` | boolean | No | Include workflow logs in response (default: `false`). When enabled, the response includes a chronological log trail spanning all generation phases: blueprint retrieval, ID generation, data mapping, and repository persistence. |
| `globalAssetId` | string | No | Custom globalAssetId to use directly instead of generating one. If omitted, the globalAssetId is auto-generated from the configured prefix and ID generation rules. |
| `assetKind` | string | No | AssetKind for the AAS: `"Instance"`, `"Type"`, or `"NotApplicable"` (default: `"Instance"`). Specifies whether the AAS represents a concrete asset instance, a type/template, or if asset classification is not applicable. |
| `extensions` | object | No | Key-value pairs to add as extensions to the AAS root level. Each entry will be added as `{"name": "key", "value": "value"}` in the extensions array. **Note:** This is a simplified implementation supporting only string name-value pairs. The full AAS specification includes additional fields (`valueType`, `refersTo`, `semanticId`) which are not supported. Extension names must be 1-128 characters. |
| `specificAssetIds` | array | No | Array of specific asset identifier objects to add to the asset information. Each object must have `name` and `value` properties. These identifiers are used to identify the asset in specific contexts (e.g., serial numbers, part numbers). The default `assetIdShort` identifier is always included. Example: `[{"name": "SerialNumber", "value": "12345"}, {"name": "PartNumber", "value": "ABC-001"}]` |
| `administration` | object | No | Administrative information for the AAS (version and revision). Object with `version` (required, string) and `revision` (optional, string) properties. Example: `{"version": "1.0", "revision": "2"}`. This is added at the AAS root level according to IDTA AAS specification. |
| `defaultThumbnail` | object | No | Default thumbnail for the AAS asset information. Matches the AAS v3 Resource schema: `path` (required) and `contentType` (optional). |
| `derivedFrom` | string | No | Parent AAS ID from which this AAS is derived. Used for navigating product family hierarchies and inheritance relationships. The value is converted to the proper AAS Metamodel v3.0 reference structure with a keys array. Example: `"https://example.com/aas/parent-template"`. Only basic format validation is performed (non-empty string); the parent AAS existence is not validated. |
| `submodelIds` | string[] | No | List of submodel IDs to link to the new AAS. IDs are plain (not base64-encoded). Existence is not validated; a linked submodel that is not in the repository yields a dangling reference. |

#### Response

**Success (201 Created)** — when a new AAS is created. The `previousAas` field is absent.

**Success (200 OK)** — when an existing AAS is replaced (`overwrite=true`). The response includes the `previousAas` field capturing the shell as it was before the overwrite.

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


**Conflict (409 Conflict)**

Returned when an AAS with the generated ID already exists and `overwrite=false`. The response body contains an `error` message and any `orphanedSubmodelIds` that could not be rolled back. See the `overwrite` section above for details.

**Error (400 Bad Request)**

Returned when submodel generation or input validation fails.

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
| `debug` | boolean | No | Include workflow logs in response (default: `false`). When enabled, the response includes a chronological log trail spanning all generation phases: blueprint retrieval, ID generation, data mapping, and repository persistence. |

#### Response

**Success (200 OK)** — with `debug: true`

```json
{
  "results": [
    {
      "blueprintId": "contact-template-v1",
      "success": true,
      "message": "",
      "generatedSubmodelId": "https://example.com/submodels/contact-001",
      "debugInfo": {
        "logs": [
          "INFO [2026-04-24T10:30:01.0000000Z] - Mapping blueprint contact-template-v1 to AAS aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvbXktbWFjaGluZQ==",
          "INFO [2026-04-24T10:30:01.1000000Z] - Fetching blueprint: contact-template-v1",
          "INFO [2026-04-24T10:30:01.2000000Z] - Blueprint fetched successfully",
          "INFO [2026-04-24T10:30:01.2100000Z] - Extracted idShort: ContactInformation",
          "INFO [2026-04-24T10:30:01.2200000Z] - Generating submodel ID",
          "INFO [2026-04-24T10:30:01.3000000Z] - Submodel ID generated: https://example.com/submodels/contact-001",
          "INFO [2026-04-24T10:30:01.3100000Z] - Starting data mapping",
          "INFO [2026-04-24T10:30:01.3200000Z] - Started ValidateBlueprintStep",
          "INFO [2026-04-24T10:30:01.3210000Z] - Finished ValidateBlueprintStep",
          "INFO [2026-04-24T10:30:01.3220000Z] - Started DeepCloneBlueprintStep",
          "INFO [2026-04-24T10:30:01.3230000Z] - Finished DeepCloneBlueprintStep",
          "INFO [2026-04-24T10:30:01.3240000Z] - Started SetKindInstanceStep",
          "INFO [2026-04-24T10:30:01.3250000Z] - Finished SetKindInstanceStep",
          "INFO [2026-04-24T10:30:01.3260000Z] - Started DuplicateCollectionsStep",
          "INFO [2026-04-24T10:30:01.3270000Z] - Finished DuplicateCollectionsStep",
          "INFO [2026-04-24T10:30:01.3280000Z] - Started FilterElementsStep",
          "INFO [2026-04-24T10:30:01.3290000Z] - Finished FilterElementsStep",
          "INFO [2026-04-24T10:30:01.3300000Z] - Started DiscoverMappingDescriptorsStep",
          "INFO [2026-04-24T10:30:01.3310000Z] - Finished DiscoverMappingDescriptorsStep",
          "INFO [2026-04-24T10:30:01.3320000Z] - Started ResolveMappingExpressionsStep",
          "INFO [2026-04-24T10:30:01.3330000Z] - Finished ResolveMappingExpressionsStep",
          "INFO [2026-04-24T10:30:01.3340000Z] - Started AssignMappedFieldsStep",
          "INFO [2026-04-24T10:30:01.3350000Z] - Finished AssignMappedFieldsStep",
          "INFO [2026-04-24T10:30:01.3360000Z] - Started RemoveTopLevelQualifiersStep",
          "INFO [2026-04-24T10:30:01.3370000Z] - Finished RemoveTopLevelQualifiersStep",
          "INFO [2026-04-24T10:30:01.3380000Z] - Started ReplaceIdentificationStep",
          "INFO [2026-04-24T10:30:01.3390000Z] - Finished ReplaceIdentificationStep",
          "INFO [2026-04-24T10:30:01.3600000Z] - Data mapping completed",
          "INFO [2026-04-24T10:30:02.0000000Z] - Posting submodel to repository",
          "INFO [2026-04-24T10:30:02.1000000Z] - Adding submodel reference to shell",
          "INFO [2026-04-24T10:30:02.2000000Z] - Submodel reference added to shell"
        ]
      }
    }
  ]
}
```

**Success (200 OK)** — with `debug: false` (default)

```json
{
  "results": [
    {
      "blueprintId": "contact-template-v1",
      "success": true,
      "message": "",
      "generatedSubmodelId": "https://example.com/submodels/contact-001"
    }
  ]
}
```

> **Note**: When `debug` is `false` or omitted, the `debugInfo` field is `null` and omitted from the response.

**Error (400 Bad Request)**

On error, `errorInfo.logs` always contains the workflow log trail up to (and including) the failure point, regardless of the `debug` flag:

```json
{
  "results": [
    {
      "blueprintId": "contact-template-v1",
      "success": false,
      "message": "Missing required data at path: contacts.name",
      "generatedSubmodelId": "",
      "errorInfo": {
        "logs": [
          "INFO [2026-04-24T10:30:01.0000000Z] - Mapping blueprint contact-template-v1 to AAS aHR0cHM6Ly9leGFtcGxlLmNvbS9hYXMvbXktbWFjaGluZQ==",
          "INFO [2026-04-24T10:30:01.1000000Z] - Fetching blueprint: contact-template-v1",
          "INFO [2026-04-24T10:30:01.2000000Z] - Blueprint fetched successfully",
          "INFO [2026-04-24T10:30:01.2200000Z] - Generating submodel ID",
          "INFO [2026-04-24T10:30:01.3000000Z] - Submodel ID generated: https://example.com/submodels/contact-001",
          "INFO [2026-04-24T10:30:01.3100000Z] - Starting data mapping",
          "ERROR [2026-04-24T10:30:01.3200000Z] - Data mapping failed: Missing required data at path: contacts.name"
        ],
        "qualifier": "MnestixAASGenerator/MappingInfo",
        "qualifierPath": "contacts.name"
      }
    }
  ]
}
```

**Error (500 Internal Server Error)**

Returned when one or more blueprints fail structural validation at generation time. This happens when a stored blueprint predates the current validation rules or was modified outside the API and is now in an invalid state. The response contains the aggregated `errors` and the per-blueprint `results`:

```json
{
  "errors": [
    {
      "rule": "FieldNotApplicableToModelType",
      "path": "ContactInformation > ContactName",
      "message": "Field 'first' is not valid on model type 'Property'. Allowed fields: value, idShort, displayName."
    }
  ],
  "results": []
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

Creates a new blueprint from a Submodel template. The blueprint is validated before being stored — if structural errors are detected, a `422 Unprocessable Entity` response is returned listing all issues.

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
          "type": "MnestixAASGenerator/MappingInfo",
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

**Validation Error (422 Unprocessable Entity)**

Returned when the blueprint contains structural errors that would cause generation failures. All detected issues are returned at once.

```json
{
  "errors": [
    {
      "rule": "UnknownFieldName",
      "path": "ContactInformation > ContactName",
      "message": "Field 'foobar' is not a recognized mapping field. Allowed: value, idShort, displayName, multiLanguage, globalAssetId, entityType, first, second."
    },
    {
      "rule": "InvalidJsonataSyntax",
      "path": "ContactInformation > Email",
      "message": "Invalid JSONata syntax: Expected ']' at position 6."
    }
  ]
}
```

**Repository Error (4xx)**

If validation passes but the BaSyx repository rejects the blueprint, the original status code and response body from the repository are relayed as-is.

### Update Blueprint

Updates an existing blueprint. The same validation rules apply as for creation.

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

**Validation Error (422 Unprocessable Entity)**

Same format as Create Blueprint validation errors.

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

> **Limit:** `count` must be between `1` and `1000`. A value above `1000` is rejected with `400 Bad Request`
> rather than returning a truncated list. This bound prevents an oversized request from exhausting server memory.

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
| `MnestixAASGenerator/MappingInfo` | Map JSON path or Jsonata expression to element value | `"car.serialNo"` or `"$string(quantity)"` |
| `MnestixAASGenerator/CollectionMappingInfo` | Duplicate elements for arrays | `"car.contacts[*]"` |
| `MnestixAASGenerator/FilterMappingInfo` | Conditional element creation using boolean expressions | `"car.engineType = 'electric'"` |
| `SMT/Cardinality` | Define required/optional data | `"One"` or `"ZeroToOne"` |

### Path Expression Syntax

Path mappings support both simple JSON paths and advanced Jsonata expressions:

**Simple Paths:**
- `data.field` - Simple field access
- `data.nested.field` - Nested object access
- `data.array[*]` - Array iteration (for collections)
- `data.array[0]` - Specific array index

**Jsonata Expressions** (for `MnestixAASGenerator/MappingInfo`):
- `$length(data.field)` - String/array length
- `$substring(data.field, 0, 3)` - Extract substring
- `data.field ~> $contains('text')` - Check if contains (returns boolean)
- `$string(data.number)` - Convert number to string
- `data.numA > data.numB` - Numeric comparison (returns boolean)
- `$uppercase($substring(data.code, 0, 3))` - Chained operations

See [Blueprint and Rules](Blueprint-and-Rules#jsonata-expressions-in-mapping-rules) for comprehensive Jsonata function reference.

### Cardinality Values

| Value | Behavior |
|-------|----------|
| `One` | Mandatory - throws error if data is missing |
| `ZeroToOne` | Optional - sets empty value if data is missing |
| `OneToMany` | Mandatory collection - at least one item required |
| `ZeroToMany` | Optional collection - empty array is allowed |

> **Note**: Cardinality values are case-sensitive and validated at blueprint save time. Invalid values (e.g. `"one"`, `"Optional"`) will be rejected with a `422` response.

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
| 422 Unprocessable Entity | Blueprint validation failed (structural errors detected) |
| 500 Internal Server Error | Server-side error |

### Validation Error Response Format (422)

Returned by the Blueprint Create/Update endpoints when structural issues are detected:

```json
{
  "errors": [
    {
      "rule": "InvalidQualifierSegmentCount",
      "path": "Nameplate > SerialNumber",
      "message": "Qualifier type 'MnestixAASGenerator/MappingInfo/value/extra' has 4 segments; expected at most 3."
    }
  ]
}
```

The `rule` field is a machine-readable enum. The `path` field uses the idShort breadcrumb trail of the element (e.g. `ParentCollection > ChildElement`), falling back to JSON pointer notation if idShort is unavailable.

### Standard Error Response Format

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
      "qualifiers": [{"type": "MnestixAASGenerator/MappingInfo", "value": "manufacturer.name"}]
    },
    {
      "idShort": "SerialNumber",
      "modelType": "Property",
      "valueType": "xs:string",
      "qualifiers": [{"type": "MnestixAASGenerator/MappingInfo", "value": "serialNumber"}]
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

### Create AAS with Custom Global Asset ID

If you already have an external asset identifier (e.g., from an ERP system), you can pass it directly as the `globalAssetId`:

```http
POST /api/v2/AasCreator/my-machine
Content-Type: application/json

{
  "globalAssetId": "https://erp.acme.com/assets/ASSET-2026-0042",
  "blueprintsIds": ["https://example.com/blueprints/nameplate-v1"],
  "data": {
    "manufacturer": {"name": "ACME Corp"},
    "serialNumber": "SN-12345"
  },
  "language": "en"
}
```

The `globalAssetId` field is used as-is for the AAS `assetInformation.globalAssetId`. All other IDs (`aasId`, `aasIdShort`, `assetIdShort`) are still generated normally from the `assetIdShort` path parameter.

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
  "qualifiers": [{"type": "MnestixAASGenerator/CollectionMappingInfo", "value": "contacts[*]"}],
  "value": [
    {
      "idShort": "Name",
      "modelType": "Property",
      "qualifiers": [{"type": "MnestixAASGenerator/MappingInfo", "value": "contacts[*].name"}]
    },
    {
      "idShort": "Email", 
      "modelType": "Property",
      "qualifiers": [{"type": "MnestixAASGenerator/MappingInfo", "value": "contacts[*].email"}]
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
