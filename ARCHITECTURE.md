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

## Multi-Tenancy Routing and DNS

Booqr supports multi-tenancy using a **shared PostgreSQL schema** with **Row-Level Security (RLS)** enforced at the database layer, keyed on per-tenant login roles. HTTP routing uses DNS subdomains to identify tenants.

### DNS and TLS

- **Wildcard DNS**: `*.booqr.dk` resolves all tenant subdomains (e.g., `alice.booqr.dk`, `bob.booqr.dk`) plus the www subdomain.
- **TLS Certificate**: Must cover the wildcard `*.booqr.dk`.
- **HAProxy gateway**: Preserves the `Host` header (untouched); the API resolves the tenant from `Request.Host`.

### Routing and Host Authority

1. **Apex redirect** (`booqr.dk`): Redirected to `www.booqr.dk` (301 permanent redirect).
2. **Tenant subdomains** (`<tenant-slug>.booqr.dk`): Routed to the app backend; the API resolves the tenant slug via registry lookup and enforces row-level access.
3. **Reserved hosts** (`www.booqr.dk`, naked `booqr.dk` before redirect, and a hypothetical `api.booqr.dk`): Resolve to no tenant (marketing/admin contexts).
4. **Unknown hosts**: Denied by HAProxy (`default_backend deny`).

### API Host Validation

The API validates incoming `Host` headers against `AllowedHosts` (default: `*.booqr.dk;www.booqr.dk;booqr.dk`). Client-supplied `X-Forwarded-Host` is ignored; the API trusts the `Host` header as set by HAProxy. See `redist/web-gateway/haproxy.cfg` and `src/Klinkby.Booqr.Api/Program.cs` for configuration.

For complete details on tenant isolation, provisioning, and data access, see [docs/1-design.md](docs/1-design.md), especially sections **1. Holistic design**, **2a. API (runtime) changes**, and **4. HAProxy + domain**.

## Enforcement

These architectural policies are enforced through automated tests using `TngTech.ArchUnitNET` in the
[Klinkby.Booqr.Tests](tests/Klinkby.Booqr.Tests) project.
