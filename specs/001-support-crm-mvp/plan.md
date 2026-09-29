# Implementation Plan: Customer Support CRM (Simple Business Edition)

**Branch**: `001-support-crm-mvp` | **Date**: 2026-09-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-support-crm-mvp/spec.md`

## Summary

Build a single-organization customer support CRM covering all 12 modules of the source document
at an essential level: tickets, customers, administration (P1); agent dashboard, customer portal,
multi-channel messaging, SLA & automation (P2); knowledge base, reports, AI assistance and
integrations (P3), with Arabic/English, responsive UI, multi-department/branch and branding
throughout.

Technical approach: a .NET 8 Web API following Clean Architecture (Domain / Application /
Infrastructure / Api) on SQL Server via EF Core, with Hangfire for SLA timers and outbound
delivery, SignalR for live chat and notifications, channel adapters behind ports (MailKit,
WhatsApp Cloud API, SMS provider), Claude (Anthropic C# SDK) behind an `IAiAssistant` port,
and an Angular workspace with three standalone-component apps (staff, portal, chat widget)
consuming a generated client from an explicit OpenAPI contract. See [research.md](research.md).

## Technical Context

**Language/Version**: C# 12 / .NET 8 (LTS); TypeScript 5.x (strict) / Angular current stable (≥ 20)
**Primary Dependencies**: ASP.NET Core Web API, EF Core 8 (SQL Server provider), ASP.NET Core Identity,
FluentValidation, Asp.Versioning.Mvc, Swashbuckle, Serilog, Hangfire (SQL Server storage), SignalR,
MailKit, Anthropic C# SDK (`Anthropic`), ClosedXML (Excel export), HtmlSanitizer;
Angular Material + CDK, Transloco, `@microsoft/signalr`, openapi-generator (typescript-angular)
**Storage**: SQL Server 2022 (Full-Text Search enabled); file attachments on disk/network share
via `IFileStorage` (Azure Blob adapter optional)
**Testing**: xUnit, NSubstitute, Shouldly, Testcontainers (MsSql), Respawn, WireMock.Net,
`WebApplicationFactory`; Angular default test runner + Angular Testing Library; Playwright E2E
**Target Platform**: Linux or Windows server (containers) behind reverse proxy; modern evergreen
browsers on desktop, tablet, mobile
**Project Type**: Web application (backend API + frontend SPAs + embeddable widget)
**Performance Goals**: search < 2 s at 100k customers / 500k tickets (SC-007); 200 staff +
1,000 concurrent portal/chat visitors (SC-008); inbound message → ticket < 1 min (SC-003);
p95 API < 500 ms for standard CRUD
**Constraints**: Arabic RTL + English everywhere; attachments ≤ 10 MB; UTC storage with
`Asia/Riyadh` business hours; HTTPS only; secrets outside source control; AI can be fully disabled
**Scale/Scope**: 1 organization, ~200 staff, 11 user stories, 52 FRs, ~35 entities,
~130 REST operations, ~60 screens across 3 frontend apps

No NEEDS CLARIFICATION items remain — all resolved in [research.md](research.md) (R1–R17).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Gate | Pre-design | Post-design |
|---|---|---|---|---|
| I | Clean Architecture & Layer Boundaries | 4 layers; Domain has no framework refs; no logic in controllers; DB access only in Infrastructure; DTOs at API↔Application | ✅ Planned | ✅ Structure below; handlers own logic (R2); `IAppDbContext` port, EF in Infrastructure (R3); DTOs in `Crm.Application/**/Dtos`, contracts in openapi.yaml |
| II | SOLID & DI | All services via DI; ports/adapters for externals | ✅ | ✅ Ports: `IAiAssistant`, `IChannelAdapter`, `ISmsProvider`, `IFileStorage`, `IErpConnector`, `IClock`, `ICurrentUser` |
| III | RESTful, explicit contracts | OpenAPI contract, versioned `/api/v1`, Problem Details, typed Angular client | ✅ | ✅ [contracts/openapi.yaml](contracts/openapi.yaml) + hubs/webhooks/AI contracts; client generated from contract (R12) |
| IV | Async by default | async I/O + CancellationToken end to end | ✅ | ✅ All handler/port signatures async with `CancellationToken` (see ai-assistant.md) |
| V | Validated input & secure by design | Validation at boundary; auth on all endpoints; RBAC server-side; sensitive data protected; parameterized queries | ✅ | ✅ FluentValidation per command; default `[Authorize]` fallback policy; explicit anonymous endpoints listed in openapi (`security: []`); permission policies + data scope (R5); webhook HMAC, API keys hashed, secrets masked; CAPTCHA + rate limits on public endpoints |
| VI | Structured logging | Serilog templates, correlation IDs, global exception handler | ✅ | ✅ R13; `correlationId` in ProblemDetails schema |
| VII | Automated testing | Unit + integration (real SQL Server) + Angular unit tests, CI gate | ✅ | ✅ R14; Testcontainers MsSql; contract tests against openapi.yaml |
| VIII | Modern strict Angular | Standalone, strict TS, signals, OnPush, lazy routes | ✅ | ✅ R12 |
| Stack | .NET 8, SQL Server, EF Core, Angular | Matches | ✅ | ✅ |
| Stack | New framework/major library justification | Hangfire, SignalR, Anthropic SDK, MailKit, Transloco justified | ✅ | ✅ See research R6, R7, R8, R9, R12 |

**Result**: PASS — no violations. Complexity Tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-support-crm-mvp/
├── plan.md              # This file
├── research.md          # Phase 0 decisions (R1–R17)
├── data-model.md        # Phase 1 entities, state machine, validation
├── quickstart.md        # Phase 1 dev setup + validation walkthrough
├── contracts/
│   ├── openapi.yaml     # REST contract (/api/v1)
│   ├── realtime-hubs.md # SignalR hubs (/hubs/agent, /hubs/chat)
│   ├── webhook-events.md# Outbound webhook events
│   └── ai-assistant.md  # IAiAssistant port behavior
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── Crm.sln
├── Directory.Build.props            # nullable, warnings-as-errors, analyzers
├── src/
│   ├── Crm.Domain/                  # entities, value objects, enums, domain events, rules (no deps)
│   │   ├── Customers/  Tickets/  Messaging/  Knowledge/  Sla/  Organization/
│   │   ├── Identity/   Integrations/  Common/
│   ├── Crm.Application/             # use cases (commands/queries + handlers), DTOs, validators, ports
│   │   ├── Abstractions/            # IAppDbContext, ICurrentUser, IClock, IAiAssistant, IChannelAdapter,
│   │   │                            # IFileStorage, IErpConnector, INotificationSender, IOutbox
│   │   ├── Common/                  # dispatcher, decorators (validation, logging, transaction), paging
│   │   ├── Customers/  Tickets/  Messaging/  Dashboard/  Tasks/  QuickReplies/  Notifications/
│   │   ├── Sla/  Automation/  Knowledge/  Ai/  Portal/  Reports/  Admin/  Integrations/
│   ├── Crm.Infrastructure/          # EF Core, Identity stores, adapters, jobs
│   │   ├── Persistence/             # CrmDbContext, configurations, migrations, interceptors (audit, history)
│   │   ├── Identity/                # JWT, refresh tokens, API key auth handler
│   │   ├── Channels/                # Email (MailKit), WhatsApp, Sms, WebForm
│   │   ├── Ai/                      # ClaudeAiAssistant (Anthropic SDK)
│   │   ├── Erp/  Files/  Search/  Notifications/  Webhooks/
│   │   └── Jobs/                    # Hangfire jobs: SLA evaluator, inbox poller, outbox, reminders, stats
│   └── Crm.Api/                     # controllers, hubs, auth setup, middleware, Program.cs
│       ├── Controllers/V1/  Hubs/  Middleware/  Localization/
└── tests/
    ├── Crm.Domain.UnitTests/
    ├── Crm.Application.UnitTests/
    ├── Crm.Infrastructure.Tests/    # adapters with WireMock.Net
    └── Crm.Api.IntegrationTests/    # WebApplicationFactory + Testcontainers SQL Server + contract tests

frontend/
├── angular.json  package.json  tsconfig.json (strict)
├── projects/
│   ├── staff/                       # agent/supervisor/admin SPA
│   │   └── src/app/features/{dashboard,tickets,customers,knowledge,reports,admin,settings}/
│   ├── portal/                      # customer portal + public web form
│   │   └── src/app/features/{auth,tickets,knowledge,feedback,web-form}/
│   └── chat-widget/                 # Angular Elements <crm-chat> web component
└── libs/
    └── shared/                      # generated API client, auth, i18n (Transloco ar/en), ui kit, signalr

e2e/                                 # Playwright journeys
deploy/
└── docker-compose.dev.yml           # sqlserver (FTS), seq, mailhog
.github/workflows/ci.yml             # build, test, lint, analyzers
```

**Structure Decision**: Web application layout with `backend/` (.NET solution split into the four
Clean Architecture projects plus test projects) and `frontend/` (one Angular workspace, three
apps, shared library). The OpenAPI contract in `specs/.../contracts/openapi.yaml` is the source
for the generated Angular client.

## Delivery Phases (input for /speckit.tasks)

| Phase | Stories | Outcome |
|---|---|---|
| 0 Foundation | – | Solution skeleton, CI, auth, audit interceptor, Problem Details, logging, i18n/RTL shell, Docker dev env |
| 1 MVP (P1) | US3 → US2 → US1 | Admin sets up org; agents manage customers & tickets end to end |
| 2 (P2) | US7 → US4 → US5 → US6 | SLA & auto-assign; dashboard; portal & web form; email → WhatsApp → SMS → live chat |
| 3 (P3) | US8 → US9 → US10 → US11 | Knowledge base; reports; AI; API keys, webhooks, ERP |

US7 precedes US4 because the dashboard highlights SLA risk. Email is delivered first within US6 as
it has no provider approval lead time; WhatsApp Business approval should be requested early.

## Key Risks

| Risk | Mitigation |
|---|---|
| WhatsApp Business account/template approval takes weeks | Start approval in Phase 0; channel is an independent adapter |
| Arabic full-text search relevance | Validate with sample Arabic data early (Phase 1 search spike) |
| Scope size (52 FRs) | Strict phase gates; each story independently releasable |
| AI data privacy concerns | Off by default until admin enables; data minimization (ai-assistant.md) |

## Complexity Tracking

Not required — Constitution Check passed with no violations.
