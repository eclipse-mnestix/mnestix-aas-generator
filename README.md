# Mnestix AAS Generator

The Mnestix AAS Generator enables automated creation of Asset Administration Shells and Submodels from structured data using a rules-based template system.

> **Project History:** This repository was previously the unified Mnestix Backend. It has since been split into two focused components:
> - **[Mnestix AAS Generator](https://github.com/eclipse-mnestix/mnestix-aas-generator)** (this repo) - AAS/Submodel creation and data ingest
> - **[Mnestix Proxy](https://github.com/eclipse-mnestix/mnestix-proxy)** - Repository proxy with search, discovery, and access control

## Features

- **AAS Creation** (`POST /api/v2/AasCreator/{assetIdShort}`) - Create AAS with optional automatic Submodel generation
- **Data Ingest** (`POST /api/v2/DataIngest/{aasId}`) - Generate Submodels from Blueprints using structured JSON data
- **Blueprints & Templates** - Manage Submodel templates with embedded mapping rules
- **ID Generator** - Generate standardized identifiers for AAS and Submodels

> **Note:** The proxy functionality has been moved to [Mnestix Proxy](https://github.com/eclipse-mnestix/mnestix-proxy).

## Documentation

- [API Reference](https://github.com/eclipse-mnestix/mnestix-aas-generator/wiki/API-Documentation) - Complete REST API documentation
- [Blueprint & Rules Guide](https://github.com/eclipse-mnestix/mnestix-aas-generator/wiki/Blueprint-and-Rules) - How to create and configure Blueprints
- [Rules Engine Architecture](https://github.com/eclipse-mnestix/mnestix-aas-generator/wiki/Rules-Engine-Architecture) - Internal pipeline architecture (for developers)

## Build & Run locally

Run the following to start locally. You need a running BaSyx repository + MongoDB.

```bash
dotnet run --project MnestixApi --watch
```

Or run in Rider with the 'MnestixApi:Mnestix' Configuration.

## Start as Docker container

To start the AAS Generator with BaSyx in Docker:

```bash
docker compose -f ./docker-compose/compose.dev.yml up
```

Access Swagger UI at: http://localhost:5064/swagger

## Configuration

### Feature Flags

Configure in `MnestixApi/appsettings.json`:

| Flag | Default | Description |
|------|---------|-------------|
| `Features__UseAuthentication` | `false` | Enable/disable authentication |
| `Features__RequiredShells` | `true` | Initialize required AAS shells on startup |
| `Features__AddExampleAas` | `true` | Also initialize demo/example AAS (`lni0729`, `Mnestix`) when `Features__RequiredShells` is enabled. Configuration, DefaultTemplate and CustomTemplate are unaffected. |

### Repository Connection

- `ServerUrls` - URL to the AAS/Submodel repository (or Mnestix Proxy)

Example: `"ServerUrls": "http://localhost:5065/repo/"`

### Separate Blueprint/Template Repositories (Optional)

- `Configuration__SubmodelTemplatesApiUrl` - Dedicated repository for templates
- `Configuration__SubmodelBlueprintsApiUrl` - Dedicated repository for blueprints

## Authentication

Authentication is disabled by default. Set `Features__UseAuthentication` to `true` to enable.

### API Key Authentication

The simplest method. Configure in `appsettings.json`:


```json
{
  "CustomerEndpointsSecurity": {
    "ApiKey": "your-secret-api-key"
  }
}
```

Clients include the key in requests:
```
X-API-KEY: your-secret-api-key
```

> **Note:** GET and HEAD requests do not require an API key. Only modifying requests (POST, PUT, PATCH, DELETE) require authentication.

### Microsoft Entra ID (Azure AD)

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "ClientId": "your-client-id",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id"
  }
}
```

| Parameter | Description |
|-----------|-------------|
| `Instance` | Microsoft Entra ID authorization server URL |
| `ClientId` | Your application's unique ID |
| `Domain` | Your tenant domain name |
| `TenantId` | Your tenant's unique ID |

### OpenID Connect (e.g., Keycloak)

```json
{
  "OpenId": {
    "EnableOpenIdAuth": true,
    "Issuer": "http://localhost:8080/realms/Mnestix",
    "ClientID": "mnestixApi-demo",
    "RequireHttpsMetadata": false
  }
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `EnableOpenIdAuth` | Enable OIDC authentication (disables Azure AD) | `false` |
| `Issuer` | OIDC provider issuer URL | - |
| `ClientID` | Application client ID | - |
| `RequireHttpsMetadata` | Require HTTPS for metadata (set `true` in production) | `false` |

### Repository Authentication (for secured BaSyx)

When BaSyx requires authentication:

```json
{
  "RepositoryOpenIdConnect": {
    "EnableRepositoryOpenIdAuth": true,
    "Authority": "http://localhost:8080/realms/Mnestix",
    "DiscoveryEndpoint": ".well-known/openid-configuration",
    "ClientId": "mnestix-repo-client",
    "ClientSecret": "your-secret",
    "ValidateIssuer": false,
    "TokenEndpoint": ""
  }
}
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `EnableRepositoryOpenIdAuth` | Enable repository authentication | `false` |
| `Authority` | OIDC provider authority URL | - |
| `DiscoveryEndpoint` | OIDC discovery endpoint | `.well-known/openid-configuration` |
| `ClientId` | Client ID for repository access | - |
| `ClientSecret` | Client secret (if required) | - |
| `ValidateIssuer` | Validate token issuer (recommended `true` in production) | `false` |
| `TokenEndpoint` | Explicit token endpoint (leave empty in production) | - |

> ⚠️ **Production Note:** Leave `TokenEndpoint` empty in production to use OIDC discovery mechanisms.
