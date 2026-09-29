# Phase 0 Research: Customer Support CRM (Simple Business Edition)

**Feature**: 001-support-crm-mvp | **Date**: 2026-09-29

Each entry: **Decision** / **Rationale** / **Alternatives considered**. All Technical Context
unknowns from `plan.md` are resolved here.

---

## R1. Backend runtime and API style

- **Decision**: .NET 8 (LTS) ASP.NET Core Web API with MVC controllers, API versioning via
  `Asp.Versioning.Mvc` (URL segment `/api/v1`), OpenAPI via Swashbuckle, RFC 7807 Problem Details.
- **Rationale**: Mandated by constitution (Technology Stack, Principle III). Controllers keep the
  API layer thin and explicit; URL versioning is simplest for external integrators.
- **Alternatives**: Minimal APIs (less structure for ~150 endpoints); header/media-type
  versioning (harder for integrators and web-form embeds).

## R2. Application layer pattern (keeping business logic out of controllers)

- **Decision**: Use-case handlers per command/query (`ICommandHandler<TCommand,TResult>`,
  `IQueryHandler<TQuery,TResult>`) registered in DI, with a small in-house dispatcher and
  decorators for validation, logging and transactions. FluentValidation validators per command.
  Manual DTO mapping via static extension methods.
- **Rationale**: Satisfies Principles I/II without taking commercial-license dependencies
  (MediatR and AutoMapper moved to commercial licensing in 2025). Handlers are trivially unit-testable.
- **Alternatives**: MediatR (license cost for commercial use); fat application services
  (grow large across 11 modules); Mapster (acceptable, but manual mapping is explicit and
  analyzer-checked).

## R3. Persistence

- **Decision**: SQL Server 2022 + EF Core 8 (code-first migrations) in Infrastructure.
  Conventions: `rowversion` concurrency tokens on Ticket/Customer/Article (edge case: concurrent
  edits); soft delete (`IsDeleted`) with global query filters; `DateTimeOffset` UTC timestamps;
  `nvarchar` for all text (Arabic). Repositories only where aggregate logic needs them; queries
  use `IAppDbContext` abstraction exposed to Application.
- **Rationale**: Constitution mandates SQL Server + EF Core. Rowversion gives the "warn on stale
  save" behavior required by the spec.
- **Alternatives**: Dapper for reads (may be added later for heavy reports if EF proves slow).

## R4. Search (customers, tickets, knowledge base)

- **Decision**: SQL Server Full-Text Search with Arabic (LCID 1025) and English (1033) word
  breakers on customer name/email/phone, ticket subject/description, and article title/body.
  Exact lookups (phone, reference number, email) use regular indexes.
- **Rationale**: Meets SC-007 (<2 s at 100k customers / 500k tickets) without extra
  infrastructure; supports Arabic.
- **Alternatives**: Elasticsearch/OpenSearch (better relevance, but a new cluster to run —
  rejected for "simple business"); `LIKE '%x%'` (does not scale).

## R5. Authentication and authorization

- **Decision**: ASP.NET Core Identity (users stored in SQL Server) issuing JWT access tokens
  (15 min) + rotating refresh tokens (7 days, stored hashed, HttpOnly cookie for browsers).
  Optional TOTP two-factor. Portal customers are Identity users with the `Customer` role.
  Authorization: permission-based policies (`Permissions.Tickets.Assign`, …) mapped from roles;
  data scope (department/branch) enforced in Application query handlers via `ICurrentUser`.
  API keys for integrations (hashed, scoped, revocable) via a separate authentication scheme.
- **Rationale**: Principle V; no external IdP dependency for a single organization; permission
  policies make role editing (FR-042) possible without code changes.
- **Alternatives**: External IdP (Entra ID / Keycloak) — deferred per spec assumption (SSO later);
  cookie-only auth (works for SPA, but the API is also consumed by integrations and the widget).

## R6. Background processing (SLA timers, notifications, channel polling, webhooks)

- **Decision**: Hangfire with SQL Server storage. Recurring jobs: SLA evaluator (every minute),
  email inbox poller (every minute), reminder dispatcher, webhook delivery with exponential
  retry. Outbox table written in the same transaction as domain changes, drained by Hangfire.
- **Rationale**: Reuses SQL Server (no broker), has a dashboard and retries; outbox guarantees
  events/notifications are never lost when a transaction commits.
- **Alternatives**: Quartz.NET (no built-in dashboard/retry UI); RabbitMQ/Azure Service Bus
  (extra infrastructure); plain `BackgroundService` (no persistence/retry).

## R7. Real-time (live chat, in-app notifications, dashboard updates)

- **Decision**: ASP.NET Core SignalR hubs: `/hubs/agent` (staff: notifications, ticket updates,
  chat) and `/hubs/chat` (website visitors, anonymous with signed chat-session token).
  Single server by default; Redis backplane only if scaled out.
- **Rationale**: FR-015 real-time chat; native to .NET; Angular client via `@microsoft/signalr`.
- **Alternatives**: Third-party chat SaaS (data leaves the system); raw WebSockets (reinventing reconnection/groups).

## R8. Communication channels

- **Decision**: One `IChannelAdapter` per channel in Infrastructure, behind an Application
  port (`IInboundMessageProcessor`, `IOutboundMessageSender`):
  - **Email**: MailKit — IMAP polling of the support mailbox (inbound) and SMTP (outbound);
    threading by `In-Reply-To`/`References` headers and `[#TCK-000123]` subject token.
  - **WhatsApp**: Meta WhatsApp Business Cloud API — webhook for inbound (signature-verified),
    Graph API for outbound; respects the 24-hour customer-service window (templates outside it).
  - **SMS**: `ISmsProvider` abstraction; first adapter targets a KSA-capable provider with
    inbound webhook (e.g., Unifonic or Twilio — chosen at deployment via configuration).
  - **Live chat**: SignalR (R7) + embeddable widget.
  - **Web forms**: public API endpoint with rate limiting and CAPTCHA (Cloudflare Turnstile).
- **Rationale**: Spec assumption: business supplies its own provider accounts; adapters keep
  providers swappable (Principle II).
- **Alternatives**: Aggregator (e.g., a single CPaaS for all channels) — fewer adapters but vendor
  lock-in and cost; can be added as another adapter later.

## R9. AI features

- **Decision**: Claude via the official Anthropic C# SDK (NuGet `Anthropic`), wrapped in an
  Application port `IAiAssistant` with an Infrastructure implementation `ClaudeAiAssistant`.
  - Model: `claude-opus-5-5` (configurable in settings, `Ai:Model`). Effort set explicitly per
    use case (`low` for classification/chatbot, `medium` for summaries/replies).
  - Classification & priority suggestion: structured output (`OutputConfig.Format` JSON schema)
    constrained to the configured category/priority lists.
  - Summaries & suggested replies: single calls; reply in the customer's language.
  - Chatbot: retrieval from public published KB articles (SQL full-text top-N) passed as
    context with citations; hand-off to a human when no article matches or the user asks.
  - Prompt caching: stable system prompt + category list first, with a cache breakpoint; the
    per-ticket content after it.
  - Server-side refusal fallbacks (`fallbacks: "default"`) enabled; handle `stop_reason` before reading content.
  - All calls go through a background-safe service with timeout; failures return a
    "temporarily unavailable" result (edge case). Global and per-feature toggles (FR-032) checked
    before any data is sent. Only needed ticket fields are sent (no attachments by default).
- **Rationale**: Meets FR-028..FR-032 with single-call patterns (no agent loop needed); structured
  output removes brittle parsing; port keeps provider replaceable.
- **Alternatives**: Local/self-hosted model (lower quality, GPU infrastructure); vector database
  for RAG (unnecessary at this KB size — full-text retrieval is sufficient for v1).

## R10. File storage (attachments, branding logo)

- **Decision**: `IFileStorage` port; default adapter stores files on local disk / network share
  outside the web root; optional Azure Blob adapter. Metadata in SQL. Max 10 MB, allow-list of
  MIME types verified by content sniffing; downloads only through authorized API endpoint.
- **Rationale**: Simple deployment; no binary data bloating SQL Server; Principle V.
- **Alternatives**: `FILESTREAM`/varbinary in SQL (backup bloat); S3/Blob only (cloud dependency).

## R11. Integrations

- **Decision**: Public REST API (same `/api/v1` contract, API-key scheme); outbound webhooks
  (event subscriptions) signed with HMAC-SHA256 (`X-CRM-Signature`), at-least-once delivery with
  retries (1 m, 5 m, 30 m, 2 h, 12 h) via outbox. ERP: `IErpConnector` port with a
  configurable generic REST adapter (read-only: customer lookup, recent orders/invoices), cached 10 min.
- **Rationale**: FR-046..FR-048; ERP varies by customer so a generic adapter + port is the simplest robust choice.
- **Alternatives**: Vendor-specific ERP adapters (SAP, Odoo, Dynamics) — can be added as adapters on demand.

## R12. Frontend

- **Decision**: Angular (current stable, ≥ 20) workspace with three applications and shared libraries:
  - `staff` — agent/supervisor/admin SPA.
  - `portal` — customer portal + public web form pages.
  - `chat-widget` — embeddable live-chat web component (Angular Elements).
  - `libs/shared` — API client (typed from OpenAPI), auth, i18n, UI kit.
  Standalone components only, signals for state, `inject()`, built-in control flow, `OnPush`,
  lazy routes, typed reactive forms, functional guards/interceptors, `strict` + `strictTemplates`.
  UI: Angular Material + CDK (RTL support via `Directionality`), CSS logical properties.
  i18n: Transloco (runtime language switch without rebuild — FR-049 per-user language).
  API client: generated from `contracts/openapi.yaml` (`openapi-generator` typescript-angular) to
  keep contracts explicit (Principle III).
- **Rationale**: Constitution Principle VIII; separate apps keep the public portal bundle small and
  isolate staff-only code.
- **Alternatives**: Angular built-in i18n (compile-time, one build per locale — no instant switch);
  PrimeNG (fine, but Material has stronger RTL/a11y story); single app for staff + portal (larger
  attack surface).

## R13. Observability

- **Decision**: Serilog with message templates; sinks: console (JSON) + rolling file, Seq
  optional. Correlation ID middleware (`X-Correlation-ID`) enriching logs and Problem Details.
  Global exception handler (`IExceptionHandler`). Health checks at `/health` (SQL, Hangfire, storage).
  Sensitive fields destructured with masking policy.
- **Rationale**: Principle VI.
- **Alternatives**: OpenTelemetry tracing — can be added later; not needed for a single service.

## R14. Testing

- **Decision**:
  - Backend: xUnit, NSubstitute, Shouldly; `Domain.UnitTests`, `Application.UnitTests`;
    `Api.IntegrationTests` using `WebApplicationFactory` + Testcontainers `MsSqlContainer`
    (real SQL Server, full-text enabled image); Respawn to reset data between tests.
    Contract tests validate responses against `contracts/openapi.yaml`.
  - External providers (email, WhatsApp, SMS, Claude, ERP) faked at the port boundary in
    integration tests; adapters get their own tests with WireMock.Net.
  - Frontend: Angular default unit test runner (Vitest in current Angular) + Angular Testing Library;
    Playwright for a small set of end-to-end journeys (P1 stories, portal submit, chat).
- **Rationale**: Principle VII (real SQL Server, no in-memory provider).
- **Alternatives**: FluentAssertions (v8 commercial license); EF in-memory provider (forbidden by constitution).

## R15. Localization, time zones and business hours

- **Decision**: Backend resource files (`.resx`) for validation messages and notification
  templates in `ar` and `en`, chosen from user/customer preferred language; `Accept-Language`
  for anonymous. All timestamps stored UTC; business-hours calendar evaluated in the
  organization time zone (default `Asia/Riyadh`, configurable) using `TimeZoneInfo` with IANA IDs.
- **Rationale**: FR-049, FR-020; spec assumption UTC+3.
- **Alternatives**: NodaTime (more precise but another dependency; KSA has no DST).

## R16. Deployment and environments

- **Decision**: Docker Compose for local development (SQL Server 2022 with full-text, Seq,
  MailHog for email). Production target: containerized API + static Angular apps behind a
  reverse proxy (IIS/Nginx) — deployment platform finalized by operations. CI: GitHub Actions
  (build, test with Testcontainers, lint, analyzers as errors).
- **Rationale**: Reproducible dev environment; constitution quality gates.
- **Alternatives**: Kubernetes (overkill for a single-organization deployment).

## R17. Performance approach

- **Decision**: Keyset/offset pagination (max page size 100) on all lists; covering indexes on
  ticket queues (`DepartmentId, Status, Priority, CreatedAt`), SLA due columns indexed for the
  evaluator; report queries on pre-aggregated daily stats table refreshed hourly by Hangfire;
  response compression; output caching for public KB pages.
- **Rationale**: SC-007/SC-008 targets at 500k tickets and 1,000 concurrent visitors.
- **Alternatives**: Separate reporting database/warehouse (overkill for v1).
