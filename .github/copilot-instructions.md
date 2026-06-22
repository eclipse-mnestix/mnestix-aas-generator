# mnestix-aas-generator Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-23

## Active Technologies
- C# / .NET 8 (LTS), nullable reference types enabled + Newtonsoft.Json (AAS serialization), Jsonata.Net.Native (expression evaluation), BaSyx v2 REST API (repo integration) 
- N/A (no storage changes)

- C# / .NET 8 (LTS), nullable reference types enabled + Jsonata.Net.Native (expression evaluation), Newtonsoft.Json (AAS serialization), BaSyx v2 REST API (repo integration)

## Project Structure

```text
MnestixCore/
Core.Tests/
```

## Commands

- Restore dependencies: `dotnet restore`
- Build all projects: `dotnet build`
- Run tests: `dotnet test`
- Verify formatting (if `dotnet format` is available): `dotnet format --verify-no-changes`

## Code Style

- Use standard C#/.NET 8 conventions with nullable reference types enabled.
- Prefer clear PascalCase names for types and methods, and camelCase for locals and parameters.
- Keep production code in `MnestixCore/` and tests in `Core.Tests/`.
- Preserve existing JSON serialization and AAS integration patterns when modifying related code.
