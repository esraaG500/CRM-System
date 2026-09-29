# Quickstart: Customer Support CRM (developer setup & validation)

**Feature**: 001-support-crm-mvp

## Prerequisites

- .NET 8 SDK (`dotnet --version` → 8.x)
- Node.js 20+ LTS and npm; Angular CLI (`npm i -g @angular/cli`)
- Docker Desktop (for SQL Server, Seq, MailHog and Testcontainers)
- Optional: Anthropic API key (AI features), WhatsApp/SMS sandbox accounts

## 1. Start infrastructure

```powershell
docker compose -f deploy/docker-compose.dev.yml up -d
# sqlserver  : localhost,1433  (SQL Server 2022 with Full-Text Search)
# seq        : http://localhost:5341   (structured logs)
# mailhog    : smtp localhost:1025, UI http://localhost:8025
```

## 2. Configure secrets (never commit)

```powershell
cd backend/src/Crm.Api
dotnet user-secrets set "ConnectionStrings:Crm" "Server=localhost,1433;Database=Crm;User Id=sa;Password=<dev-password>;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" "<32+ random bytes, base64>"
dotnet user-secrets set "Ai:ApiKey" "<anthropic key>"          # optional
dotnet user-secrets set "Captcha:SecretKey" "<turnstile test secret>"
```

## 3. Database

```powershell
dotnet tool restore
dotnet ef database update --project backend/src/Crm.Infrastructure --startup-project backend/src/Crm.Api
# Seeds: roles (Administrator, Supervisor, Agent, Customer), permissions, default calendar
# (Sun–Thu 08:00–17:00 Asia/Riyadh), priorities, SLA defaults, admin user admin@crm.local
```

## 4. Run backend

```powershell
dotnet run --project backend/src/Crm.Api
# API:        https://localhost:5001/api/v1
# Swagger:    https://localhost:5001/swagger
# Hangfire:   https://localhost:5001/jobs   (Administrator only)
# Health:     https://localhost:5001/health
```

## 5. Run frontend

```powershell
cd frontend
npm ci
npm run generate:api        # regenerates libs/shared/api from specs/001-support-crm-mvp/contracts/openapi.yaml
npx ng serve staff          # http://localhost:4200  (agents / supervisors / admins)
npx ng serve portal --port 4300   # http://localhost:4300  (customer portal + web form)
npx ng build chat-widget    # dist/chat-widget/crm-chat.js  (embed on any site)
```

## 6. Run tests

```powershell
dotnet test backend/Crm.sln                     # unit + integration (Testcontainers SQL Server)
cd frontend; npx ng test --watch=false          # Angular unit tests
npx playwright test                             # E2E journeys (needs API + apps running)
```

## 7. Validation walkthrough (maps to spec user stories)

| # | Steps | Expected |
|---|---|---|
| US3 | Sign in as `admin@crm.local`; create department "Support", branch "Riyadh", agent `agent1@crm.local` | Agent can sign in; audit log shows 3 create entries |
| US2 | As agent1, create company customer "Acme" with 2 contacts, add note + PDF | Timeline shows note & attachment with author/date |
| US1 | Create ticket for Acme (High), assign to agent1, move New→Open→InProgress→Resolved→Closed | History lists every change; reference `TCK-000001` |
| US7 | Create High SLA (response 1 h); create High ticket; wait for (or fake) 48 min | SLA warning notification; after 60 min breach + supervisor notified |
| US6 | Send email to MailHog-routed support mailbox; reply from ticket | Ticket created from email; reply appears in MailHog on the same thread |
| US5 | Open portal, register, submit ticket; agent resolves; rate 5 | Customer sees status change; feedback shown on ticket & agent report |
| US4 | Open agent dashboard; create reminder due in 1 min; mention a colleague in a note | Reminder + mention notifications arrive in real time |
| US8 | Publish one Public and one Internal article; search as portal customer | Customer sees only the Public article |
| US9 | Open Reports → SLA; filter by department; export | Totals match tickets; `.xlsx` downloads; export audited |
| US10 | With AI enabled: summarize ticket, suggest reply, create ticket (auto-category), chat with bot → "talk to a human" | Suggestions appear, nothing sent without approval; chat handed to agent with transcript |
| US11 | Create API key; `POST /api/v1/tickets` with `X-Api-Key`; subscribe webhook to https://webhook.site | Ticket created; signed `ticket.created` delivery received; revoked key → 401 |
| Platform | Switch language to Arabic; open on mobile width | Full RTL layout, all labels Arabic, no horizontal scroll |
