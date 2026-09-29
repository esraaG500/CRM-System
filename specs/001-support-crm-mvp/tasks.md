---
description: "Task list for Customer Support CRM (Simple Business Edition)"
---

# Tasks: Customer Support CRM (Simple Business Edition)

**Input**: Design documents from `/specs/001-support-crm-mvp/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: REQUIRED by the constitution (Principle VII). Every story phase starts with unit and
integration tests that MUST fail before implementation. Integration tests use real SQL Server
(Testcontainers), never the EF in-memory provider.

**Organization**: Tasks are grouped by user story. Phases follow the plan's delivery order
(P1: US3 → US2 → US1; P2: US7 → US4 → US5 → US6; P3: US8 → US9 → US10 → US11).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story from spec.md (US1–US11)

## Path Conventions

- `B/` = `backend/src/` · `BT/` = `backend/tests/` · `F/` = `frontend/projects/` · `FL/` = `frontend/libs/shared/src/lib/`
- Written in full in each task below so tasks are self-contained.

## Conventions every task must follow

- Application use case = `{Name}Command`/`{Name}Query` record + `{Name}Handler` + `{Name}Validator` (FluentValidation) + DTOs, in the feature folder named in the task; handlers are the only place business logic lives.
- Controllers in `backend/src/Crm.Api/Controllers/V1/` only: bind request DTO → dispatch → map result to HTTP; routes, status codes and schemas MUST match `specs/001-support-crm-mvp/contracts/openapi.yaml`.
- Every entity configuration lives in `backend/src/Crm.Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`; add an EF migration named after the story (e.g., `US2_Customers`) at the end of each story's data tasks.
- All I/O is `async` with `CancellationToken`; log with message templates via `ILogger<T>`.
- Frontend: standalone components, signals, `inject()`, `@if/@for`, `OnPush`, lazy routes, typed reactive forms, all text via Transloco keys in `frontend/projects/*/src/assets/i18n/{ar,en}.json`, CSS logical properties for RTL.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository, solutions, tooling, dev environment

- [ ] T001 Create repository layout `backend/`, `frontend/`, `e2e/`, `deploy/`, `.github/workflows/` and root `.gitignore` (dotnet + node + IDE files) and `.editorconfig` at repo root
- [ ] T002 Create .NET solution `backend/Crm.sln` with projects `backend/src/Crm.Domain` (classlib), `backend/src/Crm.Application` (classlib), `backend/src/Crm.Infrastructure` (classlib), `backend/src/Crm.Api` (webapi, controllers) targeting `net8.0`, with references Api→Application+Infrastructure, Infrastructure→Application, Application→Domain
- [ ] T003 Create test projects `backend/tests/Crm.Domain.UnitTests`, `backend/tests/Crm.Application.UnitTests`, `backend/tests/Crm.Infrastructure.Tests`, `backend/tests/Crm.Api.IntegrationTests` (xUnit) with packages NSubstitute, Shouldly, Testcontainers.MsSql, Respawn, WireMock.Net, Microsoft.AspNetCore.Mvc.Testing, and add them to `backend/Crm.sln`
- [ ] T004 [P] Add `backend/Directory.Build.props` (Nullable enable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel latest-recommended, LangVersion 12) and `backend/Directory.Packages.props` for central package versions
- [ ] T005 [P] Add `backend/tests/Crm.Architecture.Tests` project using NetArchTest.Rules with tests asserting Domain has no dependency on EF Core/ASP.NET/Application/Infrastructure and Api controllers do not reference `Crm.Infrastructure.Persistence` in `backend/tests/Crm.Architecture.Tests/LayerDependencyTests.cs`
- [ ] T006 [P] Create Angular workspace in `frontend/` (no default app, `strict: true`, `strictTemplates: true`), then apps `frontend/projects/staff`, `frontend/projects/portal`, `frontend/projects/chat-widget` (standalone, routing, SCSS) and library `frontend/libs/shared`
- [ ] T007 [P] Install and configure Angular Material + CDK theme, Transloco, `@microsoft/signalr`, ESLint (`@angular-eslint`), Prettier in `frontend/package.json`, `frontend/eslint.config.js`, `frontend/.prettierrc`
- [ ] T008 [P] Add `frontend/openapitools.json` and npm script `generate:api` in `frontend/package.json` that generates the typescript-angular client from `specs/001-support-crm-mvp/contracts/openapi.yaml` into `frontend/libs/shared/src/lib/api/`
- [ ] T009 [P] Create `deploy/docker-compose.dev.yml` with services sqlserver (SQL Server 2022 image with Full-Text Search, port 1433), seq (5341), mailhog (1025/8025) and a volume for SQL data
- [ ] T010 [P] Create CI workflow `.github/workflows/ci.yml`: dotnet restore/build/test (Testcontainers on ubuntu), `npm ci`, `ng lint`, `ng test --watch=false`, `ng build` for all three apps
- [ ] T011 [P] Add Playwright project in `e2e/` with `e2e/playwright.config.ts` (baseURLs for staff 4200 and portal 4300) and a smoke test `e2e/tests/smoke.spec.ts`
- [ ] T012 [P] Add `dotnet-tools.json` (dotnet-ef) in `backend/.config/dotnet-tools.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting core required by every story — ⚠️ no story work before this phase is complete

### Tests for foundation

- [ ] T013 [P] Create integration test fixture `backend/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` (WebApplicationFactory + MsSqlContainer with full-text, applies migrations, Respawn reset, test auth helper issuing JWTs for seeded roles) and `backend/tests/Crm.Api.IntegrationTests/Infrastructure/IntegrationTestBase.cs`
- [ ] T014 [P] Create contract test helper that validates HTTP responses against `specs/001-support-crm-mvp/contracts/openapi.yaml` (Microsoft.OpenApi.Readers + JSON schema validation) in `backend/tests/Crm.Api.IntegrationTests/Infrastructure/OpenApiContractValidator.cs`
- [ ] T015 [P] Integration tests for Problem Details, correlation ID header, 401 on anonymous access to a protected endpoint, and `/health` in `backend/tests/Crm.Api.IntegrationTests/Platform/PlatformPipelineTests.cs`
- [ ] T016 [P] Integration tests for auth: login success, wrong password 401 + audit `SignInFailed`, refresh rotation, logout, `/auth/me` permissions list in `backend/tests/Crm.Api.IntegrationTests/Auth/AuthTests.cs`
- [ ] T017 [P] Unit tests for `ValidationDecorator` and `TransactionDecorator` in `backend/tests/Crm.Application.UnitTests/Common/DecoratorTests.cs`

### Domain & application core

- [ ] T018 [P] Create base types `Entity`, `AuditableEntity` (CreatedAt/By, UpdatedAt/By), `ISoftDelete`, `IHasRowVersion`, `IDomainEvent`, `Result`/`Error` in `backend/src/Crm.Domain/Common/`
- [ ] T019 [P] Create enums `Language`, `Channel`, `TicketStatus`, `TicketPriority`, `CustomerType`, `UserType` in `backend/src/Crm.Domain/Common/Enums.cs`
- [ ] T020 [P] Create application abstractions `IAppDbContext`, `ICurrentUser` (UserId, Permissions, DepartmentIds, BranchId, Language, IsCustomer), `IClock`, `IOutbox`, `IFileStorage` in `backend/src/Crm.Application/Abstractions/`
- [ ] T021 Create CQRS plumbing `ICommand<T>`, `IQuery<T>`, `ICommandHandler`, `IQueryHandler`, `IDispatcher` + `Dispatcher` and decorators `ValidationDecorator`, `LoggingDecorator`, `TransactionDecorator` in `backend/src/Crm.Application/Common/Messaging/` and DI registration `backend/src/Crm.Application/DependencyInjection.cs` (scan handlers + validators)
- [ ] T022 [P] Create `PagedResult<T>`, `PageRequest` (max 100) and `IQueryable` paging extensions in `backend/src/Crm.Application/Common/Paging/`
- [ ] T023 [P] Create permission constants (all keys in data-model.md, e.g. `tickets.assign`, `customers.export`, `admin.users.manage`, `data.scope.all-departments`) in `backend/src/Crm.Application/Common/Security/Permissions.cs` and `DataScope` helper that filters queries by department/branch for `ICurrentUser` in `backend/src/Crm.Application/Common/Security/DataScopeExtensions.cs`

### Organization & identity entities (needed by every story)

- [ ] T024 [P] Create `Department`, `Branch`, `BusinessCalendar` (+ `WorkingHours`, `Holiday`) entities in `backend/src/Crm.Domain/Organization/`
- [ ] T025 [P] Create `User` (extends IdentityUser<Guid>, fields per data-model.md), `Role` (IdentityRole<Guid> + IsSystem), `RolePermission`, `UserDepartment`, `RefreshToken` in `backend/src/Crm.Domain/Identity/` (Identity base types referenced via `Microsoft.Extensions.Identity.Stores` only)
- [ ] T026 [P] Create `AuditLogEntry`, `OutboxMessage`, `SystemSetting`, `Attachment` entities in `backend/src/Crm.Domain/Common/`

### Persistence

- [ ] T027 Create `CrmDbContext` (IdentityDbContext<User, Role, Guid>, implements `IAppDbContext`, soft-delete global filters, `nvarchar` defaults, enum-as-string convention, rowversion convention) in `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` with configurations for T024–T026 entities in `backend/src/Crm.Infrastructure/Persistence/Configurations/`
- [ ] T028 Create SaveChanges interceptors `AuditableEntityInterceptor` (timestamps, CreatedBy/UpdatedBy), `AuditLogInterceptor` (before/after JSON for entities marked `[Audited]`), `OutboxInterceptor` (domain events → OutboxMessage) in `backend/src/Crm.Infrastructure/Persistence/Interceptors/`
- [ ] T029 Create migration `Foundation` and seeder `backend/src/Crm.Infrastructure/Persistence/Seed/FoundationSeeder.cs` (system roles Administrator/Supervisor/Agent/Customer with default permissions, default calendar Sun–Thu 08:00–17:00 `Asia/Riyadh`, default settings, admin user from config)
- [ ] T030 [P] Implement `LocalFileStorage : IFileStorage` (path outside web root, content-type sniffing, 10 MB limit, allow-list from settings) in `backend/src/Crm.Infrastructure/Files/LocalFileStorage.cs`
- [ ] T031 Create `backend/src/Crm.Infrastructure/DependencyInjection.cs` registering DbContext (SQL Server, retry), interceptors, Identity, file storage, `SystemClock`, and options classes bound from configuration

### Authentication & authorization

- [ ] T032 Implement JWT issuing + refresh token rotation (hashed, HttpOnly cookie) + TOTP 2FA challenge in `backend/src/Crm.Infrastructure/Identity/TokenService.cs` and `backend/src/Crm.Application/Auth/` handlers `Login`, `VerifyTwoFactor`, `Refresh`, `Logout`, `GetCurrentUser`, `ForgotPassword`, `ResetPassword`
- [ ] T033 Implement `CurrentUser : ICurrentUser` from claims (permissions, departments, branch, language) in `backend/src/Crm.Api/Security/CurrentUser.cs` and permission policy provider `PermissionAuthorizationHandler` in `backend/src/Crm.Api/Security/`
- [ ] T034 Implement `AuthController` (`/auth/*` per openapi) in `backend/src/Crm.Api/Controllers/V1/AuthController.cs` with login rate limiting

### API pipeline

- [ ] T035 Configure `Program.cs` in `backend/src/Crm.Api/Program.cs`: Serilog (JSON console + file + Seq), API versioning `/api/v1`, controllers with fallback `RequireAuthenticatedUser` policy, Swashbuckle, CORS for staff/portal origins, rate limiter, response compression, localization (`ar`,`en`), health checks (SQL), HTTPS/HSTS
- [ ] T036 [P] Implement `CorrelationIdMiddleware` (`X-Correlation-ID`, Serilog enrichment) in `backend/src/Crm.Api/Middleware/CorrelationIdMiddleware.cs`
- [ ] T037 [P] Implement `GlobalExceptionHandler : IExceptionHandler` mapping validation → 400 `ValidationProblemDetails`, not found → 404, forbidden → 403, `DbUpdateConcurrencyException`/domain conflicts → 409 with `code` and `currentVersion`, others → 500 without internals, all with `correlationId` in `backend/src/Crm.Api/Middleware/GlobalExceptionHandler.cs`
- [ ] T038 [P] Create `.resx` resources for validation and error messages in `backend/src/Crm.Api/Localization/SharedResource.ar.resx` and `SharedResource.en.resx`, wired to FluentValidation language manager
- [ ] T039 Configure Hangfire (SQL Server storage, dashboard at `/jobs` for Administrator) and `OutboxProcessorJob` (every 15 s, dispatches domain events to registered `IOutboxHandler`s, retries) in `backend/src/Crm.Infrastructure/Jobs/OutboxProcessorJob.cs`
- [ ] T040 Create SignalR `AgentHub` skeleton at `/hubs/agent` (JWT from `access_token` query, groups `user:{id}` and `dept:{id}`) in `backend/src/Crm.Api/Hubs/AgentHub.cs` and `IRealtimeNotifier` port + implementation in `backend/src/Crm.Application/Abstractions/IRealtimeNotifier.cs` / `backend/src/Crm.Api/Hubs/SignalRRealtimeNotifier.cs`

### Frontend foundation

- [ ] T041 [P] Run `npm run generate:api` and export the generated client from `frontend/libs/shared/src/public-api.ts`
- [ ] T042 [P] Implement auth in shared lib: `AuthStore` (signals: user, permissions), `authInterceptor` (Bearer + refresh on 401), `permissionGuard`, `hasPermission` structural directive in `frontend/libs/shared/src/lib/auth/`
- [ ] T043 [P] Implement i18n + direction: Transloco loader, `LanguageService` switching `ar`/`en`, setting `<html dir lang>` and Material `Directionality`, persisting choice in `frontend/libs/shared/src/lib/i18n/`
- [ ] T044 [P] Implement `problemDetailsInterceptor` mapping 400/409/403/503 to localized snackbar messages and form errors in `frontend/libs/shared/src/lib/http/problem-details.interceptor.ts`
- [ ] T045 [P] Implement `SignalRService` (connect with token, reconnect, typed event streams as signals/observables) in `frontend/libs/shared/src/lib/realtime/signalr.service.ts`
- [ ] T046 Create staff app shell: responsive layout (side nav collapsing to bottom/drawer on mobile), top bar with language switch, login page, lazy routes placeholder for each feature in `frontend/projects/staff/src/app/app.routes.ts`, `frontend/projects/staff/src/app/layout/`, `frontend/projects/staff/src/app/features/auth/login.page.ts`
- [ ] T047 [P] Unit tests for `authInterceptor`, `permissionGuard` and `LanguageService` in `frontend/libs/shared/src/lib/**/*.spec.ts`

**Checkpoint**: `dotnet test` and `ng test` green; admin can sign in to the staff shell in Arabic and English.

---

## Phase 3: User Story 3 — Administer Users, Roles, Departments and Branches (P1) 🎯 MVP part 1

**Goal**: Admin configures organization, users, roles/permissions, categories, settings, branding; audit log records everything.
**Independent Test**: Admin creates department, branch, Agent user; agent is denied admin actions (logged); audit log shows the changes.

### Tests for US3 ⚠️ write first, must fail

- [ ] T048 [P] [US3] Integration tests for `/admin/users` (create, update, deactivate returns tickets to queue placeholder, reset password) and permission denial logged in `backend/tests/Crm.Api.IntegrationTests/Admin/UsersTests.cs`
- [ ] T049 [P] [US3] Integration tests for `/admin/roles` (custom role, cannot delete system role → 409) and `/admin/permissions` in `backend/tests/Crm.Api.IntegrationTests/Admin/RolesTests.cs`
- [ ] T050 [P] [US3] Integration tests for `/admin/departments`, `/admin/branches`, `/admin/categories` CRUD + contract validation in `backend/tests/Crm.Api.IntegrationTests/Admin/OrganizationTests.cs`
- [ ] T051 [P] [US3] Integration tests for `/admin/settings`, `/admin/branding/logo`, `/public/branding` in `backend/tests/Crm.Api.IntegrationTests/Admin/SettingsTests.cs`
- [ ] T052 [P] [US3] Integration tests for `/admin/audit-log` filters and read-only guarantee (no update/delete endpoints, DB trigger/permission) in `backend/tests/Crm.Api.IntegrationTests/Admin/AuditLogTests.cs`
- [ ] T053 [P] [US3] Unit tests for user validators (department required for staff, unique email) and role permission validation in `backend/tests/Crm.Application.UnitTests/Admin/AdminValidatorTests.cs`

### Implementation for US3

- [ ] T054 [P] [US3] Create `Category` entity in `backend/src/Crm.Domain/Tickets/Category.cs` with configuration and add configurations for Department/Branch/Calendar if missing; migration `US3_Administration`
- [ ] T055 [P] [US3] Implement user use cases `ListUsers`, `GetUser`, `CreateUser` (sends invitation email via `IEmailSender` port), `UpdateUser`, `DeactivateUser` (raises `UserDeactivated` event), `ResetUserPassword` in `backend/src/Crm.Application/Admin/Users/`
- [ ] T056 [P] [US3] Implement role use cases `ListRoles`, `CreateRole`, `UpdateRole`, `DeleteRole`, `ListPermissions` in `backend/src/Crm.Application/Admin/Roles/`
- [ ] T057 [P] [US3] Implement department, branch, category use cases (list/create/update) in `backend/src/Crm.Application/Admin/Organization/`
- [ ] T058 [P] [US3] Implement settings & branding use cases `GetSettings`, `UpdateSettings`, `UploadLogo`, `GetPublicBranding` in `backend/src/Crm.Application/Admin/Settings/`
- [ ] T059 [P] [US3] Implement `QueryAuditLog` in `backend/src/Crm.Application/Admin/Audit/QueryAuditLog.cs` and log `PermissionDenied` from `PermissionAuthorizationHandler` in `backend/src/Crm.Api/Security/PermissionAuthorizationHandler.cs`
- [ ] T060 [P] [US3] Implement SMTP `IEmailSender` (MailKit) with localized templates for invitation/password reset in `backend/src/Crm.Infrastructure/Notifications/SmtpEmailSender.cs` and `backend/src/Crm.Infrastructure/Notifications/Templates/`
- [ ] T061 [US3] Implement controllers `AdminUsersController`, `AdminRolesController`, `AdminOrganizationController` (departments, branches, categories), `AdminSettingsController` (settings, branding logo, audit log), `PublicBrandingController` in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T062 [US3] Staff admin UI — users list/edit dialog, roles & permission matrix page in `frontend/projects/staff/src/app/features/admin/users/` and `frontend/projects/staff/src/app/features/admin/roles/`
- [ ] T063 [P] [US3] Staff admin UI — departments, branches, categories pages in `frontend/projects/staff/src/app/features/admin/organization/`
- [ ] T064 [P] [US3] Staff admin UI — settings & branding page (logo upload, primary color, system name, email footer ar/en) applying branding CSS variables app-wide via `BrandingService` in `frontend/projects/staff/src/app/features/admin/settings/` and `frontend/libs/shared/src/lib/branding/branding.service.ts`
- [ ] T065 [P] [US3] Staff admin UI — audit log viewer with filters in `frontend/projects/staff/src/app/features/admin/audit-log/`
- [ ] T066 [P] [US3] Component tests for permission matrix and user form in `frontend/projects/staff/src/app/features/admin/**/*.spec.ts`

**Checkpoint**: US3 acceptance scenarios 1–5 pass.

---

## Phase 4: User Story 2 — Manage Customer Profiles (P1) 🎯 MVP part 2

**Goal**: Customer profiles, contacts, timeline, notes, attachments, search, merge, auto-create from unknown sender.
**Independent Test**: Create customer + contacts + note + attachment; link a ticket; timeline shows all.

### Tests for US2 ⚠️

- [ ] T067 [P] [US2] Unit tests for `Customer` domain rules (email or phone required, E.164, merge re-points history, NeedsReview) in `backend/tests/Crm.Domain.UnitTests/Customers/CustomerTests.cs`
- [ ] T068 [P] [US2] Integration tests for `/customers` CRUD, search by name/email/phone/reference (Arabic + English), 409 on stale version, contract validation in `backend/tests/Crm.Api.IntegrationTests/Customers/CustomersTests.cs`
- [ ] T069 [P] [US2] Integration tests for contacts, notes, attachments (10 MB → 413, blocked type → 415), timeline order, merge in `backend/tests/Crm.Api.IntegrationTests/Customers/CustomerDetailsTests.cs`
- [ ] T070 [P] [US2] Unit test for `FindOrCreateCustomerBySender` (match by any email/phone, else create with NeedsReview) in `backend/tests/Crm.Application.UnitTests/Customers/FindOrCreateCustomerBySenderTests.cs`

### Implementation for US2

- [ ] T071 [P] [US2] Create `Customer` (aggregate, audited, soft delete, rowversion), `CustomerEmail`, `CustomerPhone`, `ContactPerson`, `Note`, `Tag`, `Address` value object in `backend/src/Crm.Domain/Customers/`
- [ ] T072 [US2] Add EF configurations for customer entities, reference number sequence `CUS-000001` (SQL sequence), indexes, full-text catalog + index on Customer name/emails/phones (migration SQL) in `backend/src/Crm.Infrastructure/Persistence/Configurations/Customers/` and migration `US2_Customers`
- [ ] T073 [P] [US2] Implement `SearchCustomers` (full-text via `CONTAINS`/`FREETEXT` with exact-match fallback on phone/email/reference, data scope) in `backend/src/Crm.Application/Customers/SearchCustomers.cs` and search SQL helper in `backend/src/Crm.Infrastructure/Search/FullTextSearch.cs`
- [ ] T074 [P] [US2] Implement `CreateCustomer`, `UpdateCustomer`, `GetCustomer`, `DeactivateCustomer`, `MergeCustomers` in `backend/src/Crm.Application/Customers/`
- [ ] T075 [P] [US2] Implement contact use cases `ListContacts`, `AddContact`, `UpdateContact`, `RemoveContact` in `backend/src/Crm.Application/Customers/Contacts/`
- [ ] T076 [P] [US2] Implement `AddCustomerNote`, `UploadCustomerAttachment`, `GetCustomerTimeline` (union of tickets/messages/notes/attachments/feedback, paged) in `backend/src/Crm.Application/Customers/Timeline/`
- [ ] T077 [P] [US2] Implement `FindOrCreateCustomerBySender` service used by channels/portal in `backend/src/Crm.Application/Customers/FindOrCreateCustomerBySender.cs`
- [ ] T078 [US2] Implement `CustomersController` (all `/customers/*` except `/erp`) and `FilesController` (`GET /files/{id}` with authorization against owning record) in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T079 [US2] Staff UI — customer search list (debounced search, filters, needs-review badge) in `frontend/projects/staff/src/app/features/customers/customer-list.page.ts`
- [ ] T080 [US2] Staff UI — customer profile page (details form, contacts panel, timeline with infinite scroll, note composer, attachment upload, merge dialog) in `frontend/projects/staff/src/app/features/customers/customer-profile/`
- [ ] T081 [P] [US2] Component tests for customer form validation and timeline rendering in `frontend/projects/staff/src/app/features/customers/**/*.spec.ts`

**Checkpoint**: US2 acceptance scenarios 1–5 pass (scenario 5 verified via `FindOrCreateCustomerBySender` test until channels exist).

---

## Phase 5: User Story 1 — Create, Assign and Resolve Tickets (P1) 🎯 MVP part 3

**Goal**: Full ticket lifecycle with categories, priorities, assignment, status, escalation, history, internal notes, replies (portal/manual channel).
**Independent Test**: Create → assign → status changes → close; history shows every change; reopen within 7 days.

### Tests for US1 ⚠️

- [ ] T082 [P] [US1] Unit tests for `Ticket` state machine (allowed/invalid transitions, reopen window 7 days, escalation level, first response timestamp) in `backend/tests/Crm.Domain.UnitTests/Tickets/TicketStateMachineTests.cs`
- [ ] T083 [P] [US1] Integration tests for `/tickets` create/list filters/get/patch, reference number format, contract validation in `backend/tests/Crm.Api.IntegrationTests/Tickets/TicketsCrudTests.cs`
- [ ] T084 [P] [US1] Integration tests for assign/take/status/escalate endpoints, 409 on invalid transition or stale version, department scoping (agent sees only own department) in `backend/tests/Crm.Api.IntegrationTests/Tickets/TicketWorkflowTests.cs`
- [ ] T085 [P] [US1] Integration tests for `/tickets/{id}/history`, `/notes` (internal, mentions), `/messages` (reply, internal excluded when `includeInternal=false`), attachments in `backend/tests/Crm.Api.IntegrationTests/Tickets/TicketConversationTests.cs`
- [ ] T086 [P] [US1] Integration test: deactivating a user (US3) returns their open tickets to the department queue in `backend/tests/Crm.Api.IntegrationTests/Tickets/UserDeactivationTicketsTests.cs`

### Implementation for US1

- [ ] T087 [P] [US1] Create `Ticket` aggregate (fields per data-model.md, state machine methods `Assign`, `Take`, `ChangeStatus`, `Escalate`, `Reopen`, `RecordFirstResponse`, domain events) in `backend/src/Crm.Domain/Tickets/Ticket.cs`
- [ ] T088 [P] [US1] Create `Message` (+ `MessageMention`), `TicketHistoryEntry` entities in `backend/src/Crm.Domain/Tickets/`
- [ ] T089 [US1] Add EF configurations for Ticket/Message/History, `TCK-000001` sequence, indexes from data-model.md, full-text index on Subject/Description, migration `US1_Tickets` in `backend/src/Crm.Infrastructure/Persistence/Configurations/Tickets/`
- [ ] T090 [US1] Implement `TicketHistoryInterceptor` writing `TicketHistoryEntry` rows for every tracked field change in `backend/src/Crm.Infrastructure/Persistence/Interceptors/TicketHistoryInterceptor.cs`
- [ ] T091 [P] [US1] Implement `CreateTicket` (defaults department from category, validates contact belongs to customer, attachments), `UpdateTicket`, `GetTicket` (with `allowedTransitions`), `ListTickets` (all filters, data scope, sort) in `backend/src/Crm.Application/Tickets/`
- [ ] T092 [P] [US1] Implement `AssignTicket`, `TakeTicket`, `ChangeTicketStatus`, `EscalateTicket` (to department escalation owner), `GetTicketHistory` in `backend/src/Crm.Application/Tickets/Workflow/`
- [ ] T093 [P] [US1] Implement `AddInternalNote` (mentions → `Mentioned` notification event), `ListMessages`, `ReplyToTicket` (outbound message via `IOutboundMessageSender` port; default in-app/portal sender), `UploadTicketAttachment` in `backend/src/Crm.Application/Tickets/Conversation/`
- [ ] T094 [US1] Implement `Notification` entity + `NotificationService` (create in-app notification, push via `IRealtimeNotifier`, email for enabled types) and outbox handlers for `TicketAssigned`, `UserMentioned` in `backend/src/Crm.Domain/Notifications/Notification.cs`, `backend/src/Crm.Application/Notifications/`
- [ ] T095 [US1] Implement `UserDeactivated` outbox handler returning open tickets to queue in `backend/src/Crm.Application/Tickets/Handlers/ReturnTicketsOnUserDeactivated.cs`
- [ ] T096 [US1] Implement `ReopenOnCustomerReply` logic (closed ≤ 7 days → reopen; else new linked ticket) used by inbound processing in `backend/src/Crm.Application/Tickets/ReopenOnCustomerReply.cs`
- [ ] T097 [US1] Implement `TicketsController` and `TicketMessagesController` (`/tickets/*`, `/tickets/{id}/messages/*`) in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T098 [US1] Staff UI — ticket list/queue page (filters, saved "My tickets"/"Unassigned" views, status/priority chips, SLA badge placeholder) in `frontend/projects/staff/src/app/features/tickets/ticket-list.page.ts`
- [ ] T099 [US1] Staff UI — create ticket dialog (customer autocomplete, contact, category, priority, attachments) in `frontend/projects/staff/src/app/features/tickets/create-ticket.dialog.ts`
- [ ] T100 [US1] Staff UI — ticket detail page (conversation with public/internal tabs, reply composer, internal note with @mentions, status actions from `allowedTransitions`, assign, escalate-with-reason dialog, history tab, customer side panel, stale-version conflict banner) in `frontend/projects/staff/src/app/features/tickets/ticket-detail/`
- [ ] T101 [P] [US1] Component tests for ticket detail actions and conflict handling in `frontend/projects/staff/src/app/features/tickets/**/*.spec.ts`
- [ ] T102 [P] [US1] Playwright E2E for P1 journey (admin creates agent → agent creates customer → ticket → close) in `e2e/tests/p1-ticket-lifecycle.spec.ts`

**Checkpoint**: 🎯 MVP complete (US3 + US2 + US1). Deployable and demonstrable.

---

## Phase 6: User Story 7 — SLA Targets, Auto-Assignment, Escalation and Alerts (P2)

**Goal**: SLA due times in business hours, pause on PendingCustomer, round-robin assignment, warning/breach escalation, notifications.
**Independent Test**: High SLA 1 h; create High ticket; warning at 80 %, breach → supervisor notified, breach recorded.

### Tests for US7 ⚠️

- [ ] T103 [P] [US7] Unit tests for `BusinessHoursCalculator` (working hours, weekends Fri/Sat, holidays, `Asia/Riyadh`, spanning days) in `backend/tests/Crm.Domain.UnitTests/Sla/BusinessHoursCalculatorTests.cs`
- [ ] T104 [P] [US7] Unit tests for SLA application (due times on create, recalculation on priority change from creation time, pause/resume accumulation) in `backend/tests/Crm.Domain.UnitTests/Sla/SlaTimerTests.cs`
- [ ] T105 [P] [US7] Unit tests for round-robin assignment (only active + available + department members, cursor rotation, no agent → queue + supervisor alert) in `backend/tests/Crm.Application.UnitTests/Automation/RoundRobinAssignerTests.cs`
- [ ] T106 [P] [US7] Integration tests for `/sla/*` and `/automation/*` CRUD + `SlaEvaluatorJob` producing warning/breach notifications and escalation actions using a fake `IClock` in `backend/tests/Crm.Api.IntegrationTests/Sla/SlaTests.cs`
- [ ] T107 [P] [US7] Integration tests for `/notifications`, `/notifications/read`, `/notifications/preferences` (mandatory types cannot be muted → 400) in `backend/tests/Crm.Api.IntegrationTests/Notifications/NotificationsTests.cs`

### Implementation for US7

- [ ] T108 [P] [US7] Create `SlaPolicy`, `AssignmentRule`, `EscalationRule`, `NotificationPreference` entities in `backend/src/Crm.Domain/Sla/` and `backend/src/Crm.Domain/Notifications/`, configurations, migration `US7_Sla`
- [ ] T109 [P] [US7] Implement `BusinessHoursCalculator` domain service in `backend/src/Crm.Domain/Sla/BusinessHoursCalculator.cs`
- [ ] T110 [US7] Implement `SlaService` (apply policy on `TicketCreated`/`TicketPriorityChanged`, pause/resume on `PendingCustomer`, mark first response/resolution met) as outbox handlers in `backend/src/Crm.Application/Sla/SlaService.cs`
- [ ] T111 [US7] Implement `RoundRobinAssigner` handling `TicketCreated` when unassigned in `backend/src/Crm.Application/Automation/RoundRobinAssigner.cs`
- [ ] T112 [US7] Implement `SlaEvaluatorJob` (Hangfire every minute: find tickets crossing warning threshold or due time, set breach flags, write history, run matching `EscalationRule`s — notify, reassign, raise priority) in `backend/src/Crm.Infrastructure/Jobs/SlaEvaluatorJob.cs` and rule execution in `backend/src/Crm.Application/Automation/EscalationRuleExecutor.cs`
- [ ] T113 [P] [US7] Implement SLA/calendar/rule CRUD use cases in `backend/src/Crm.Application/Sla/Policies/`, `backend/src/Crm.Application/Sla/Calendars/`, `backend/src/Crm.Application/Automation/Rules/`
- [ ] T114 [P] [US7] Implement notification use cases `ListNotifications`, `MarkRead`, `GetPreferences`, `UpdatePreferences` in `backend/src/Crm.Application/Notifications/`
- [ ] T115 [US7] Implement `SlaController`, `AutomationController`, `NotificationsController` in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T116 [US7] Staff UI — SLA policies, business calendar (hours + holidays), assignment & escalation rule pages in `frontend/projects/staff/src/app/features/admin/sla/`
- [ ] T117 [US7] Staff UI — notification bell with real-time updates (`NotificationReceived`), notification list, preferences page; SLA badge/countdown component used in ticket list/detail in `frontend/projects/staff/src/app/layout/notifications/` and `frontend/libs/shared/src/lib/ui/sla-badge.component.ts`
- [ ] T118 [P] [US7] Component tests for SLA badge states and notification bell in `frontend/**/sla-badge.component.spec.ts` and `frontend/projects/staff/src/app/layout/notifications/*.spec.ts`

**Checkpoint**: US7 scenarios 1–5 pass.

---

## Phase 7: User Story 4 — Agent Dashboard and Team Collaboration (P2)

**Goal**: Personal dashboard, tasks & reminders, quick replies with placeholders, internal notes/mentions (from US1) surfaced.
**Independent Test**: Dashboard shows counts & at-risk tickets; reminder notifies; quick reply fills placeholders; mention notifies.

### Tests for US4 ⚠️

- [ ] T119 [P] [US4] Integration tests for `/dashboard/agent` (counts, at-risk/overdue ordering, queue count) in `backend/tests/Crm.Api.IntegrationTests/Dashboard/AgentDashboardTests.cs`
- [ ] T120 [P] [US4] Integration tests for `/tasks` CRUD + `ReminderJob` notification at due time (fake clock) in `backend/tests/Crm.Api.IntegrationTests/Tasks/TasksTests.cs`
- [ ] T121 [P] [US4] Unit tests for quick reply placeholder rendering (ar/en, unknown placeholder left intact, HTML-escaped values) in `backend/tests/Crm.Application.UnitTests/QuickReplies/QuickReplyRendererTests.cs`
- [ ] T122 [P] [US4] Integration tests for `/quick-replies` (shared vs personal visibility, render) in `backend/tests/Crm.Api.IntegrationTests/QuickReplies/QuickRepliesTests.cs`

### Implementation for US4

- [ ] T123 [P] [US4] Create `TaskItem`, `QuickReply` entities + configurations, migration `US4_Productivity` in `backend/src/Crm.Domain/Productivity/`
- [ ] T124 [P] [US4] Implement `GetAgentDashboard` query in `backend/src/Crm.Application/Dashboard/GetAgentDashboard.cs`
- [ ] T125 [P] [US4] Implement task use cases (list/create/update/delete) in `backend/src/Crm.Application/Tasks/` and `ReminderJob` (every minute) in `backend/src/Crm.Infrastructure/Jobs/ReminderJob.cs`
- [ ] T126 [P] [US4] Implement quick reply use cases + `QuickReplyRenderer` in `backend/src/Crm.Application/QuickReplies/`
- [ ] T127 [US4] Implement `DashboardController`, `TasksController`, `QuickRepliesController` in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T128 [US4] Add `AgentHub` methods `WatchTicket`, `UnwatchTicket`, `SetAvailability` and emit `TicketUpdated`, `MessageAdded` from outbox handlers in `backend/src/Crm.Api/Hubs/AgentHub.cs` and `backend/src/Crm.Application/Realtime/TicketRealtimeHandlers.cs`
- [ ] T129 [US4] Staff UI — agent dashboard home (status counts, at-risk/overdue lists, tasks due today, availability toggle, live refresh via SignalR) in `frontend/projects/staff/src/app/features/dashboard/`
- [ ] T130 [P] [US4] Staff UI — tasks & reminders page and "add reminder" from ticket/customer in `frontend/projects/staff/src/app/features/tasks/`
- [ ] T131 [P] [US4] Staff UI — quick replies management page and quick-reply picker in the ticket reply composer in `frontend/projects/staff/src/app/features/quick-replies/` and `frontend/projects/staff/src/app/features/tickets/ticket-detail/quick-reply-picker.component.ts`
- [ ] T132 [P] [US4] Component tests for dashboard and quick-reply picker in `frontend/projects/staff/src/app/features/{dashboard,quick-replies}/**/*.spec.ts`

**Checkpoint**: US4 scenarios 1–5 pass.

---

## Phase 8: User Story 5 — Customer Self-Service Portal and Web Forms (P2)

**Goal**: Public web form, portal registration/sign-in, my tickets, replies, FAQ suggestions, satisfaction survey.
**Independent Test**: Register, submit ticket, see agent reply, view history, rate after resolution.

### Tests for US5 ⚠️

- [ ] T133 [P] [US5] Integration tests for `/public/web-form` (creates ticket + customer via `FindOrCreateCustomerBySender`, CAPTCHA required, duplicate within 2 min blocked, rate limit 429) in `backend/tests/Crm.Api.IntegrationTests/Portal/WebFormTests.cs`
- [ ] T134 [P] [US5] Integration tests for `/portal/register`, `/portal/tickets*` (own tickets only, company tickets with permission, internal notes never returned, attachments) in `backend/tests/Crm.Api.IntegrationTests/Portal/PortalTicketsTests.cs`
- [ ] T135 [P] [US5] Integration tests for feedback: survey created on `Resolved`, `/portal/tickets/{id}/feedback` and `/public/feedback/{token}` (one per ticket, expired → 410) in `backend/tests/Crm.Api.IntegrationTests/Portal/FeedbackTests.cs`

### Implementation for US5

- [ ] T136 [P] [US5] Create `Feedback`, `FeedbackRequest`, `WebFormSubmissionLog` entities + configurations, migration `US5_Portal` in `backend/src/Crm.Domain/Portal/`
- [ ] T137 [P] [US5] Implement `ICaptchaVerifier` port and Cloudflare Turnstile adapter in `backend/src/Crm.Application/Abstractions/ICaptchaVerifier.cs` and `backend/src/Crm.Infrastructure/Security/TurnstileCaptchaVerifier.cs`
- [ ] T138 [P] [US5] Implement `SubmitWebForm` (duplicate/rate protection, confirmation email with reference) in `backend/src/Crm.Application/Portal/SubmitWebForm.cs`
- [ ] T139 [P] [US5] Implement `RegisterCustomerAccount` (links/creates Customer, email confirmation) and portal ticket use cases `ListMyTickets`, `GetMyTicket` (public messages only), `CreatePortalTicket`, `AddPortalReply` (triggers `ReopenOnCustomerReply`), `UploadPortalFile` in `backend/src/Crm.Application/Portal/`
- [ ] T140 [P] [US5] Implement feedback: outbox handler on `TicketResolved` creating `FeedbackRequest` and sending survey link (email) + auto-close job after 7 days in `backend/src/Crm.Application/Portal/Feedback/` and `backend/src/Crm.Infrastructure/Jobs/AutoCloseResolvedTicketsJob.cs`
- [ ] T141 [US5] Implement `PortalController`, `PublicWebFormController`, `PublicFeedbackController` with rate-limit policies in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T142 [US5] Portal app shell (branding from `/public/branding`, language switch, responsive layout, customer login/register/forgot password) in `frontend/projects/portal/src/app/layout/` and `frontend/projects/portal/src/app/features/auth/`
- [ ] T143 [US5] Portal — my tickets list, ticket detail with conversation + reply + attachments, new ticket form with live article suggestions (calls `/knowledge/articles?q=` debounced; results shown once US8 is available) in `frontend/projects/portal/src/app/features/tickets/`
- [ ] T144 [P] [US5] Portal — public web form page (embeddable route `/contact`, Turnstile widget) and feedback page (`/feedback/:token`, 1–5 stars + comment) in `frontend/projects/portal/src/app/features/web-form/` and `frontend/projects/portal/src/app/features/feedback/`
- [ ] T145 [P] [US5] Component tests for portal ticket form and feedback page; Playwright E2E portal journey in `frontend/projects/portal/src/app/features/**/*.spec.ts` and `e2e/tests/portal-journey.spec.ts`

**Checkpoint**: US5 scenarios 1–4 pass (scenario 3 fully once US8 lands).

---

## Phase 9: User Story 6 — Multi-Channel Communication (P2)

**Goal**: Email, WhatsApp, SMS, live chat into tickets; replies out on the same channel with delivery status.
**Independent Test**: Message on each enabled channel creates/updates a ticket for the right customer; agent reply arrives on that channel.

### Tests for US6 ⚠️

- [ ] T146 [P] [US6] Unit tests for `InboundMessageProcessor` (thread to open ticket per channel, subject token `[#TCK-…]`, `In-Reply-To`, dedupe by `ExternalMessageId`, reopen rules, new customer flagged) in `backend/tests/Crm.Application.UnitTests/Messaging/InboundMessageProcessorTests.cs`
- [ ] T147 [P] [US6] Adapter tests with WireMock.Net/GreenMail-style fakes for email (IMAP fetch, SMTP send with threading headers), WhatsApp (webhook signature verify, send, 24-h window template fallback), SMS (webhook, send, delivery report) in `backend/tests/Crm.Infrastructure.Tests/Channels/`
- [ ] T148 [P] [US6] Integration tests for `/channels/whatsapp/webhook`, `/channels/sms/webhook` (invalid signature → 401), `/admin/channels*` (secrets masked), `/tickets/{id}/messages/{id}/retry` in `backend/tests/Crm.Api.IntegrationTests/Channels/ChannelsTests.cs`
- [ ] T149 [P] [US6] Integration tests for live chat via SignalR test client: start session, visitor message, `ChatWaiting` to department, `AcceptChat` first-wins, transcript saved to ticket in `backend/tests/Crm.Api.IntegrationTests/Channels/LiveChatTests.cs`

### Implementation for US6

- [ ] T150 [P] [US6] Create `ChannelConfiguration`, `ChatSession` entities + configurations (secrets stored as secret-store references, never plain), migration `US6_Channels` in `backend/src/Crm.Domain/Messaging/`
- [ ] T151 [P] [US6] Define ports `IInboundMessageProcessor`, `IOutboundMessageSender`, `IChannelAdapter` (Channel, SendAsync, TestAsync), `ISecretStore` in `backend/src/Crm.Application/Abstractions/Messaging/`
- [ ] T152 [US6] Implement `InboundMessageProcessor` (customer match via `FindOrCreateCustomerBySender`, thread/create ticket with channel default department, attachments, dedupe) and `OutboundMessageSender` (routes to adapter, updates `DeliveryStatus`, emits `MessageDeliveryChanged`) in `backend/src/Crm.Application/Messaging/`
- [ ] T153 [P] [US6] Implement email adapter (MailKit IMAP poll → inbound processor; SMTP send with `Message-ID`/`In-Reply-To`/subject token, branded localized template) and `EmailInboxPollerJob` (every minute) in `backend/src/Crm.Infrastructure/Channels/Email/`
- [ ] T154 [P] [US6] Implement WhatsApp Cloud API adapter (webhook verification + HMAC signature, inbound text/media, outbound send, template fallback outside 24 h, status callbacks) in `backend/src/Crm.Infrastructure/Channels/WhatsApp/`
- [ ] T155 [P] [US6] Implement SMS adapter via `ISmsProvider` with first provider implementation selected by config (inbound webhook, send, delivery report) in `backend/src/Crm.Infrastructure/Channels/Sms/`
- [ ] T156 [US6] Implement `ChatHub` at `/hubs/chat` (session-token auth, rate limit 20/min, `SendMessage`, `RequestHuman`, `Typing`, `End`) and `AgentHub` chat methods `AcceptChat`, `SendChatMessage`, `Typing`, `EndChat`; `StartChatSession` use case creating ticket with `LiveChat` channel in `backend/src/Crm.Api/Hubs/ChatHub.cs`, `backend/src/Crm.Api/Hubs/AgentHub.cs`, `backend/src/Crm.Application/Messaging/Chat/`
- [ ] T157 [US6] Implement `ChannelWebhooksController`, `AdminChannelsController` (get/put/test), `PublicChatController` (`/public/chat/sessions`), message retry endpoint in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T158 [US6] Staff UI — channel settings pages (enable/disable, provider settings with masked secrets, test button, status) in `frontend/projects/staff/src/app/features/admin/channels/`
- [ ] T159 [US6] Staff UI — channel icon + delivery status/retry on messages, channel selector in reply composer, live chat inbox (waiting chats, accept, chat panel with typing) in `frontend/projects/staff/src/app/features/tickets/ticket-detail/` and `frontend/projects/staff/src/app/features/live-chat/`
- [ ] T160 [US6] Chat widget — `<crm-chat>` web component (Angular Elements; branding, ar/en + RTL, pre-chat form, messages, typing, "talk to a human", reconnect) built to `dist/chat-widget/crm-chat.js` in `frontend/projects/chat-widget/src/`
- [ ] T161 [P] [US6] Component tests for chat widget and live chat inbox; Playwright E2E chat journey in `frontend/projects/chat-widget/src/**/*.spec.ts` and `e2e/tests/live-chat.spec.ts`

**Checkpoint**: US6 scenarios 1–5 pass.

---

## Phase 10: User Story 8 — Knowledge Base (P3)

**Goal**: FAQs, articles, guides in ar/en, public/internal, search, votes, links in replies.
**Independent Test**: Public + internal article; customer finds only the public one; agent finds both.

### Tests for US8 ⚠️

- [ ] T162 [P] [US8] Integration tests for `/knowledge/categories`, `/knowledge/articles` CRUD, publish/unpublish, visibility rules by role/anonymous, Arabic + English full-text search, vote once per voter in `backend/tests/Crm.Api.IntegrationTests/Knowledge/KnowledgeTests.cs`
- [ ] T163 [P] [US8] Unit tests for article HTML sanitization and visibility policy in `backend/tests/Crm.Application.UnitTests/Knowledge/ArticlePolicyTests.cs`

### Implementation for US8

- [ ] T164 [P] [US8] Create `KnowledgeCategory`, `KnowledgeArticle` (audited, soft delete, rowversion), `ArticleVote` + configurations, full-text index on Title/Body, migration `US8_Knowledge` in `backend/src/Crm.Domain/Knowledge/`
- [ ] T165 [P] [US8] Implement category and article use cases (`SearchArticles` with visibility filter, `GetArticle` incrementing views, `CreateArticle`/`UpdateArticle` with HtmlSanitizer, `SetArticlePublished`, `DeleteArticle`, `VoteArticle`) in `backend/src/Crm.Application/Knowledge/`
- [ ] T166 [US8] Implement `KnowledgeController` (anonymous-allowed GETs and vote; output caching for public reads) in `backend/src/Crm.Api/Controllers/V1/KnowledgeController.cs`
- [ ] T167 [US8] Staff UI — knowledge base management (category tree, article list, rich text editor with RTL, ar/en translation link, publish toggle, stats) and "insert article link" in reply composer in `frontend/projects/staff/src/app/features/knowledge/`
- [ ] T168 [US8] Portal — help center (categories, search, article page with helpful/not helpful) and FAQ section in `frontend/projects/portal/src/app/features/knowledge/`
- [ ] T169 [P] [US8] Component tests for article editor and portal search in `frontend/projects/*/src/app/features/knowledge/**/*.spec.ts`

**Checkpoint**: US8 scenarios 1–4 pass; US5 scenario 3 now fully passes.

---

## Phase 11: User Story 9 — Reports and Management Dashboards (P3)

**Goal**: Ticket, SLA, agent, satisfaction reports + management dashboard with filters, export, scope-aware.
**Independent Test**: Totals match underlying tickets; export downloads `.xlsx`; branch-limited supervisor sees only their branch.

### Tests for US9 ⚠️

- [ ] T170 [P] [US9] Integration tests for `DailyStatsJob` aggregation correctness against seeded tickets in `backend/tests/Crm.Api.IntegrationTests/Reports/DailyStatsJobTests.cs`
- [ ] T171 [P] [US9] Integration tests for `/reports/dashboard`, `/reports/{type}` filters/groupBy, data scope, `/reports/{type}/export` (xlsx content-type, audit `Export` entry) in `backend/tests/Crm.Api.IntegrationTests/Reports/ReportsTests.cs`

### Implementation for US9

- [ ] T172 [P] [US9] Create `DailyTicketStat` entity + configuration, migration `US9_Reports` in `backend/src/Crm.Domain/Reports/DailyTicketStat.cs`
- [ ] T173 [US9] Implement `DailyStatsJob` (hourly, recompute last 2 days + today) in `backend/src/Crm.Infrastructure/Jobs/DailyStatsJob.cs`
- [ ] T174 [P] [US9] Implement `GetManagementDashboard` and `GetReport` (tickets, sla, agents, satisfaction; filters; groupBy; scope) in `backend/src/Crm.Application/Reports/`
- [ ] T175 [P] [US9] Implement `IReportExporter` + ClosedXML implementation (localized headers, RTL sheet for Arabic) in `backend/src/Crm.Application/Abstractions/IReportExporter.cs` and `backend/src/Crm.Infrastructure/Reports/ExcelReportExporter.cs`
- [ ] T176 [US9] Implement `ReportsController` in `backend/src/Crm.Api/Controllers/V1/ReportsController.cs`
- [ ] T177 [US9] Staff UI — management dashboard (KPI tiles, charts by channel/status, trend) and reports pages with shared filter bar, table, export button in `frontend/projects/staff/src/app/features/reports/`
- [ ] T178 [P] [US9] Component tests for report filter bar and KPI tiles in `frontend/projects/staff/src/app/features/reports/**/*.spec.ts`

**Checkpoint**: US9 scenarios 1–5 pass.

---

## Phase 12: User Story 10 — AI Assistance (P3)

**Goal**: Summaries, suggested replies, auto-categorization, suggested articles, KB-grounded chatbot with human hand-off; fully switchable off.
**Independent Test**: Summary, suggested reply, auto-category on new ticket, bot hand-off with transcript; disabling AI hides all and sends nothing.

### Tests for US10 ⚠️

- [ ] T179 [P] [US10] Unit tests for AI use cases with fake `IAiAssistant` (disabled → 403 `ai.disabled`, unavailable → 503, refusal handling, suggestion never auto-sent, classification stored as suggestion, data minimization of `AiTicketContext`) in `backend/tests/Crm.Application.UnitTests/Ai/AiUseCaseTests.cs`
- [ ] T180 [P] [US10] Adapter tests for `ClaudeAiAssistant` against WireMock.Net stub of the Messages API (request uses configured model, structured output schema with category enum, cache breakpoint placement, stop_reason refusal mapping, timeout → Unavailable) in `backend/tests/Crm.Infrastructure.Tests/Ai/ClaudeAiAssistantTests.cs`
- [ ] T181 [P] [US10] Integration tests for `/ai/tickets/{id}/*` endpoints and chatbot flow over `/hubs/chat` (answer with article links; hand-off sets `Waiting` and notifies agents) with fake assistant in `backend/tests/Crm.Api.IntegrationTests/Ai/AiEndpointsTests.cs`

### Implementation for US10

- [ ] T182 [P] [US10] Define `IAiAssistant`, `AiResult<T>`, `AiTicketContext`, `KbSnippet`, DTOs per `specs/001-support-crm-mvp/contracts/ai-assistant.md` in `backend/src/Crm.Application/Abstractions/Ai/`
- [ ] T183 [US10] Implement `ClaudeAiAssistant` using NuGet `Anthropic` (model from `Ai:Model`, default `claude-opus-5-5`; effort per operation; structured outputs via `OutputConfig.Format`; stable system prompt with cache breakpoint; server-side refusal fallbacks; check `StopReason` before content; 20 s timeout; token usage logged, never prompt text) in `backend/src/Crm.Infrastructure/Ai/ClaudeAiAssistant.cs` and prompts in `backend/src/Crm.Infrastructure/Ai/Prompts/`
- [ ] T184 [P] [US10] Implement use cases `SummarizeTicket`, `SuggestReply` (retrieves top KB snippets), `ClassifyTicket`, `SuggestArticles` with feature-toggle checks in `backend/src/Crm.Application/Ai/`
- [ ] T185 [US10] Implement `TicketCreated` outbox handler applying AI classification suggestion (stores `AiSuggestedCategoryId/Priority`; applies category only when ticket arrived without one) in `backend/src/Crm.Application/Ai/Handlers/ClassifyOnTicketCreated.cs`
- [ ] T186 [US10] Implement chatbot in `ChatHub` flow: bot answers from public published KB via `AnswerChatAsync`, returns article links, hands off on `handOff`/`RequestHuman`/AI unavailable in `backend/src/Crm.Application/Messaging/Chat/ChatbotResponder.cs`
- [ ] T187 [US10] Implement `AiController` in `backend/src/Crm.Api/Controllers/V1/AiController.cs`
- [ ] T188 [US10] Staff UI — AI panel on ticket detail (summarize, suggest reply → inserts into composer for editing, suggested category/priority chips with accept/override, suggested articles), hidden when AI disabled; AI toggles in settings page in `frontend/projects/staff/src/app/features/tickets/ticket-detail/ai-panel.component.ts` and `frontend/projects/staff/src/app/features/admin/settings/`
- [ ] T189 [P] [US10] Chat widget — bot message rendering with article links and "talk to a human" button in `frontend/projects/chat-widget/src/app/`
- [ ] T190 [P] [US10] Component tests for AI panel (disabled/unavailable states) in `frontend/projects/staff/src/app/features/tickets/ticket-detail/ai-panel.component.spec.ts`

**Checkpoint**: US10 scenarios 1–5 pass.

---

## Phase 13: User Story 11 — Integrations with External Systems (P3)

**Goal**: API keys, public API access, signed webhooks with retries, read-only ERP data on customer profile.
**Independent Test**: API key creates customer + ticket; webhook receives signed `ticket.status_changed`; revoked key rejected.

### Tests for US11 ⚠️

- [ ] T191 [P] [US11] Integration tests for API key auth (`X-Api-Key` scheme, permission limits, revoked/expired → 401, `LastUsedAt`) using `/customers` and `/tickets` in `backend/tests/Crm.Api.IntegrationTests/Integrations/ApiKeyTests.cs`
- [ ] T192 [P] [US11] Integration tests for webhooks: subscription CRUD, delivery with `X-CRM-Signature` verified by WireMock receiver, retry schedule, permanent failure, payload excludes internal notes in `backend/tests/Crm.Api.IntegrationTests/Integrations/WebhookTests.cs`
- [ ] T193 [P] [US11] Adapter tests for generic REST `ErpConnector` (auth types, mapping, 10-min cache, timeout → 503) in `backend/tests/Crm.Infrastructure.Tests/Erp/RestErpConnectorTests.cs`

### Implementation for US11

- [ ] T194 [P] [US11] Create `ApiKey`, `WebhookSubscription`, `WebhookDelivery`, `ErpConnection` entities + configurations, migration `US11_Integrations` in `backend/src/Crm.Domain/Integrations/`
- [ ] T195 [US11] Implement `ApiKeyAuthenticationHandler` (hash lookup, permissions as claims, audit ApiKeyId) and register as second scheme in `backend/src/Crm.Api/Security/ApiKeyAuthenticationHandler.cs`
- [ ] T196 [P] [US11] Implement API key use cases `ListApiKeys`, `CreateApiKey` (returns plain key once), `RevokeApiKey` in `backend/src/Crm.Application/Integrations/ApiKeys/`
- [ ] T197 [P] [US11] Implement webhook use cases (CRUD, list deliveries) and outbox handlers mapping `TicketCreated`, `TicketUpdated`, `TicketStatusChanged`, `CustomerCreated` to `WebhookDelivery` rows in `backend/src/Crm.Application/Integrations/Webhooks/`
- [ ] T198 [US11] Implement `WebhookDispatcherJob` (HMAC-SHA256 over `{timestamp}.{body}`, headers per `contracts/webhook-events.md`, retries 1m/5m/30m/2h/12h) in `backend/src/Crm.Infrastructure/Webhooks/WebhookDispatcherJob.cs`
- [ ] T199 [P] [US11] Implement `IErpConnector` port + `RestErpConnector` (IHttpClientFactory, Polly timeout/retry, memory cache) and `GetCustomerErpInfo`, `GetErpConnection`, `UpdateErpConnection` use cases in `backend/src/Crm.Application/Integrations/Erp/` and `backend/src/Crm.Infrastructure/Erp/RestErpConnector.cs`
- [ ] T200 [US11] Implement `AdminApiKeysController`, `AdminWebhooksController`, `AdminErpController` and `GET /customers/{id}/erp` in `backend/src/Crm.Api/Controllers/V1/`
- [ ] T201 [US11] Staff UI — integrations pages (API keys with one-time key reveal, webhooks with delivery log, ERP connection form + test) in `frontend/projects/staff/src/app/features/admin/integrations/`
- [ ] T202 [P] [US11] Staff UI — read-only ERP panel on customer profile (orders, invoices, unavailable state) in `frontend/projects/staff/src/app/features/customers/customer-profile/erp-panel.component.ts`

**Checkpoint**: US11 scenarios 1–4 pass. All stories complete.

---

## Phase 14: Polish & Cross-Cutting Concerns

- [ ] T203 [P] Accessibility & RTL audit of all three apps (keyboard nav, contrast, `dir` switching, mixed ar/en text rendering) with fixes in `frontend/projects/**`
- [ ] T204 [P] Responsive audit at 360 px / 768 px / 1280 px for every staff and portal page; fix overflow in `frontend/projects/**`
- [ ] T205 [P] Complete Arabic and English translation files and backend `.resx`/notification templates; add a test failing on missing keys in `frontend/libs/shared/src/lib/i18n/translation-keys.spec.ts`
- [ ] T206 Performance: seed 100k customers / 500k tickets script `backend/tools/Crm.Seeder/` and verify SC-007 (search < 2 s) and list endpoints; add missing indexes via migration `Polish_Indexes`
- [ ] T207 [P] Load test (k6) for 200 staff + 1,000 portal/chat visitors in `e2e/load/crm-load.js`; record results in `specs/001-support-crm-mvp/perf-results.md`
- [ ] T208 [P] Security hardening: security headers (CSP incl. chat widget embedding rules), cookie flags, upload scanning hook, dependency audit (`dotnet list package --vulnerable`, `npm audit`), secrets scan in CI in `backend/src/Crm.Api/Program.cs` and `.github/workflows/ci.yml`
- [ ] T209 [P] Verify audit coverage (SC-010): integration test iterating all `[Audited]` entities' create/update/delete in `backend/tests/Crm.Api.IntegrationTests/Admin/AuditCoverageTests.cs`
- [ ] T210 [P] Production Dockerfiles for API and static apps + `deploy/docker-compose.prod.yml` + reverse-proxy sample config in `deploy/`
- [ ] T211 [P] Update `README.md` (overview, architecture diagram, run instructions) and admin guide `docs/admin-guide.md` (channel setup, WhatsApp approval, AI enablement)
- [ ] T212 Run full `specs/001-support-crm-mvp/quickstart.md` validation walkthrough and record results in `specs/001-support-crm-mvp/checklists/acceptance.md`

---

## Dependencies & Execution Order

### Phase dependencies

```text
Setup (1) ─► Foundational (2) ─► US3 (3) ─► US2 (4) ─► US1 (5) ══ MVP ══
                                                        │
                   ┌────────────────────────────────────┼─────────────────────┐
                   ▼                                    ▼                     ▼
               US7 (6) ─► US4 (7)                   US5 (8)               US8 (10)
                   │                                    │                     │
                   └──────────────► US6 (9) ◄───────────┘                     ▼
                                                                         US10 (12)
               US9 (11) needs US1 (+ US7 for SLA, US5 for satisfaction data)
               US11 (13) needs US1 + US2 only
All ─► Polish (14)
```

### Story dependencies

| Story | Depends on | Why |
|---|---|---|
| US3 | Foundation | Users, roles, departments are prerequisites for everyone |
| US2 | US3 | Customers scoped by branch; staff users exist |
| US1 | US2, US3 | Tickets belong to customers and departments |
| US7 | US1 | SLA applies to tickets |
| US4 | US1 (US7 for SLA risk) | Dashboard lists tickets; highlights SLA risk |
| US5 | US1, US2 | Portal tickets & auto-created customers |
| US6 | US1, US2 (US4 hub groups) | Inbound messages create tickets for customers |
| US8 | Foundation (US5 for portal pages) | KB is largely independent |
| US9 | US1 (+US7, US5 for data) | Reports aggregate ticket/SLA/feedback data |
| US10 | US1, US8 (US6 for chatbot) | AI works on tickets; chatbot needs KB + chat |
| US11 | US1, US2 | API/webhooks expose customers & tickets |

### Within each story

1. Tests (must fail) → 2. Domain entities & migration → 3. Application use cases → 4. Infrastructure adapters/jobs → 5. Controllers/hubs → 6. UI → 7. UI tests. Checkpoint before next story.

---

## Parallel Execution Examples

**Foundation** (after T021):
```text
T022 PagedResult · T023 Permissions · T024 Org entities · T025 Identity entities · T026 Common entities
T036 CorrelationId · T037 ExceptionHandler · T038 Resources
T041–T045 frontend shared lib pieces
```

**US3**: all tests T048–T053 together; then T055, T056, T057, T058, T059, T060 together; UI T063, T064, T065 together after T061.

**US2**: T067–T070 together; T073–T077 together after T072.

**US1**: T082–T086 together; T087 + T088 together; T091, T092, T093 together after T090.

**US6**: adapters T153 (email), T154 (WhatsApp), T155 (SMS) in parallel by different developers after T152.

**Across stories after MVP** (team of 3):
```text
Dev A: US7 → US4 → US9
Dev B: US5 → US8 → US10
Dev C: US6 (email → WhatsApp → SMS → chat) → US11
```

---

## Implementation Strategy

### MVP first (Phases 1–5)

1. Setup + Foundation → sign-in, audit, i18n shell.
2. US3 → organization configured.
3. US2 → customers.
4. US1 → ticket lifecycle. **STOP & VALIDATE** with quickstart rows US3/US2/US1, then demo/deploy.

### Incremental delivery

- Release 2 (P2): US7 → US4 → US5 → US6 (email first; WhatsApp once Meta approval arrives).
- Release 3 (P3): US8 → US9 → US10 → US11.
- Each release: run affected quickstart rows + full test suite; polish tasks T203–T212 before production go-live.

### Notes

- [P] = different files, no incomplete dependencies.
- Commit after each task or logical group; keep migrations one per story.
- Never skip the failing-test step; CI must be green before merging a story.
