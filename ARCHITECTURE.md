# Architecture Policies

This document describes the architectural policies for the Klinkby.Booqr solution.

## Layer Overview

The solution follows a minimalist clean architecture pattern with the following layers:

### Core Layer ([Klinkby.Booqr.Core](src/Klinkby.Booqr.Core))

- **References**: Only `System.*` assemblies are allowed (no third‑party libraries). Types may of course reference other `Core` types.
- **Immutability**: All Core classes/types are immutable.
- **Contents**: Contains only records, interfaces, exceptions, and static classes (for constants).
- **Purpose**: Defines the domain contracts and data structures.

### Application Layer ([Klinkby.Booqr.Application](src/Klinkby.Booqr.Application))

- **Internal References**: Only references `Core` internally and must not reference `Infrastructure`.
- **I/O Restrictions**: No direct I/O or data‑access dependencies. In particular, must not depend on types in namespaces matching: `Dapper`, `System.Console`, `System.IO`, `System.Net`, `System.Data`, `Npgsql`.
- **Immutability**: Classes whose names end with `Request` are immutable.
- **Contents**: Contains business logic including Commands and Services.
- **Purpose**: Implements the application's use cases and business rules.

### Infrastructure Layer ([Klinkby.Booqr.Infrastructure](src/Klinkby.Booqr.Infrastructure))

- **Internal References**: Only references `Core` internally and must not reference `Application`.
- **Repositories**: Any class implementing `IRepository` must live in the Infrastructure assembly and must be `sealed`.
- **Business Logic Restrictions**: No business logic like Commands.
- **Contents**: Only I/O service agents, repositories, and proxies.
- **Purpose**: Implements technical concerns like database access and external service integration.

### API Layer ([Klinkby.Booqr.Api](src/Klinkby.Booqr.Api))

- **Dependency Restrictions**: No specific dependency restrictions.
- **Business Logic Restrictions**: No actual business logic.
- **Contents**: Minimal API HTTP presentation layer only.
- **Purpose**: Exposes the application via HTTP endpoints.

## Enforcement

These architectural policies are enforced through automated tests using `TngTech.ArchUnitNET` in the
[Klinkby.Booqr.Tests](tests/Klinkby.Booqr.Tests) project.
