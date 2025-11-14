# Klinkby.Booqr

## Overview

[![Build and Push Docker image](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml)

This repository contains an AOT (Ahead-of-Time) enabled ASP.NET Core 10 Web API, designed to serve as a robust backend
for an application requiring efficient and secure booking management with a PostgreSQL database.

## Features

*   **AOT Compilation**: Leverages .NET 10's AOT compilation for improved startup performance and reduced memory
footprint.
*   **PostgreSQL Backend**: Utilizes PostgreSQL for reliable and scalable data storage.
*   **JWT Authentication**: Implements JSON Web Token (JWT) for secure API authentication and authorization.
*   **Compile-time Generated OpenAPI Specification**: Features a build-time generated OpenAPI specification, ensuring
API documentation is always up-to-date and accurate without runtime overhead.
*   **Separation of Concerns**: The codebase is structured with as a "minimalist clean-architecture" with emphasis on
maintainability and testability without the ceremony.

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
