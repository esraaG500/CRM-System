<p align="center">
  <img src="frontend/projects/staff/public/brand/crm-logo.png" alt="CRM logo" width="96" height="96" />
</p>

<h1 align="center">Customer Support CRM</h1>

<p align="center">
  A bilingual (English / Arabic) customer support CRM for support teams: customers, tickets and an agent desk,
  built with a .NET 8 Web API and an Angular staff app.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Angular 20" src="https://img.shields.io/badge/Angular-20-DD0031?logo=angular&logoColor=white" />
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL%20Server-2019%2B-CC2927?logo=microsoftsqlserver&logoColor=white" />
  <img alt="Languages" src="https://img.shields.io/badge/UI-English%20%7C%20%D8%B9%D8%B1%D8%A8%D9%8A-5b3b8c" />
</p>

---

## Contents

- [Overview](#overview)
- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Development accounts](#development-accounts)
- [Demo data](#demo-data)
- [Running tests](#running-tests)
- [API](#api)
- [Localization and RTL](#localization-and-rtl)
- [Security](#security)
- [Spec-driven development](#spec-driven-development)
- [Roadmap](#roadmap)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Overview

The CRM gives a support team one place to manage customers and their requests. Agents work tickets from a
personal desk, supervisors assign and monitor work across departments, and every change is recorded.

The full product scope (12 modules: customers, tickets, channels, agent dashboard, SLA and automation, knowledge
base, AI features, customer portal, reports, security and administration, integrations, and platform) is defined in
[`specs/001-support-crm-mvp/spec.md`](specs/001-support-crm-mvp/spec.md). This repository currently delivers the
**first release slice**: customer management, the ticket lifecycle and the agent desk. See [Roadmap](#roadmap).

## Features

### Available now

**Customers**
- Search by name, email, phone, contact person or reference number (`CUS-000001`)
- Individual and company customers; companies have contact persons with one primary contact
- Profile with details, contacts, notes and an activity timeline (tickets, messages, notes)
- Editing is protected against overwrites: saving a record someone else just changed is rejected with a clear message
- Customers can be flagged **Needs review** (used for unknown senders once inbound channels arrive)

**Tickets**
- Create tickets for a customer and contact, with category, priority, channel and department
  (the department defaults from the category)
- Ticket list with views (assigned to me, unassigned, all I can see), search and filters
  (status, priority, department)
- Status workflow: New, Open, In progress, Waiting on customer, Resolved, Closed; invalid moves are blocked,
  and closing an unresolved ticket requires a reason
- Assign (supervisors), take from the department queue (agents), escalate with a reason (up to level 3,
  reassigns to the department's escalation owner)
- Conversation: replies to the customer and internal notes with @mentions (never visible to customers)
- Full change history: who changed what, when and why
- Reference numbers `TCK-000001`, priority shown as a colored edge on every row

**Agent desk (dashboard)**
- My tickets by status, my open queue by priority, tickets waiting for my first reply
- Department queue with one-click **Take**
- Today's new and resolved counts
- Team overview for supervisors: open tickets by priority and per agent

**Platform**
- English (default) and Arabic, with the full layout switching between left-to-right and right-to-left
- Responsive: desktop, tablet and phone (navigation becomes a slide-in drawer)
- Role-based access: Administrator, Supervisor, Agent
- Department scoping: agents only see their own departments' tickets
- Audit log of every create, update, delete, sign-in, failed sign-in and denied action

## Tech stack

| Area | Technology |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API (controllers), C# 12 |
| Data | SQL Server (Express, LocalDB or full), Entity Framework Core 8, code-first migrations |
| Auth | ASP.NET Core Identity, JWT access tokens (15 min) + rotating refresh tokens (HttpOnly cookie) |
| Validation and errors | FluentValidation, RFC 7807 Problem Details |
| Logging | Serilog (structured), correlation IDs, optional Seq |
| API docs | OpenAPI / Swagger, contract in [`contracts/openapi.yaml`](specs/001-support-crm-mvp/contracts/openapi.yaml) |
| Frontend | Angular 20 (standalone components, signals, OnPush, strict TypeScript), Angular Material 3 |
| i18n | Transloco (runtime language switching), CSS logical properties for RTL |
| Tests | xUnit, Shouldly, NSubstitute, WebApplicationFactory, Respawn, Testcontainers, NetArchTest; Jasmine/Karma |

## Architecture

The backend follows **Clean Architecture**. Dependencies point inwards, and architecture tests fail the build if
a layer reaches outwards.

```text
            ┌────────────────────────────────────────────┐
            │  Crm.Api  (controllers, auth, middleware)  │
            └───────────────┬──────────────────┬─────────┘
                            │                  │
                            ▼                  ▼
   ┌──────────────────────────────┐    ┌─────────────────────────────────┐
   │  Crm.Application             │◄───│  Crm.Infrastructure             │
   │  use cases, DTOs, validation,│    │  EF Core, Identity, JWT,        │
   │  ports (IAppDbContext, ...)  │    │  audit interceptor, seeders     │
   └──────────────┬───────────────┘    └─────────────────────────────────┘
                  ▼
   ┌──────────────────────────────┐
   │  Crm.Domain                  │   entities and business rules,
   │  Customer, Ticket, Message   │   no framework dependencies
   └──────────────────────────────┘
```

- **Domain** holds the rules: for example, the ticket state machine, reopen window and escalation limits live
  in `Ticket`, and they're unit-tested without a database.
- **Application** contains one handler per use case (commands and queries), dispatched through a small in-house
  dispatcher that runs FluentValidation first. Controllers contain no business logic.
- **Infrastructure** implements the ports: EF Core persistence, identity, token issuing, the user directory,
  audit logging and seeding.
- **Api** maps HTTP to use cases and back: routing, versioning (`/api/v1`), permission policies, Problem Details.

The frontend is an Angular workspace (`frontend/`) with the **staff** app today; the customer portal and chat
widget are planned as separate apps in the same workspace.

## Repository layout

```text
backend/
  Crm.sln
  src/
    Crm.Domain/            entities, value objects, domain rules
    Crm.Application/       use cases (Customers, Tickets, Dashboard, Lookups), DTOs, validators, ports
    Crm.Infrastructure/    EF Core DbContext, configurations, migrations, identity, seeders
    Crm.Api/               controllers (V1), security, middleware, Program.cs
  tests/
    Crm.Domain.UnitTests/
    Crm.Application.UnitTests/
    Crm.Architecture.Tests/
    Crm.Api.IntegrationTests/
frontend/
  projects/staff/          staff app: core (api, auth, i18n), layout, features, shared
specs/001-support-crm-mvp/ spec, plan, research, data model, contracts, tasks, quickstart
.specify/                  Spec Kit templates, scripts and the project constitution
```

## Getting started

### Prerequisites

- .NET 8 runtime and a .NET 8 or 9 SDK
- SQL Server: Express, LocalDB (installed with Visual Studio) or Docker
- Node.js 20+ and npm
- Chrome, for the headless frontend tests

### 1. Configure the database connection

`backend/src/Crm.Api/appsettings.Development.json` contains the development connection string. Point it at your
own SQL Server, or better, override it with user-secrets so it stays out of source control:

```powershell
cd backend/src/Crm.Api

# Examples; pick the one that matches your machine
dotnet user-secrets set "ConnectionStrings:Crm" "Server=.\SQLEXPRESS;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:Crm" "Server=(localdb)\MSSQLLocalDB;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True"
```

### 2. Set the secrets (once per machine)

```powershell
cd backend/src/Crm.Api
dotnet user-secrets set "Jwt:SigningKey"       "<at least 32 random bytes, e.g. a base64 string of 48 bytes>"
dotnet user-secrets set "Seed:AdminPassword"   "<admin password, 10+ characters>"
dotnet user-secrets set "Seed:SamplePassword"  "<password for the sample supervisor and agents>"
```

User-secrets are stored in your user profile, never in the repository.

### 3. Run the API

```powershell
dotnet run --project backend/src/Crm.Api --launch-profile http
```

| URL | What |
|---|---|
| http://localhost:5029/swagger | Swagger UI |
| http://localhost:5029/api/v1 | API base |
| http://localhost:5029/health | Health check |

In Development the API applies migrations and seeds reference data on startup: roles and permissions,
3 departments, a branch, 5 ticket categories, the administrator and the sample users.

To update the database without starting the API:

```powershell
cd backend
$env:CRM_MIGRATIONS_CONNECTION = "<your connection string>"
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Infrastructure
```

### 4. Run the staff app

```powershell
cd frontend
npm ci
npx ng serve staff
```

Open http://localhost:4200. The dev server proxies `/api` to the API on port 5029, so there's no CORS setup to do.

## Development accounts

Created by the seeder in Development. Passwords are the values you set in step 2.

| Email | Role | Departments | Password |
|---|---|---|---|
| `admin@crm.local` | Administrator | All | `Seed:AdminPassword` |
| `supervisor@crm.local` | Supervisor | All | `Seed:SamplePassword` |
| `agent1@crm.local` | Agent | Customer Support | `Seed:SamplePassword` |
| `agent2@crm.local` | Agent | Customer Support, Billing | `Seed:SamplePassword` |
| `agent3@crm.local` | Agent | Technical Support | `Seed:SamplePassword` |

| Role | Can |
|---|---|
| Administrator | Everything, including deleting customers and viewing all departments |
| Supervisor | See all departments, assign tickets, team overview on the dashboard |
| Agent | Work tickets in their departments, take tickets from the queue, manage customers |

Five failed sign-ins lock an account for 15 minutes.

## Demo data

Fill the database with realistic, bilingual demo data: 24 customers (companies with contacts, and individuals)
and 96 tickets spread over the last six weeks, in every status, with replies, internal notes, escalations and
full history.

```powershell
dotnet run --project backend/src/Crm.Api -- --seed-demo    # add demo data, then exit (skips if already present)
dotnet run --project backend/src/Crm.Api -- --reset-demo   # remove demo data and seed it again
```

- Runs in Development only.
- Never touches records you created yourself: demo customers are marked with an ERP reference starting `DEMO-`.
- Deterministic: the same data every time.
- Set `Seed:DemoData` to `true` to seed on every startup instead.

Sign in as `agent2@crm.local` for the busiest personal desk.

## Running tests

```powershell
# Backend: unit, architecture and integration tests (real SQL Server; LocalDB by default)
dotnet test backend/Crm.sln

# The same integration tests against a disposable SQL Server container
$env:CRM_TEST_USE_DOCKER = "1"; dotnet test backend/Crm.sln

# Frontend unit tests
cd frontend
npx ng test staff --watch=false --browsers=ChromeHeadless
```

| Suite | Covers |
|---|---|
| `Crm.Domain.UnitTests` | Customer rules, ticket state machine, reopen window, escalation |
| `Crm.Application.UnitTests` | Dispatcher and validation pipeline |
| `Crm.Architecture.Tests` | Layer dependency rules (Domain framework-free, controllers never touch the database) |
| `Crm.Api.IntegrationTests` | Every endpoint over HTTP against a real database: auth, customers, tickets, dashboard, seeding |
| Frontend | Auth interceptor (token + refresh-and-retry), language/RTL switching, UI components |

Integration tests create a throwaway database per run, reset data between tests with Respawn and delete the
database afterwards. They never use the EF Core in-memory provider.

## API

All endpoints are under `/api/v1` and, except sign-in and health, require a bearer token.

| Area | Endpoints |
|---|---|
| Auth | `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me` |
| Customers | `GET/POST /customers`, `GET/PUT/DELETE /customers/{id}`, contacts, `GET /customers/{id}/timeline`, `POST /customers/{id}/notes` |
| Tickets | `GET/POST /tickets`, `GET/PATCH /tickets/{id}`, `POST .../assign`, `.../take`, `.../status`, `.../escalate`, `GET .../history`, `GET/POST .../messages`, `POST .../notes` |
| Dashboard | `GET /dashboard/agent`, `GET /lookups` |

**Try it in Swagger:**
1. Call `POST /api/v1/auth/login` with `{ "email": "supervisor@crm.local", "password": "<your sample password>" }`.
2. Copy `accessToken` from the response.
3. Click **Authorize** and paste the token.

**Conventions:**
- Errors are RFC 7807 Problem Details with a stable `code` (for example `ticket.invalid-transition`) and a
  `correlationId` to quote in support requests.
- Updates send the record's `version`; a stale version returns `409 Conflict` instead of overwriting.
- Lists accept `page` and `pageSize` (max 100) and return `{ items, page, pageSize, totalCount }`.

The full contract, including the endpoints planned for later modules, is in
[`specs/001-support-crm-mvp/contracts/openapi.yaml`](specs/001-support-crm-mvp/contracts/openapi.yaml).

## Localization and RTL

- Translations: [`frontend/projects/staff/public/i18n/en.json`](frontend/projects/staff/public/i18n/en.json) and
  [`ar.json`](frontend/projects/staff/public/i18n/ar.json). Keep both files in sync when adding keys.
- Switch languages with the **English | عربي** control in the top bar or on the sign-in page; the choice is
  remembered per browser.
- Layouts use CSS logical properties (`inline-start`, `inline-end`), so they mirror automatically in Arabic.
- Mixed-language content (for example English ticket subjects in the Arabic UI) uses automatic text direction.
- Bilingual reference data (departments, categories, branches) is stored with `nameEn` and `nameAr`.

## Security

- Every endpoint requires authentication by default; permissions are checked server-side on every action.
- Access tokens live only in memory in the browser; the refresh token is an HttpOnly, SameSite=Strict cookie,
  rotated on every use, and reuse of a revoked token revokes all of the user's sessions.
- Passwords are hashed by ASP.NET Core Identity; accounts lock after 5 failed attempts.
- All input is validated at the boundary; database access is parameterized through EF Core.
- The audit log records who did what and when, with before and after values, the client IP and the correlation ID.
- Secrets (signing key, passwords, connection strings with credentials) belong in user-secrets or environment
  variables, never in `appsettings*.json`.
- HTTPS redirection and HSTS are enabled outside Development.

## Spec-driven development

This project is built with [GitHub Spec Kit](https://github.com/github/spec-kit):

| Document | Purpose |
|---|---|
| [`.specify/memory/constitution.md`](.specify/memory/constitution.md) | Non-negotiable engineering principles (architecture, testing, security, frontend standards) |
| [`spec.md`](specs/001-support-crm-mvp/spec.md) | What the product does: user stories, requirements, success criteria |
| [`plan.md`](specs/001-support-crm-mvp/plan.md) and [`research.md`](specs/001-support-crm-mvp/research.md) | How it's built and why each technology was chosen |
| [`data-model.md`](specs/001-support-crm-mvp/data-model.md) | Entities, ticket state machine, validation rules |
| [`contracts/`](specs/001-support-crm-mvp/contracts/) | REST, real-time, webhook and AI contracts |
| [`tasks.md`](specs/001-support-crm-mvp/tasks.md) | 212 implementation tasks with progress |
| [`quickstart.md`](specs/001-support-crm-mvp/quickstart.md) | Setup and per-story validation walkthrough |

With Claude Code, the workflow commands are `/speckit-specify`, `/speckit-plan`, `/speckit-tasks` and
`/speckit-implement`.

## Roadmap

Delivery follows the priorities in the spec. Progress per task is tracked in
[`tasks.md`](specs/001-support-crm-mvp/tasks.md).

| Release | Scope | Status |
|---|---|---|
| **1 (P1)** | Customers, tickets, agent desk | ✅ Core done |
| | Admin screens (users, roles, departments, categories, settings, branding, audit viewer) | Planned |
| | Attachments, customer merge, two-factor sign-in, password reset | Planned |
| **2 (P2)** | SLA targets, auto-assignment and escalation rules; notifications and live updates | Planned |
| | Tasks and reminders, quick replies | Planned |
| | Customer portal and web forms, satisfaction surveys | Planned |
| | Channels: email, WhatsApp, SMS, live chat | Planned |
| **3 (P3)** | Knowledge base, reports and management dashboards | Planned |
| | AI assistance (summaries, suggested replies, categorization, chatbot) | Planned |
| | Integrations: API keys, webhooks, ERP | Planned |
| Ongoing | CI pipeline, full-text search, performance and security hardening, deployment | Planned |

## Troubleshooting

| Problem | Fix |
|---|---|
| `Jwt:SigningKey must be at least 32 bytes` | Set the signing key with user-secrets (step 2) and run with the `http` or `https` profile. |
| `address already in use` on start | Another copy of the API is running; stop it first. |
| Cannot connect to SQL Server | Check the server name: a named instance needs `Server=MACHINE\SQLEXPRESS`. For LocalDB, run `sqllocaldb start MSSQLLocalDB`. |
| Signed out right after signing in | Make sure the Angular app is served with `ng serve staff`, which proxies `/api` to the API. |
| Swagger calls return `401` | Sign in with `POST /auth/login` and paste the token into **Authorize**. |
| Certificate warning at `https://localhost:7206` | Run `dotnet dev-certs https --trust` once, or use the `http` profile. |

## Contributing

1. Create a branch from `main`.
2. Follow the [constitution](.specify/memory/constitution.md): business logic stays out of controllers,
   database access stays in Infrastructure, and every change comes with tests.
3. Make sure `dotnet test backend/Crm.sln`, the frontend tests and `npx ng build staff` pass.
4. Open a pull request describing what changed and how it was tested.

---

Internal project. All rights reserved.
