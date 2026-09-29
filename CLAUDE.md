# CRM-System Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-09-29

## Active Technologies

- Backend: C# 12 / .NET 8 (LTS), ASP.NET Core Web API, EF Core 8 (SQL Server 2022, Full-Text Search),
  ASP.NET Core Identity + JWT, FluentValidation, Serilog, Hangfire, SignalR, MailKit,
  Anthropic C# SDK (`Anthropic`) (001-support-crm-mvp)
- Frontend: Angular (current stable, standalone components, strict TypeScript), Angular Material,
  Transloco (ar/en, RTL), `@microsoft/signalr`, generated API client from OpenAPI (001-support-crm-mvp)
- Testing: xUnit, NSubstitute, Shouldly, Testcontainers (SQL Server), WireMock.Net; Angular unit
  tests; Playwright E2E (001-support-crm-mvp)

## Project Structure

```text
backend/
  src/Crm.Domain  Crm.Application  Crm.Infrastructure  Crm.Api
  tests/Crm.*.UnitTests  Crm.Infrastructure.Tests  Crm.Api.IntegrationTests
frontend/
  projects/staff  projects/portal  projects/chat-widget  libs/shared
e2e/  deploy/  specs/
```

## Commands

```powershell
docker compose -f deploy/docker-compose.dev.yml up -d
dotnet build backend/Crm.sln
dotnet test backend/Crm.sln
dotnet run --project backend/src/Crm.Api
cd frontend; npm run generate:api; npx ng serve staff; npx ng test --watch=false; npx ng lint
```

## Code Style

- Follow `.specify/memory/constitution.md` (Clean Architecture, no logic in controllers, DTOs,
  async + CancellationToken, structured logging, tests required).
- API contract source of truth: `specs/001-support-crm-mvp/contracts/openapi.yaml`.

## Recent Changes

- 001-support-crm-mvp: Added .NET 8 / EF Core / SQL Server backend and Angular frontend stack

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
