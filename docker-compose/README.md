[![Made by XITASO](https://img.shields.io/badge/Made_by_XITASO-0d4453?style=flat-square)](https://xitaso.com/)
[![!ASP.NET](https://img.shields.io/badge/ASP.NET_core-.NET_8-0d4453?style=flat-square)]()

# What is Mnestix AAS Generator?

The Mnestix AAS Generator enables automated creation of Asset Administration Shells and Submodels from structured data. Built with ASP.NET Core 8, it offers the following features:

- **AAS Creation Endpoint**: Create Asset Administration Shells (AAS) using only the assetIdShort, with optional automatic Submodel generation.
- **Data Ingest Endpoint**: Generate Submodels from Blueprints and map structured JSON data into them using a rules engine.
- **Blueprints & Templates**: Manage Submodel templates with embedded mapping rules for automated data transformation.
- **AasRelationship Endpoint**: Navigate `derivedFrom` relationships between AAS.
- **ID Generator**: Generate standardized identifiers for AAS and Submodels.

> **Note:** The proxy functionality has been moved to a separate repository: [Mnestix Proxy](https://github.com/eclipse-mnestix/mnestix-proxy)

# Run Mnestix AAS Generator locally

## Requirements
- Linux or WSL
- Docker
- Docker-Compose

## Instructions:

1. **Copy the compose.yml code**

   Found at the bottom of this guide.

2. **Create the compose.yml file**

   Create a file in your local directory with the name compose.yml and paste the copied code in there.

3. **Run the application**

   Navigate to the directory where the compose.yml is, and run the following command:

    ```
    docker compose up
    ```
4. **Visit Mnestix AAS Generator**

    Open your Browser and go to http://localhost:5064/swagger/index.html to see Swagger documentation and all public 
 exposed endpoints.


5. **Important Environmental Settings**

**API Key**

- `CustomerEndpointsSecurity__ApiKey` - Configure the API key to secure all API endpoints.

> **Note:** The API key provided in the `compose.yml` file is just an example.
> Please replace `YOUR_API_KEY_HERE` with your actual API key immediately to ensure proper functionality and security.

> **Note:** GET and HEAD requests do not require an API key. Only modifying requests (POST, PUT, PATCH, DELETE) require the `X-API-KEY` header.

**Repository Connection**

- `ServerUrls` - Defines the URL for the AAS/Submodel repository (or Mnestix Proxy).  
  Example: `ServerUrls: 'http://mnestix-proxy:5065/repo/'`

**Separate Submodel Repositories (API v2 only)**

You can optionally configure separate repositories for blueprints and templates:

- `Configuration__SubmodelTemplatesApiUrl` - Dedicated repository URL for submodel templates.
- `Configuration__SubmodelBlueprintsApiUrl` - Dedicated repository URL for submodel blueprints.

> If not configured, the standard repository defined in `ServerUrls` is used.

**Feature Flags**

Mnestix provides multiple feature flags. Set them to `true` or `false` to define the behavior:  

| Feature Flag                                     | Default value | Description                                                                                                                                                                                         |
|--------------------------------------------------|------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------| 
| `Features__UseAuthentication`                    | false | Enable or disable authentication in the backend.                                                                                                                                                |
| `Features__RequiredShells`                       | true | Enable initialization of required AAS shells on startup.                                                                                                                                                |

**MongoDb Configuration**
- Public access for development only

>MongoDB is exposed publicly solely for development purposes. It's crucial to restrict public access in production environments.

- Security reminder

>To enhance security, it's essential to update the default admin username and password.

### Authentication

This section is applicable only if `Features__UseAuthentication` is set to `true`.

#### Default Settings - Protecting Mnestix API Endpoints

By default, Mnestix API uses Microsoft Entra ID (formerly Azure AD) as the authorization server (OAuth 2.0). To secure Mnestix API endpoints with this configuration, you can use the `[Authorize]` attribute, which ensures that only authenticated users can access specific controllers or actions in your application.

**Key Settings for Configuring Microsoft Entra ID:**

| **Parameter**           | **Description**                                         | **Example Value**                  |
|-------------------------|---------------------------------------------------------|------------------------------------|
| **AzureAd__ClientId**   | Unique ID for your application in Microsoft Entra ID.   | `"your-client-id"`                 |
| **AzureAd__Domain**     | Domain name of your Microsoft Entra ID tenant.          | `"your-domain.onmicrosoft.com"`    |
| **AzureAd__TenantId**   | Unique ID for your Microsoft Entra ID tenant.           | `"your-tenant-id"`                 |

#### OpenID Settings - Protecting Mnestix API Endpoints

Mnestix API can also be secured using an OpenID Connect (OIDC) provider, such as Keycloak. If using an OpenID Connect provider, configure the following settings:

| **Parameter**                 | **Description**                                                                                                          | **Example Value**                    | **Note**                                                                                      | **Default Value** |
|-------------------------------|--------------------------------------------------------------------------------------------------------------------------|--------------------------------------|------------------------------------------------------------------------------------------------|-------------------|
| **OpenId__EnableOpenIdAuth**  | Determines whether OpenID authentication is activated. Set this to `true` to enable authentication via the OpenID Connect provider. | -                                    | When this is set to `true`, the default Azure settings will no longer be applied.             | -                 |
| **OpenId__Issuer**            | The URL of the OpenID Connect provider's issuer. This URL is used to discover authentication endpoints and verify tokens. | `http://localhost:9096/realms/BaSyx` | -                                                                                              | -                 |
| **OpenId__ClientID**          | Unique identifier for your application as registered with the OpenID Connect provider. This ID is used during authentication. | `"mnestixApi-demo"`                  | -                                                                                              | -                 |
| **OpenId__RequireHttpsMetadata** | Determines whether the OpenID Connect provider metadata should be accessed over HTTPS. Set this to `true` to enforce HTTPS for secure communication. | -                                    | Setting this to `false` is intended only for development environments. For production, always use `true` to ensure secure communication. | `false`           |


### OpenID Settings - Configuring Client for Repository Authentication

When using Basyx with authorization and Keycloak as an OpenID Connect (OIDC) provider, Mnestix API needs to authenticate with the repository. Configure the following settings to support this repository authentication:

| **Parameter**                                  | **Description**                                                                                                          | **Example Value**                          | **Note**                                                         | **Default Value** |
|------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|--------------------------------------------|------------------------------------------------------------------|-------------------|
| **RepositoryOpenIdConnect__EnableRepositoryOpenIdAuth** | Determines whether OpenID Connect authentication is enabled for the repository. Set this to `true` to activate authentication through the OpenID Connect provider. | -                                          | -                                                                | `false`           |
| **RepositoryOpenIdConnect__Authority**         | Base URL of the OpenID Connect provider’s authority. This URL is used to obtain authentication tokens and metadata.       | `http://localhost:9096/realms/BaSyx`       | -                                                                | -                 |
| **RepositoryOpenIdConnect__DiscoveryEndpoint** | Endpoint used to discover OpenID Connect configuration details. This is appended to the `Authority` URL to access the provider’s configuration. | `.well-known/openid-configuration`         | -                                                                | -                 |
| **RepositoryOpenIdConnect__ClientId**          | Unique identifier for the client application registered with the OpenID Connect provider. This ID is used during authentication. | `"mnestix-repo-client-demo"`               | -                                                                | -                 |
| **RepositoryOpenIdConnect__ClientSecret**      | Secret key associated with the client application. This key secures client credentials during authentication. Leave empty if not required. | `"your-secret"`                            | -                                                                | -                 |
| **RepositoryOpenIdConnect__ValidateIssuer**    | Indicates whether to validate the issuer of the OpenID Connect tokens. Set this to `true` to ensure tokens are issued by the expected authority. | -                                          | For enhanced security, it is recommended to set this to `true` in production environments. | `false`           |
| **TokenEndpoint**     | Used to explicitly define the token endpoint URL that your application will use to obtain access tokens. This is crucial in a Dockerized environment where the internal network configuration might differ from the external one. |                                          |                                                                                            | -                |

### ⚠️ Important Note for `TokenEndpoint` Configuration

For production, this setting should be left empty. In a production environment, relying on environment-specific configurations can introduce security risks and maintenance challenges. The system should be configured to use the default discovery mechanisms provided by OIDC to dynamically determine the token endpoint, ensuring a more robust and secure setup.