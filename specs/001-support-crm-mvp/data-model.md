# Data Model: Customer Support CRM (Simple Business Edition)

**Feature**: 001-support-crm-mvp | **Date**: 2026-09-29 | **Source**: [spec.md](spec.md) Key Entities

## Conventions (all entities unless stated)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key, sequential GUID (`NEWSEQUENTIALID` style) |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` (UTC) | Set by SaveChanges interceptor |
| `CreatedBy` / `UpdatedBy` | `Guid?` → User | Set by interceptor from current user / API key |
| `IsDeleted` | `bool` | Soft delete + global query filter (where noted) |
| `RowVersion` | `rowversion` | Optimistic concurrency (where noted) |

All text columns `nvarchar`; enums stored as `nvarchar` names for readability.
Changes to audited entities (★) produce `AuditLogEntry` rows via interceptor.

---

## Organization

### Department ★
| Field | Type | Rules |
|---|---|---|
| NameEn / NameAr | string(100) | Required, unique per language |
| IsActive | bool | |
| EscalationOwnerId | Guid? → User | Target for escalations |

### Branch ★
| Field | Type | Rules |
|---|---|---|
| NameEn / NameAr | string(100) | Required, unique |
| TimeZoneId | string(64) | Default `Asia/Riyadh` |
| IsActive | bool | |

### BusinessCalendar
Working hours per weekday (`DayOfWeek`, `Start`, `End`) + `Holiday` rows (`Date`, `NameEn/Ar`).
One default calendar; optional per branch.

---

## Identity & Security

### User ★ (extends Identity user)
| Field | Type | Rules |
|---|---|---|
| Email | string(256) | Required, unique, valid email |
| FullName | string(150) | Required |
| UserType | enum `Staff` \| `Customer` | Customer = portal account |
| PreferredLanguage | enum `ar` \| `en` | Default `ar` |
| BranchId | Guid? → Branch | Required for Staff |
| Status | enum `Active` \| `Deactivated` | Deactivation returns open tickets to queue |
| TwoFactorEnabled | bool | |
| CustomerId / ContactPersonId | Guid? | Set for Customer users |
| Departments | M:N → Department (`UserDepartment`) | Staff ≥ 1 |
| IsAvailable | bool | For auto-assignment / chat routing |

### Role ★ / Permission
- `Role`: `Name` (unique), `IsSystem` (Administrator, Supervisor, Agent, Customer cannot be deleted).
- `RolePermission`: `RoleId`, `Permission` (string constant, e.g., `tickets.assign`, `reports.view`,
  `customers.export`, `admin.users.manage`, `data.scope.all-departments`).
- User ↔ Role: M:N (Identity).

### RefreshToken
`UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt?`, `ReplacedByTokenId?`, `CreatedIp`.

### ApiKey ★
| Field | Type | Rules |
|---|---|---|
| Name | string(100) | Required |
| Prefix | string(8) | Shown to admins for identification |
| KeyHash | string | SHA-256; plain key shown once |
| Permissions | string list | Subset of API permissions |
| ExpiresAt / RevokedAt | DateTimeOffset? | Revoked keys rejected (US11-4) |
| LastUsedAt | DateTimeOffset? | |

### AuditLogEntry (append-only, no update/delete)
`Timestamp`, `UserId?`, `ApiKeyId?`, `Action` (`Create|Update|Delete|SignIn|SignInFailed|PermissionDenied|Export`),
`EntityType`, `EntityId?`, `Changes` (JSON before/after), `IpAddress`, `CorrelationId`.

---

## Customers

### Customer ★ (soft delete, RowVersion)
| Field | Type | Rules |
|---|---|---|
| ReferenceNumber | string(20) | Unique, `CUS-000001`, generated |
| Type | enum `Individual` \| `Company` | Required |
| Name | string(200) | Required |
| PrimaryEmail | string(256)? | Valid email; at least one of email/phone required |
| PrimaryPhone | string(20)? | E.164 format |
| AdditionalEmails / AdditionalPhones | child tables | For channel matching |
| Address | owned: Line1, City, Country | Optional |
| PreferredLanguage | `ar` \| `en` | Default from branch |
| BranchId | Guid? → Branch | |
| Tags | M:N → Tag | |
| ErpReference | string(64)? | Link to ERP record (FR-048) |
| NeedsReview | bool | Auto-created from unknown sender (US2-5) |
| MergedIntoCustomerId | Guid? | Set when merged; history re-pointed |

### ContactPerson ★
`CustomerId` (Company only), `Name`, `Email?`, `Phone?`, `JobTitle?`, `IsPrimary`.

### Note ★
`Body` (max 4,000), `CustomerId?` / `TicketId?` (exactly one), `AuthorId`.

### Attachment
`FileName`, `ContentType` (allow-listed), `SizeBytes` (≤ 10,485,760), `StorageKey`,
`CustomerId?` / `TicketId?` / `MessageId?` / `NoteId?`, `UploadedById?`.

### Tag
`Name` (unique, 50).

---

## Tickets

### Ticket ★ (soft delete, RowVersion)
| Field | Type | Rules |
|---|---|---|
| ReferenceNumber | string(20) | Unique, `TCK-000001` |
| Subject | string(250) | Required |
| Description | nvarchar(max) | Required |
| CustomerId | Guid → Customer | Required |
| ContactPersonId | Guid? → ContactPerson | Must belong to customer |
| Channel | enum `Email` \| `WhatsApp` \| `Sms` \| `LiveChat` \| `WebForm` \| `Portal` \| `Phone` \| `Api` | Required |
| CategoryId | Guid → Category | Required (AI may suggest) |
| Priority | enum `Low` \| `Medium` \| `High` \| `Urgent` | Default `Medium` |
| Status | enum (see state machine) | Default `New` |
| DepartmentId | Guid → Department | Required |
| BranchId | Guid? → Branch | |
| AssigneeId | Guid? → User | Staff only, active, member of department |
| EscalationLevel | int | 0..3 |
| FirstResponseDueAt / ResolutionDueAt | DateTimeOffset? | From SLA policy |
| FirstRespondedAt / ResolvedAt / ClosedAt | DateTimeOffset? | |
| SlaPausedAt | DateTimeOffset? | Set while `PendingCustomer` |
| SlaPausedTotal | TimeSpan | Accumulated pause |
| FirstResponseBreached / ResolutionBreached | bool | |
| AiSuggestedCategoryId / AiSuggestedPriority | nullable | Kept for accuracy reporting |

**Indexes**: `(DepartmentId, Status, Priority, CreatedAt)`, `(AssigneeId, Status)`,
`(CustomerId, CreatedAt)`, `(ResolutionDueAt) WHERE Status NOT IN (Resolved, Closed)`,
full-text on `Subject`, `Description`.

**State machine** (`TicketStatus`):

```text
New ──► Open ──► InProgress ──► Resolved ──► Closed
 │        │  ▲       │  ▲           │            │
 │        ▼  │       ▼  │           │            │ customer reply ≤ 7 days
 │     PendingCustomer ◄┘           │            ▼
 │        (SLA paused)              └──reopen──► Open
 └────────────────────────────────────────────► Closed (spam/duplicate, with reason)
```

- Assigning a `New` ticket moves it to `Open`.
- First outbound public reply sets `FirstRespondedAt`.
- `Resolved` triggers the satisfaction survey; auto-closes after 7 days without reply.
- `Closed` + customer reply within 7 days → `Open` (reopen); after 7 days → new linked ticket.
- Invalid transitions return 409 Conflict (Problem Details `ticket.invalid-transition`).

### Message
| Field | Type | Rules |
|---|---|---|
| TicketId | Guid → Ticket | Required |
| Direction | enum `Inbound` \| `Outbound` | |
| IsInternal | bool | Internal note — never exposed to Customer role or channels |
| Channel | enum (as Ticket) | Outbound defaults to ticket channel |
| Body | nvarchar(max) | Required, sanitized HTML/plain |
| SenderUserId / SenderCustomerId | Guid? | Exactly one (or system/bot) |
| IsFromBot | bool | Chatbot messages |
| ExternalMessageId | string(256)? | Provider id / email Message-ID; unique per channel (dedupe) |
| DeliveryStatus | enum `Pending` \| `Sent` \| `Delivered` \| `Read` \| `Failed` | Outbound only |
| DeliveryError | string? | |
| Mentions | M:N → User | Internal notes only (FR-019) |

### TicketHistoryEntry (append-only)
`TicketId`, `Timestamp`, `UserId?` (null = system/automation), `ChangeType`
(`Created|FieldChanged|Assigned|StatusChanged|Escalated|SlaBreached|Merged|Reopened`),
`Field?`, `OldValue?`, `NewValue?`, `Reason?`.

### Category ★ / Priority settings
- `Category`: `NameEn`, `NameAr`, `DepartmentId?` (default routing), `IsActive`, `SortOrder`.
- Priorities are a fixed enum; display names configurable in `SystemSetting`.

---

## Agent Productivity

### TaskItem
`Title` (200), `Notes?`, `DueAt`, `OwnerId` → User, `TicketId?`, `CustomerId?`,
`Status` (`Open|Done`), `ReminderSentAt?`.

### QuickReply ★
`Title` (100), `Body` (supports `{{customer.name}}`, `{{ticket.reference}}`, `{{agent.name}}`),
`Language` (`ar|en`), `Scope` (`Shared|Personal`), `OwnerId?` (Personal), `DepartmentId?`.

### Notification
`UserId`, `Type` (`Assigned|Mentioned|Reminder|SlaWarning|SlaBreached|Escalated|ChatWaiting`),
`Title`, `Body`, `Link`, `IsRead`, `EmailSentAt?`. Users may mute non-critical types
(`NotificationPreference`: `UserId`, `Type`, `InApp`, `Email`); SLA breach/escalation cannot be muted.

---

## SLA & Automation

### SlaPolicy ★
`Name`, `DepartmentId?` (null = default), `Priority`, `FirstResponseMinutes`,
`ResolutionMinutes`, `CalendarId`, `WarningThresholdPercent` (default 80), `IsActive`.
Unique `(DepartmentId, Priority)`.

### AssignmentRule ★
`DepartmentId`, `CategoryId?`, `Strategy` (`RoundRobin`), `OnlyAvailableAgents` (bool),
`LastAssignedUserId?` (round-robin cursor), `IsActive`, `Order`.

### EscalationRule ★
`Trigger` (`SlaWarning|SlaBreach`), `Target` (`FirstResponse|Resolution`), `DepartmentId?`,
`Priority?`, `NotifyUserIds` / `NotifyRole`, `ReassignToUserId?`, `RaisePriority` (bool), `IsActive`.

### OutboxMessage
`Type`, `Payload` (JSON), `OccurredAt`, `ProcessedAt?`, `Attempts`, `Error?`.

---

## Channels

### ChannelConfiguration ★
`Channel` (unique), `IsEnabled`, `Settings` (JSON; secrets referenced by key into secret store,
never stored in plain text), `DefaultDepartmentId`, `LastPolledAt?`.

### ChatSession
`VisitorName?`, `VisitorEmail?`, `CustomerId?`, `TicketId?`, `Status` (`Bot|Waiting|Active|Ended`),
`AssignedAgentId?`, `StartedAt`, `EndedAt?`, `SessionTokenHash`.

---

## Knowledge Base

### KnowledgeCategory
`NameEn`, `NameAr`, `ParentId?`, `SortOrder`.

### KnowledgeArticle ★ (soft delete, RowVersion)
| Field | Type | Rules |
|---|---|---|
| Type | enum `Faq` \| `HelpArticle` \| `Guide` | |
| Title | string(250) | Required |
| Body | nvarchar(max) | Required, sanitized HTML |
| Language | `ar` \| `en` | |
| TranslationGroupId | Guid | Links ar/en versions |
| CategoryId | Guid → KnowledgeCategory | |
| Visibility | enum `Public` \| `Internal` | |
| Status | enum `Draft` \| `Published` \| `Unpublished` | |
| PublishedAt | DateTimeOffset? | |
| ViewCount / HelpfulCount / NotHelpfulCount | int | |

Full-text on `Title`, `Body`. `ArticleVote`: `ArticleId`, `VoterKey` (user id or anonymous hash), unique per article.

---

## Portal & Feedback

### Feedback
`TicketId` (unique), `CustomerId`, `AgentId?`, `Rating` (1–5), `Comment?` (1,000), `SubmittedAt`.
Survey token: `FeedbackRequest` (`TicketId`, `TokenHash`, `ExpiresAt`) for email/SMS links.

### WebFormSubmissionLog
`IpHash`, `Email`, `SubmittedAt` — used for duplicate/rate protection (edge case).

---

## Reports

### DailyTicketStat (pre-aggregated, refreshed hourly)
`Date`, `DepartmentId`, `BranchId?`, `Channel`, `CategoryId`, `AgentId?`, `Created`, `Resolved`,
`Closed`, `FirstResponseMet`, `FirstResponseBreached`, `ResolutionMet`, `ResolutionBreached`,
`SumFirstResponseMinutes`, `SumResolutionMinutes`, `FeedbackCount`, `FeedbackSum`.

---

## Integrations

### WebhookSubscription ★
`Name`, `Url` (HTTPS only), `Events` (list: `ticket.created`, `ticket.updated`,
`ticket.status_changed`, `customer.created`), `SecretHash`/secret ref, `IsActive`.

### WebhookDelivery
`SubscriptionId`, `EventId`, `EventType`, `Payload`, `Attempt`, `NextAttemptAt?`,
`ResponseStatus?`, `DeliveredAt?`, `FailedPermanentlyAt?`.

### ErpConnection (singleton setting) ★
`IsEnabled`, `BaseUrl`, `AuthType`, credential ref, `CustomerEndpoint`, `OrdersEndpoint`, `CacheMinutes` (default 10).

---

## Settings

### SystemSetting ★ (key/value)
Keys include: `org.name`, `org.timezone`, `branding.logoAttachmentId`, `branding.primaryColor`,
`branding.systemNameEn/Ar`, `branding.emailFooterEn/Ar`, `tickets.reopenWindowDays` (7),
`tickets.autoCloseDays` (7), `ai.enabled`, `ai.features.{summary,reply,classify,suggestArticles,chatbot}`,
`ai.model`, `attachments.maxBytes`, `attachments.allowedTypes`.

---

## Relationships (summary)

```text
Branch 1─* User(Staff)          Department *─* User(Staff)
Customer 1─* ContactPerson      Customer 1─* Ticket 1─* Message 1─* Attachment
Ticket 1─* TicketHistoryEntry   Ticket 0..1─1 Feedback      Ticket *─1 Category
Ticket *─1 Department           Ticket *─0..1 User(Assignee)
Customer/Ticket 1─* Note        User 1─* TaskItem / Notification
SlaPolicy *─1 BusinessCalendar  KnowledgeArticle *─1 KnowledgeCategory
ChatSession 0..1─1 Ticket       WebhookSubscription 1─* WebhookDelivery
```

## Validation rules traced to requirements

| Rule | Source |
|---|---|
| Attachment ≤ 10 MB, allow-listed types | FR-004, Edge cases |
| Customer needs email or phone | FR-001 |
| Ticket assignee must be active staff in ticket's department (unless `assign.any-department`) | FR-008 |
| Escalation requires reason | FR-009 |
| Internal messages excluded from Customer role & channel sends | FR-019 |
| SLA policy unique per department + priority; thresholds 1–99% | FR-020, FR-023 |
| Rating 1–5, one feedback per ticket | FR-035 |
| Webhook URL must be HTTPS | FR-047, Principle V |
| Article visible to customers only if `Public` and `Published` | FR-026, FR-034 |
| Stale `RowVersion` on update → 409 Conflict with current version | Edge cases |
