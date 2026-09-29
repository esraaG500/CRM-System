# Quickstart: Customer Support CRM (developer setup & validation)

**Feature**: 001-support-crm-mvp

## Prerequisites

- .NET 8 runtime and a .NET 8 or 9 SDK (projects target `net8.0`)
- SQL Server LocalDB (installed with Visual Studio) **or** Docker Desktop
- Node.js 20+ LTS and npm; Chrome (for headless Angular tests)
- Later stories: Anthropic API key (AI), WhatsApp/SMS sandbox accounts, MailHog/Seq via Docker

## 1. Configure secrets (once, never committed)

```powershell
cd backend/src/Crm.Api
dotnet user-secrets set "Jwt:SigningKey" "<48 random bytes, base64>"
dotnet user-secrets set "Seed:AdminPassword" "<admin password, 10+ characters>"
dotnet user-secrets set "Seed:SamplePassword" "<password for the sample supervisor and agents>"
```

`appsettings.Development.json` points at LocalDB (`(localdb)\MSSQLLocalDB`, database `CrmDev`).
To use a Docker SQL Server instead, override `ConnectionStrings:Crm` with user-secrets.

## 2. Run the backend

```powershell
sqllocaldb start MSSQLLocalDB
dotnet run --project backend/src/Crm.Api --launch-profile http
# API:      http://localhost:5029/api/v1
# Swagger:  http://localhost:5029/swagger
# Health:   http://localhost:5029/health
```

In Development the API applies migrations and seeds on startup:

- Roles Administrator, Supervisor, Agent, Customer with their permissions
- Departments Customer Support, Billing, Technical Support; branch Riyadh; five ticket categories
- `admin@crm.local` (password from `Seed:AdminPassword`)
- Sample users (password from `Seed:SamplePassword`): `supervisor@crm.local` (all departments),
  `agent1@crm.local` (Customer Support), `agent2@crm.local` (Support + Billing),
  `agent3@crm.local` (Technical Support)

### Demo data (optional)

```powershell
dotnet run --project backend/src/Crm.Api -- --seed-demo    # add demo customers and tickets, then exit
dotnet run --project backend/src/Crm.Api -- --reset-demo   # remove demo records and seed them again
```

Creates 24 customers (Arabic and English, companies with contacts), 96 tickets over the last six weeks
in every status, with replies, internal notes, escalations and full history. Runs only in Development,
is safe to repeat (skips when demo data exists), and never touches records you entered yourself
(demo customers are marked with an ERP reference starting `DEMO-`). Set `Seed:DemoData` to `true` to
seed automatically on startup instead.

## 3. Run the staff app

```powershell
cd frontend
npm ci
npx ng serve staff     # http://localhost:4200, proxies /api to http://localhost:5029
```

The customer portal (`projects/portal`) and chat widget (`projects/chat-widget`) are added with US5 and US6.

## 4. Run tests

```powershell
dotnet test backend/Crm.sln                                  # unit, architecture, integration (real SQL Server via LocalDB)
$env:CRM_TEST_USE_DOCKER = "1"; dotnet test backend/Crm.sln  # the same, against a Testcontainers SQL Server
cd frontend; npx ng test staff --watch=false --browsers=ChromeHeadless
```

## 5. Validation walkthrough (maps to spec user stories)

Rows marked ✅ can be validated with the current build.

| # | Steps | Expected |
|---|---|---|
| ✅ US2 | As agent1, create company customer "Acme" with 2 contacts and a note | Profile shows contacts; activity shows the note with author and time |
| ✅ US1 | As supervisor, create a ticket for Acme (High), assign to agent1; as agent1 move Open → In progress, reply with "Send and resolve", then close | History lists every change with who and when; reference `TCK-000001` |
| ✅ US1 | As agent3 (Technical Support), open the Customer Support ticket URL | Not found: agents only see their departments' tickets |
| ✅ US4 | As agent1, open My desk | Assigned tickets by priority, tickets awaiting first reply, department queue with Take |
| ✅ Platform | Switch language between العربية and English; narrow the window to phone width | Full RTL/LTR switch, all labels translated, no horizontal scroll |
| US3 | Sign in as `admin@crm.local`; create department, branch and an agent | Agent can sign in; audit log shows 3 create entries |
| US2 | Upload a PDF to a customer; merge a duplicate | Attachment on timeline; duplicate's history moves to the kept customer |
| US7 | Create High SLA (response 1 h); create High ticket; wait for (or fake) 48 min | SLA warning notification; after 60 min breach + supervisor notified |
| US6 | Send email to MailHog-routed support mailbox; reply from ticket | Ticket created from email; reply appears in MailHog on the same thread |
| US5 | Open portal, register, submit ticket; agent resolves; rate 5 | Customer sees status change; feedback shown on ticket & agent report |
| US4 | Create reminder due in 1 min; mention a colleague in a note | Reminder + mention notifications arrive in real time |
| US8 | Publish one Public and one Internal article; search as portal customer | Customer sees only the Public article |
| US9 | Open Reports → SLA; filter by department; export | Totals match tickets; `.xlsx` downloads; export audited |
| US10 | With AI enabled: summarize ticket, suggest reply, create ticket (auto-category), chat with bot → "talk to a human" | Suggestions appear, nothing sent without approval; chat handed to agent with transcript |
| US11 | Create API key; `POST /api/v1/tickets` with `X-Api-Key`; subscribe webhook to https://webhook.site | Ticket created; signed `ticket.created` delivery received; revoked key → 401 |
