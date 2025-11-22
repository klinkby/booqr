# Klinkby.Booqr

## Overview

[![Build and Push Docker image](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml)

This repository contains an AOT (Ahead-of-Time) enabled ASP.NET Core 10 Web API, designed to serve as a robust backend
for an application requiring efficient and secure booking management with a PostgreSQL database.

## Features

*   **[AOT Compilation](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)**: Leverages .NET 10's Native AOT with aggressive trimming for sub-second startup and minimal memory footprint.
*   **[Build-time OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)**: Compile-time generated OpenAPI spec with zero runtime overhead.
*   **[Source Generators](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview)**: Extensive use of compile-time code generation:
    *   [ServiceScan](https://github.com/Dreamescaper/ServiceScan.SourceGenerator) for automatic DI registration
    *   [LoggerMessage](https://learn.microsoft.com/dotnet/core/extensions/logger-message-generator) for high-performance logging
    *   [System.Text.Json source generation](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation) for AOT-compatible serialization
    *   [OptionsValidator](https://learn.microsoft.com/dotnet/core/extensions/options#options-validation-source-generator) for compile-time options validation
    *   [Configuration binding](https://learn.microsoft.com/dotnet/core/extensions/configuration#source-generator) for AOT-compatible configuration binding
    *   [EmbeddedResourceAccessGenerator](https://github.com/ChristianHorvat/EmbeddedResourceAccessGenerator) for type-safe embedded resource access
    *   [Dapper.AOT](https://github.com/DapperLib/DapperAOT) for AOT-compatible SQL queries
    *   Custom infrastructure repository query helper for streamlined data access
*   **[IAsyncEnumerable Streaming](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-stream)**: Unbuffered streaming for collection endpoints with true async iteration.
*   **[System.Threading.Channels](https://learn.microsoft.com/dotnet/core/extensions/channels)**: Lock-free async pipelines for email delivery and activity tracking with bounded channels.
*   **[Background Services](https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services)**: Three hosted services for email processing, activity recording, and scheduled reminder delivery.
*   **[Problem Details](https://datatracker.ietf.org/doc/html/rfc7807)**: RFC 7807 compliant error responses with trace ID correlation.
*   **Design Patterns**:
    *   [Command pattern](https://en.wikipedia.org/wiki/Command_pattern) for encapsulating operations as objects
    *   [Repository pattern](https://martinfowler.com/eaaCatalog/repository.html) with soft deletes and immutable variants
*   **Activity Tracking**: Automatic audit logging using [CallerMemberName](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.callermembernameattribute) to capture action names at compile-time
*   **[CLEF Logging](https://github.com/Serilog/serilog-formatting-compact)** via [NLog](https://nlog-project.org/): Structured logging in Compact Log Event Format (CLEF) using NLog's [structured logging](https://nlog-project.org/config/?tab=structured-logging) for efficient log processing.
*   **Container Security**: Runs rootless in tiny [Alpine Linux](https://alpinelinux.org/) images (~8MB runtime-deps) with immutable filesystem (`read_only: true`).
*   **PostgreSQL Backend**: Reliable data storage with [Npgsql](https://www.npgsql.org/) optimized for AOT.
*   **[JWT Authentication](https://jwt.io/)**: Secure API authentication with [Microsoft.AspNetCore.Authentication.JwtBearer](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer).
*   **Separation of Concerns**: Minimalist clean architecture emphasizing maintainability without ceremony.

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
`Klinkby.Booqr.Api.Tests`, `Klinkby.Booqr.Application.Tests`, and `Klinkby.Booqr.Infrastructure.Tests`.

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
