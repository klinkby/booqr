# Architecture Policies

This document describes the architectural policies for the Klinkby.Booqr solution.

## Layer Overview

The solution follows a clean architecture pattern with the following layers:

```
┌─────────────────────────────────────────────┐
│                    API                       │
│  (HTTP Presentation Layer - Minimal API)    │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────┴──────────────────────────┐
│             Application                      │
│    (Business Logic, Commands, Services)     │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────┴──────────────────────────┐
│            Infrastructure                    │
│     (I/O, Service Agents/Proxies)          │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────┴──────────────────────────┐
│                 Core                         │
│      (Records, Interfaces, Exceptions)      │
└─────────────────────────────────────────────┘
```

## Architectural Policies

### Core Layer (`Klinkby.Booqr.Core`)

- **References**: Only `System.*` assemblies are allowed.
- **Contents**: Contains only records, interfaces, exceptions, and static classes (for constants).
- **Purpose**: Defines the domain contracts and data structures.

### Application Layer (`Klinkby.Booqr.Application`)

- **Internal References**: Only references `Core` internally.
- **I/O Restrictions**: No direct I/O implementations (e.g., `System.Net.Http`, `Npgsql`, `Dapper`). Exception types from `System.Data.Common` are allowed for exception handling.
- **Contents**: Contains business logic including Commands and Services.
- **Purpose**: Implements the application's use cases and business rules.

### Infrastructure Layer (`Klinkby.Booqr.Infrastructure`)

- **Internal References**: Only references `Core` internally.
- **Business Logic Restrictions**: No business logic like Commands.
- **Contents**: Only I/O service agents and proxies.
- **Purpose**: Implements technical concerns like database access and external service integration.

### API Layer (`Klinkby.Booqr.Api`)

- **Dependency Restrictions**: No specific dependency restrictions.
- **Business Logic Restrictions**: No actual business logic.
- **Contents**: Minimal API HTTP presentation layer only.
- **Purpose**: Exposes the application via HTTP endpoints.

## Enforcement

These architectural policies are enforced through automated tests using `NetArchTest.Rules` in the `Klinkby.Booqr.Tests` project.
