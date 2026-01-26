# Architecture Tests (`Klinkby.Booqr.Tests`)

## Purpose

This project contains automated architectural policy tests that enforce the clean architecture boundaries and design rules across the entire solution. It uses ArchUnit to validate dependencies, naming conventions, immutability requirements, and other architectural constraints.

## Technology

- **TngTech.ArchUnitNET**: Fluent API for architectural testing
- All standard testing practices from [tests/AGENTS.md](../AGENTS.md) apply

## What is Tested

### Layer Dependency Rules

#### Core Layer
- ✅ Only references `System.*` assemblies (no third-party libraries)
- ✅ Types may only reference other Core types
- ❌ Must not reference Application, Infrastructure, or API

#### Application Layer
- ✅ Only references Core internally
- ❌ Must NOT reference Infrastructure
- ❌ Must not depend on forbidden I/O namespaces:
  - `Dapper`
  - `System.Console`
  - `System.IO`
  - `System.Net`
  - `System.Data`
  - `Npgsql`

#### Infrastructure Layer
- ✅ Only references Core internally
- ❌ Must NOT reference Application
- ✅ Any class implementing `IRepository` must live in Infrastructure assembly
- ✅ All repository implementations must be `sealed`

#### API Layer
- ✅ No specific dependency restrictions
- ❌ No actual business logic

### Immutability Rules

- **Core**: All Core classes/types are immutable
- **Application**: Classes whose names end with `Request` are immutable

### Naming Conventions

Tests validate consistent naming patterns across the solution (specific patterns depend on implementation).

## Test Structure

Example architectural test:
```csharp
[Fact]
public void CoreLayer_ShouldNotReferenceForbiddenNamespaces()
{
    var architecture = new ArchLoader()
        .LoadAssemblies(typeof(CoreAssemblyMarker).Assembly)
        .Build();

    var rule = ArchRuleDefinition.Types()
        .That().ResideInAssembly(typeof(CoreAssemblyMarker).Assembly)
        .Should().NotDependOnAnyTypesThat()
        .ResideInNamespace("Dapper", useRegex: true)
        .OrShould().NotDependOnAnyTypesThat()
        .ResideInNamespace("Npgsql", useRegex: true);

    rule.Check(architecture);
}
```

## Benefits

1. **Continuous validation**: Architecture rules are tested on every build
2. **Early detection**: Violations caught immediately during development
3. **Documentation**: Tests serve as executable documentation of architectural policies
4. **Refactoring safety**: Ensures architectural boundaries remain intact during changes
5. **Team alignment**: Enforces consistent understanding of architectural principles

Note: The assembly is tagged with `[Trait("Category", "Architecture")]` at assembly level.

## When Tests Fail

If an architectural test fails:
1. **Review the violation**: Understand which rule was broken
2. **Check if it's intentional**: Determine if the architecture needs updating or code needs fixing
3. **Fix the code**: Usually, the code should be refactored to comply with the architecture
4. **Update the test**: Only if the architectural policy itself needs to change

## Test Categories

The project validates:
- **Layer dependencies**: Who can reference whom
- **Repository placement**: All IRepository implementations in Infrastructure
- **Repository sealing**: All repositories are sealed classes
- **Immutability**: Core and Request types are immutable
- **Namespace restrictions**: Application layer I/O restrictions

## Dependencies

From `.csproj`:
- **TngTech.ArchUnitNET.xUnitV3**: ArchUnit for .NET with xUnit v3 integration
- **Project references**: All four layers (Core, Application, Infrastructure, API)

## Best Practices

1. **Keep tests focused**: One architectural rule per test method
2. **Clear naming**: Test names should describe the rule being enforced
3. **Helpful messages**: Use descriptive failure messages
4. **Regular review**: Periodically review tests to ensure they match current architecture
5. **Fast execution**: Architecture tests should run quickly (no I/O, no integration)

## Related Documentation

- **[tests/AGENTS.md](../AGENTS.md)** - General testing guidelines
- **ARCHITECTURE.md** (root) - Full architectural policies that these tests enforce
- **src/*/AGENTS.md** - Layer-specific guidelines
