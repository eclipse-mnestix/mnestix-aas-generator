<!--
  Sync Impact Report
  ==================================================
  Version change: N/A → 1.0.0 (initial ratification)
  Modified principles: N/A (initial)
  Added sections:
    - Core Principles (7 principles)
    - Technology & Compliance Constraints
    - Development Workflow & Quality Gates
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md — ✅ compatible (Constitution Check section present)
    - .specify/templates/spec-template.md — ✅ compatible (requirements/scenarios structure aligns)
    - .specify/templates/tasks-template.md — ✅ compatible (test tasks align with testing principle)
  Follow-up TODOs: None
  ==================================================
-->

# Mnestix AAS Generator Constitution

## Core Principles

### I. AAS Specification Conformance (NON-NEGOTIABLE)

All generated AAS and Submodel instances MUST conform to the
IDTA Asset Administration Shell Metamodel v3.0 specification.

- Generated output MUST pass AAS Metamodel v3.0 validation.
- Template qualifiers, Submodel element types, and ID formats
  MUST follow the IDTA specification exactly.
- When the IDTA publishes a new specification version, the team
  MUST evaluate and plan migration before adopting it.
- No proprietary extensions to the AAS schema are permitted
  unless wrapped behind a clearly documented, optional feature
  flag.

**Rationale**: The generator's core value proposition is producing
standards-compliant digital twins. Deviation from the spec
invalidates downstream interoperability with any AAS-compatible
tooling.

### II. Deterministic Generation

Given identical inputs (template, blueprint, data, language),
the AAS Generator MUST produce byte-identical output every time.

- Pipeline steps MUST be free of non-deterministic behavior
  (random IDs, timestamps, unordered collections).
- Submodel IDs MUST be derived deterministically from inputs or
  from the explicitly provided `NewSubmodelId` parameter.
- Any new pipeline step MUST document and test its
  determinism guarantees.

**Rationale**: Determinism enables reliable bulk generation,
diffing, regression testing, and reproducible deployments across
environments.

### III. Backwards Compatibility & API Versioning

Public API changes MUST NOT break existing consumers. When a
breaking change is unavoidable, it MUST be introduced behind a
new API version.

- URL-based versioning (`/api/vN/`) is the only supported
  versioning strategy.
- Deprecated API versions MUST remain functional for at least
  one major release cycle and MUST emit deprecation warnings.
- Blueprint qualifier schemas MUST remain backwards-compatible;
  new qualifier types are additive only.
- Database schema migrations MUST be non-destructive
  (additive columns/collections, no drops without migration).

**Rationale**: Downstream integrators (Mnestix Browser, BaSyx
deployments, third-party systems) depend on stable contracts.
Breaking changes silently would erode trust in the ecosystem.

### IV. Unit Testing for Generator Changes (NON-NEGOTIABLE)

Every new file or modified behaviour in the AAS Generator
(`MnestixCore/AASGenerator/`) MUST be accompanied by
corresponding unit tests in `Core.Tests/AasGenerator/`.

- New pipeline steps MUST have dedicated test classes covering
  happy path, edge cases, and error conditions.
- New rule types MUST include tests with representative
  blueprint + data fixtures.
- Bug fixes MUST include a regression test reproducing the
  original defect before applying the fix.
- Tests MUST be runnable via `dotnet test` without external
  service dependencies (no live BaSyx, no MongoDB).

**Rationale**: The rules engine is the most complex subsystem.
Untested changes in mapping logic can silently produce
non-compliant output at scale, which is far harder to detect
than a crash.

### V. Open Source & Community First

This project is developed under the Eclipse Foundation umbrella
and licensed under MIT. All decisions MUST respect open-source
best practices.

- No proprietary dependencies are permitted in production code.
- All features MUST be usable without commercial accounts or
  paid services.
- Documentation for public APIs, blueprints, and configuration
  MUST be kept up-to-date in the `docs/` directory.
- Security vulnerabilities MUST be disclosed responsibly
  following Eclipse Foundation guidelines.

**Rationale**: As an Eclipse project, community trust depends on
transparency, accessibility, and an unencumbered dependency
chain.

### VI. Pipeline Extensibility & Simplicity

The Pipes-and-Filters architecture of the AAS Generator MUST
remain the single mechanism for data transformation.

- New transformation logic MUST be implemented as an
  `IPipelineStep<SubmodelMappingContext>` and registered in
  `PipelineBuilder`.
- Pipeline steps MUST be stateless; all mutable state lives in
  the `SubmodelMappingContext`.
- Avoid unnecessary abstractions: do not introduce new
  architectural layers (repositories, mediators, event buses)
  unless a concrete, documented need exists.
- Follow YAGNI—implement only what is requested or clearly
  required by the current task.

**Rationale**: A predictable, linear pipeline is easier to
reason about, debug, and extend than a graph of loosely coupled
event handlers. Simplicity reduces onboarding time for new
contributors.

### VII. Security by Default

The API MUST follow secure defaults and address OWASP Top 10
risks.

- Authentication MUST be configurable but disabled only via
  explicit opt-out (`Features__UseAuthentication = false`).
- Input data from `DataIngest` and `AasCreator` endpoints MUST
  be validated and sanitized before entering the pipeline.
- Secrets (API keys, client secrets) MUST NOT appear in source
  code, logs, or generated AAS output.
- Docker images MUST run as non-root and pin base image
  versions.

**Rationale**: The generator often runs in industrial
environments with sensitive asset data. Insecure defaults
expose the entire AAS ecosystem to risk.

## Technology & Compliance Constraints

- **Runtime**: .NET 8 (LTS), C# with nullable reference types
  enabled.
- **AAS Standard**: IDTA AAS Metamodel v3.0.
- **Expression Engine**: Jsonata.Net.Native for blueprint rule
  evaluation.
- **Serialization**: Newtonsoft.Json (required by AAS libraries
  and BaSyx integration).
- **Repository Integration**: Eclipse BaSyx v2 REST API.
- **Containerization**: Docker with Linux target OS.
- **License**: MIT — all production dependencies MUST have
  MIT/Apache-2.0/BSD-compatible licenses.
- **CI**: All tests (`dotnet test`) MUST pass before merge.

## Development Workflow & Quality Gates

### Branch Model

- `main` — stable, release-ready. Merges require **2 approved
  reviews** and green CI.
- `dev` — integration branch. Merges require **1 approved
  review** and green CI.
- Feature branches branch from `dev` and merge back to `dev`.

### Pull Request Requirements

1. CI pipeline MUST pass (`dotnet build` + `dotnet test`).
2. New AAS Generator code MUST include unit tests (Principle IV).
3. API changes MUST update `docs/api.md`.
4. Breaking changes MUST use a new API version (Principle III).
5. PR description MUST reference the related issue or feature
   specification.

### Code Quality

- No compiler warnings in merged code.
- Nullable reference types (`<Nullable>enable</Nullable>`) MUST
  remain enabled.
- Follow existing code conventions (naming, folder structure)
  as established in the codebase.

## Governance

This constitution is the authoritative source for project
principles and development standards. It supersedes conflicting
guidance in other documents.

### Amendment Process

1. Propose amendment via a pull request modifying this file.
2. Amendment requires the same review threshold as `main`
   merges (2 approved reviews).
3. Each amendment MUST update the version and
   `Last Amended` date below.
4. Amendments MUST include a Sync Impact Report (HTML comment
   at top of this file) documenting propagation to dependent
   templates.

### Versioning Policy

- **MAJOR**: Principle removed, redefined, or made incompatible
  with prior guidance.
- **MINOR**: New principle added or existing principle
  materially expanded.
- **PATCH**: Wording clarifications, typo fixes,
  non-semantic refinements.

### Compliance

- All PRs and code reviews MUST verify adherence to these
  principles.
- Deviations MUST be justified in the PR description and
  approved by at least two reviewers.

**Version**: 1.0.0 | **Ratified**: 2025-04-09 | **Last Amended**: 2025-04-09
