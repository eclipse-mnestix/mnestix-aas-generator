# AAS Generator (prior Mnestix Backend) - Developer Documentation

This directory contains developer documentation for the AAS Generator, a comprehensive .NET application that provides REST APIs and services for managing Asset Administration Shells (AAS) within the Eclipse Mnestix ecosystem.

## Scope

This project covers the backend services for AAS management, including automated Submodel generation from structured data using a rules engine, template management, repository integration, authentication, and configuration management. The proxy which handles requests to Basyx is in another repository. The AAS Generator folder inside this repository handles the generation of a submodel from structured data using a rules engine. Don't confuse the AAS Generator component with the entire AAS Generator Repository.

## Repository Overview

The AAS Generator is a multi-component system that handles:

- **AAS Management**: Create, read, update, delete operations for AAS instances
- **Template Management**: Blueprint-based AAS and Submodel templates 
- **AAS Generator (Rules-Engine)**: Automated generation of AAS Submodels from structured data
- **Authentication & Authorization**: API key and OAuth-based security
- **Repository Integration**: Proxy services for Eclipse BaSyx repositories
- **ID Generation**: Standardized identifier creation for AAS components
- **Configuration Management**: Dynamic configuration of system behavior

### Core Components

- **MnestixApi**: REST API controllers, authentication, middleware
- **MnestixCore**: Business logic, services, and core functionality
  - `AasCreator`: Create complete AAS instances 
  - `AASGenerator`: Rules-based Submodel generation (detailed in `rules-engine.md`)
  - `TemplateBuilder`: Template and blueprint management
  - `IdGenerator`: AAS/Submodel ID generation services
  - `RepoProxyClient`: Eclipse BaSyx repository integration
  - `ConfigurationService`: Runtime configuration management
- **Testing**: Comprehensive unit and integration test suites


## Documentation Structure

- [Architecture Overview](architecture.md) - High-level system architecture and design patterns
- [Rules Engine](rules-engine.md) - **AAS Generator**: Rule types, pipeline processing, and automated Submodel generation
- [Integration Guide](integration.md) - Component integration within the Mnestix ecosystem

## Quick Start

### Development Setup
1. **Prerequisites**: .NET 8, Docker (for BaSyx), MongoDB
2. **Local Run**: `dotnet run --watch` or use Rider configuration
3. **Docker**: `docker compose -f ./docker-compose/docker-compose.dev.yml up`

### Key Endpoints
- `/api/v1/DataIngest` - Generate Submodels from templates and data
- `/api/v1/templates` - Manage AAS/Submodel templates
- `/api/v1/aas` - AAS CRUD operations
- `/swagger` - Interactive API documentation

### Configuration
Key settings in `appsettings.json`:
- `Features__UseAuthentication` - Enable/disable auth
- `Features__AllowRetrievingAllShellsAndSubmodels` - Bulk operations
- `ReverseProxy__Clusters` - BaSyx repository addresses
- `ApiKey` - Valid API keys for authentication <!-- TODO: Replace internal Confluence link with public documentation -->

## Architecture

### Service Layer Structure
- **Controllers** (`MnestixApi/Controllers/`): REST API endpoints
- **Services** (`MnestixCore/*/`): Business logic and domain services
- **Repository Integration**: Proxy pattern for Eclipse BaSyx
- **Authentication**: API key and OAuth support
- **Dependency Injection**: .NET built-in DI container
