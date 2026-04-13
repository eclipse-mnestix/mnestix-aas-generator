<p align="center">
 <img src="https://raw.githubusercontent.com/eclipse-mnestix/mnestix-proxy/main/assets/android-chrome-192x192.png" alt="Mnestix Logo">
</p>
<h1 style="text-align: center">Mnestix AAS Generator</h1>

[![Made by XITASO](https://img.shields.io/badge/Made_by_XITASO-005962?style=flat-square)](https://xitaso.com/)
[![MIT License](https://img.shields.io/badge/License-MIT-005962.svg?style=flat-square)](https://choosealicense.com/licenses/mit/)

### Welcome to the Mnestix AAS Generator Wiki!

**Mnestix AAS Generator** is an open-source .NET application that provides REST APIs and services for managing Asset Administration Shells (AAS) within the Eclipse Mnestix ecosystem. It enables automated Submodel generation from structured data using a rules engine, template management, repository integration, and more.

> **Note:** The proxy which handles requests to BaSyx is in a [separate repository](https://github.com/eclipse-mnestix/mnestix-proxy). The AAS Generator component within this repository handles the generation of Submodels from structured data using a rules engine.

## Features

- **AAS Management**: Create, read, update, delete operations for AAS instances
- **Template Management**: Blueprint-based AAS and Submodel templates
- **AAS Generator (Rules Engine)**: Automated generation of AAS Submodels from structured data
- **Authentication & Authorization**: API key and OAuth-based security
- **Repository Integration**: Proxy services for Eclipse BaSyx repositories
- **ID Generation**: Standardized identifier creation for AAS components
- **Configuration Management**: Dynamic configuration of system behavior

## Core Components

- **MnestixApi**: REST API controllers, authentication, middleware
- **MnestixCore**: Business logic, services, and core functionality
  - `AasCreator`: Create complete AAS instances
  - `AASGenerator`: Rules-based Submodel generation
  - `TemplateBuilder`: Template and blueprint management
  - `IdGenerator`: AAS/Submodel ID generation services
  - `RepoProxyClient`: Eclipse BaSyx repository integration
  - `ConfigurationService`: Runtime configuration management
- **Testing**: Comprehensive unit and integration test suites

## Quick Start

### Development Setup
1. **Prerequisites**: .NET 8, Docker (for BaSyx), MongoDB
2. **Local Run**: `dotnet run --watch` or use Rider configuration
3. **Docker**: `docker compose -f ./docker-compose/compose.dev.yml up`

### Key Endpoints
- `/api/v2/DataIngest` - Generate Submodels from templates and data
- `/api/v2/Blueprints` - Manage Submodel blueprints
- `/api/v2/AasCreator` - AAS CRUD operations
- `/swagger` - Interactive API documentation

### Configuration
Key settings in `appsettings.json`:
- `Features__UseAuthentication` - Enable/disable auth
- `Features__AllowRetrievingAllShellsAndSubmodels` - Bulk operations
- `ReverseProxy__Clusters` - BaSyx repository addresses

## Documentation

- [API Documentation](API-Documentation) - REST API endpoints reference
- [Blueprint and Rules](Blueprint-and-Rules) - How to create blueprints and mapping rules for automated Submodel generation
- [Rules Engine Architecture](Rules-Engine-Architecture) - Pipeline processing internals and rule system design
