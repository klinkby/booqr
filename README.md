# Klinkby.Booqr

[![Build and Push Docker image](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml/badge.svg?branch=main)](https://github.com/klinkby/booqr/actions/workflows/docker-publish.yml)

This repository contains an AOT (Ahead-of-Time) enabled ASP.NET Core 10 Web API, designed to serve as a robust backend
for applications requiring efficient and secure data management with a PostgreSQL database.

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
