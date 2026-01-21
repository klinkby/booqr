# Klinkby.Booqr

[![Build+Push Docker image](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml)
[![codecov](https://codecov.io/github/klinkby/booqr/graph/badge.svg?token=GNQ7UPJ35G)](https://codecov.io/github/klinkby/booqr)
[![CodeQL](https://github.com/klinkby/booqr/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/klinkby/booqr/actions/workflows/github-code-scanning/codeql)
[![License](https://img.shields.io/github/license/klinkby/booqr.svg)](LICENSE)

An AOT (Ahead-of-Time) enabled ASP.NET 10 Web API, designed to serve as a robust backend
for an application requiring efficient and secure booking management with a PostgreSQL database. The service is designed
on a minimalist clean architecture emphasizing performance and maintainability without ceremony.


## Features

*   **[AOT Compilation](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)**: Leverages .NET 10's Native AOT with aggressive trimming.
*   **[Build-time OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)**: Compile-time generated OpenAPI spec with zero runtime overhead.
*   **[Source Generators](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)**: Extensive use of compile-time code generation:
    *   [ServiceScan](https://github.com/Dreamescaper/ServiceScan.SourceGenerator) for automatic DI registration
    *   [LoggerMessage](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator) for high-performance logging
    *   [Json serializer](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation) for serialization
    *   [OptionsValidator](https://learn.microsoft.com/dotnet/core/extensions/options#options-validation-source-generator) for compile-time options validation
    *   [Dapper.AOT](https://github.com/DapperLib/DapperAOT) for ORM mapping/SQL queries
    *   [Custom infrastructure](https://github.com/klinkby/booqr-generators) query builder helper
*   **[IAsyncEnumerable Streaming](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)**: Unbuffered streaming for collection endpoints with async iteration
*   **[Channels](https://learn.microsoft.com/dotnet/core/extensions/channels)**: Async pipelines for immediate response with deferred processing.
*   **[Background Services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)**: Hosted services for email processing, activity recording, and CRON-scheduled reminder delivery.
*   **Activity Tracking**: Efficient audit logging aspect.
*   **[Problem Details](https://datatracker.ietf.org/doc/html/rfc7807)**: Structured error responses using RFC 7807 (ProblemDetails) standard with detailed validation errors.
*   **[CLEF structured logging](https://github.com/Serilog/serilog-formatting-compact)** via [NLog](https://nlog-project.org/): Compact Log Event Format for efficient json log sink.
*   **Container Security**: Runs rootless in tiny [Alpine Linux](https://alpinelinux.org/) [images (~17MB image)](https://hub.docker.com/r/klinkby/booqr/tags) with immutable filesystem.
*   **Password handling**: Email-verified user accounts with [BCrypt](https://en.wikipedia.org/wiki/Bcrypt) password hashing and timing attack mitigation.
*   **PostgreSQL Backend**: Reliable data storage in [Npgsql 18](https://www.npgsql.org/) container with schema bootstrapping.
*   **[JWT Authentication](https://jwt.io/)**: Secure API authentication with [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer).
*   **Refresh Token Rotation**: Opaque refresh tokens with 240-bit cryptographic entropy, family-based tracking for reuse detection, and automatic revocation on compromise.
*   **HttpOnly Cookies**: Secure refresh token storage with `HttpOnly`, `Secure`, `SameSite=Strict`, and path-scoped attributes to prevent XSS and CSRF attacks.
*   **Token Management**: SHAKE128 hashing for database storage, transactional token rotation, and automated daily cleanup of expired tokens.
*   **Role-based Authorization**: Fine-grained access control using ASP.NET Core's built-in authorization policies.
*   **Docker Compose**: Wraps the service with HAProxy gateway in the front, PostgreSQL in the back, and efficient UNIX
sockets for inter-container communication.


## Project Structure

The **Klinkby.Booqr** solution is organized into several projects, each responsible for a distinct aspect of the
application:

*   [Core](src/Klinkby.Booqr.Core): Defines core domain models, interfaces, and common utilities used across all layers.
*   [Application](src/Klinkby.Booqr.Application): Contains the application's business logic and orchestrates operations between the API and
infrastructure layers.
*   [Infrastructure](src/Klinkby.Booqr.Infrastructure): Manages data access (e.g., interaction with PostgreSQL) and external service integrations.
Includes `Klinkby.Booqr.Infrastructure.Generators` for code generation.
*   [Api](src/Klinkby.Booqr.Api): The entry point of the application, containing middleware configuration and all things HTTP.
*   [Tests](tests): Contains unit and integration tests for various components of the solution, including
`Klinkby.Booqr.Api.Tests`, `Klinkby.Booqr.Application.Tests`, and `Klinkby.Booqr.Infrastructure.Tests` using
test containers to spin up a live PostgreSQL instance.


## Minimal Runtime Dependencies

- [dotnet 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Npgsql](https://www.npgsql.org/) for PostgreSQL access
- [Dapper + AOT](https://aot.dapperlib.dev/) for high-performance data access
- [BCrypt.Net](https://github.com/BcryptNet/bcrypt.net) for secure password hashing
- [NLog](https://nlog-project.org/) for structured logging


## Licensed under AGPL-3.0

Copyright (C) 2025 Mads Klinkby

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a [copy of the GNU Affero General Public License
along with this program](LICENSE).  If not, see <http://www.gnu.org/licenses/>.
