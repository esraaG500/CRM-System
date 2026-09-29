<!--
Sync Impact Report
- Version change: (template) → 1.0.0
- Modified principles: all template placeholders replaced (initial ratification)
- Added principles:
  I. Clean Architecture & Layer Boundaries
  II. SOLID & Dependency Injection
  III. RESTful, Explicit API Contracts
  IV. Asynchronous by Default
  V. Validated Input & Secure by Design
  VI. Observability via Structured Logging
  VII. Automated Testing (NON-NEGOTIABLE)
  VIII. Modern, Strict Angular Frontend
- Added sections: Technology Stack & Constraints; Development Workflow & Quality Gates
- Removed sections: none
- Templates:
  ✅ .specify/templates/tasks-template.md (tests changed from OPTIONAL to REQUIRED)
  ✅ .specify/templates/plan-template.md (Constitution Check derives gates from this file; no edit needed)
  ✅ .specify/templates/spec-template.md (no conflicting constraints; no edit needed)
  ✅ .claude/skills/speckit-*/SKILL.md (no outdated references found)
- Follow-up TODOs: none
-->

# CRM System Constitution

## Core Principles

### I. Clean Architecture & Layer Boundaries

The backend MUST follow Clean Architecture with four layers: **Domain**, **Application**,
**Infrastructure**, and **API** (presentation).

- Dependencies MUST point inward: API → Application → Domain; Infrastructure → Application/Domain.
  Domain MUST NOT reference any other layer or any framework (including EF Core).
- Business logic MUST live in the Domain and Application layers. Controllers MUST NOT contain
  business logic; they only accept requests, delegate to Application services/handlers, and map
  results to HTTP responses.
- All database access (EF Core `DbContext`, repositories, migrations, query implementations)
  MUST live in the Infrastructure layer. Application defines the abstractions it needs.
- DTOs MUST be used between the API and Application layers. Domain entities MUST NOT be
  exposed in API requests or responses.

**Rationale**: Clear boundaries keep business rules testable and independent of frameworks,
the database, and the transport layer.

### II. SOLID & Dependency Injection

- Code MUST apply the SOLID principles; classes have a single responsibility and depend on
  abstractions, not concrete implementations.
- All services MUST be composed through the built-in .NET dependency injection container
  (backend) and Angular's DI (`inject()` / providers) on the frontend. Manual `new`-ing of
  services with dependencies, service locators, and static mutable state are prohibited.
- Service lifetimes (singleton/scoped/transient) MUST be chosen deliberately; `DbContext`
  MUST be scoped.

**Rationale**: Loose coupling enables substitution in tests and safe evolution of components.

### III. RESTful, Explicit API Contracts

- APIs MUST be RESTful: resource-oriented URLs, correct HTTP verbs, correct status codes,
  and consistent error responses using RFC 7807 Problem Details.
- API contracts MUST be explicitly defined and published via OpenAPI (Swagger) and kept in
  `specs/<feature>/contracts/` for each feature. Request/response DTOs are part of the contract.
- APIs MUST be versioned (e.g., `/api/v1/...`); breaking changes require a new version.
- The Angular client MUST use typed models that match the published contract.

**Rationale**: Explicit contracts let frontend and backend evolve independently and safely.

### IV. Asynchronous by Default

- All I/O-bound operations (database, HTTP, file, messaging) MUST use `async`/`await`
  end to end, accepting and propagating `CancellationToken`.
- Blocking on async code (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`) and `async void`
  (except event handlers) are prohibited.

**Rationale**: Async I/O keeps the API responsive and scalable under load.

### V. Validated Input & Secure by Design

- All external input (HTTP bodies, query strings, route values, headers, files, third-party
  responses) MUST be validated at the boundary (e.g., FluentValidation in the Application
  layer) before reaching business logic.
- Authentication MUST be enforced on every endpoint by default; anonymous access MUST be an
  explicit, reviewed exception. Authorization MUST be role/policy based and checked server-side.
- Sensitive data (credentials, tokens, personal/customer data) MUST be encrypted in transit
  (HTTPS only), MUST NOT be logged, and secrets MUST NOT be committed to source control
  (use user-secrets, environment variables, or a secret vault).
- EF Core parameterized queries MUST be used; raw SQL concatenation is prohibited.
  The frontend MUST NOT bypass Angular sanitization.

**Rationale**: A CRM holds customer data; security failures are the highest-impact defects.

### VI. Observability via Structured Logging

- Logging MUST be structured (e.g., Serilog with message templates and named properties) via
  `ILogger<T>`; string-interpolated log messages are prohibited.
- Every request MUST carry a correlation ID included in logs and error responses.
- Unhandled exceptions MUST be caught by global exception-handling middleware, logged, and
  returned as Problem Details without leaking internal details.

**Rationale**: Structured, correlated logs make production issues diagnosable.

### VII. Automated Testing (NON-NEGOTIABLE)

- Every feature MUST include automated unit tests for Domain/Application logic and
  integration tests for API endpoints and Infrastructure (database) behavior.
- Integration tests MUST run against real SQL Server (e.g., Testcontainers) or an
  equivalent isolated instance, not the EF Core in-memory provider.
- Angular components and services MUST have unit tests.
- All tests MUST pass in CI before merge.

**Rationale**: Automated tests are the only reliable guarantee that code stays production-ready.

### VIII. Modern, Strict Angular Frontend

- The frontend MUST use Angular (current stable version) with **standalone components**;
  NgModules MUST NOT be introduced for features.
- TypeScript MUST run in `strict` mode, with Angular `strictTemplates` enabled; `any` is
  prohibited unless justified in review.
- Current Angular best practices MUST be followed: signals for state, `inject()` for DI,
  built-in control flow (`@if`, `@for`), `OnPush` change detection, lazy-loaded routes, typed
  reactive forms, and functional guards/interceptors.

**Rationale**: Current Angular practices give better performance, type safety, and maintainability.

## Technology Stack & Constraints

- **Backend**: .NET 8 Web API (C#), ASP.NET Core.
- **Data**: SQL Server as the primary database, accessed through Entity Framework Core with
  code-first migrations.
- **Frontend**: Angular (standalone components, strict TypeScript).
- **API documentation**: OpenAPI/Swagger.
- **Logging**: structured logging via `ILogger<T>` (Serilog recommended).
- **Testing**: xUnit (or equivalent) for .NET unit/integration tests; Angular's default test
  runner for the frontend.
- Adding a new framework, database, or major library requires a documented justification in
  the feature's `plan.md` Complexity Tracking section.

## Development Workflow & Quality Gates

- All code MUST be production-ready and maintainable: readable naming, no dead code, no
  unresolved TODOs in merged code without a linked task, and nullable reference types enabled.
- Every change goes through a pull request with at least one review that verifies
  compliance with this constitution.
- CI MUST build the solution, run all automated tests, and run linters/analyzers
  (.NET analyzers with warnings treated as errors; ESLint for Angular) before merge.
- Each feature's `plan.md` MUST pass the Constitution Check gate before implementation.

## Governance

- This constitution supersedes all other development practices for this project. Where a
  practice conflicts with it, the constitution wins.
- Amendments MUST be proposed via pull request that updates this file, includes a Sync
  Impact Report, and updates any affected templates.
- Versioning follows semantic versioning: MAJOR for removed or redefined principles, MINOR
  for new principles or materially expanded guidance, PATCH for clarifications.
- Any deviation from a principle MUST be justified in the feature plan's Complexity Tracking
  table and approved during review.
- Compliance is reviewed on every pull request and at each `/speckit.plan` Constitution Check.

**Version**: 1.0.0 | **Ratified**: 2026-09-29 | **Last Amended**: 2026-09-29
