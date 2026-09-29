# Feature Specification: Customer Support CRM (Simple Business Edition)

**Feature Branch**: `001-support-crm-mvp`
**Created**: 2026-09-29
**Status**: Draft
**Input**: User description: "See the PDF (Customer Support CRM – Core Features) and make all these features in a simple business version." Source modules: Customer Management, Ticket Management, Communication Channels, Agent Dashboard, SLA & Automation, Knowledge Base, AI Features, Customer Portal, Reports & Management, Security & Administration, Integrations, Platform.

**Scope intent**: Deliver every module from the source document at a *simple, essential* level
suitable for a small-to-medium business support team. Advanced variants (complex workflow
builders, custom report designers, multi-company tenancy) are out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create, Assign and Resolve Tickets (Priority: P1)

A support agent logs a customer request as a ticket, sets its category and priority, assigns it
to themselves or a colleague, updates its status while working on it, escalates it if needed,
and closes it when resolved. Every change is recorded in the ticket's history.

**Why this priority**: Tickets are the core of a support CRM; without them no other module has value.

**Independent Test**: An agent creates a ticket, assigns it, moves it through statuses to Closed,
and the history shows every change with who and when.

**Acceptance Scenarios**:

1. **Given** a logged-in agent, **When** they create a ticket with subject, description, customer,
   category and priority, **Then** the ticket is saved with a unique reference number and status "New".
2. **Given** an open ticket, **When** a supervisor assigns it to an agent, **Then** the agent sees it
   in their assigned list and the assignment appears in the ticket history.
3. **Given** a ticket in progress, **When** the agent changes status to "Pending Customer",
   "Resolved" or "Closed", **Then** the new status and timestamp are recorded in history.
4. **Given** an open ticket, **When** the agent escalates it, **Then** its escalation level increases,
   it is reassigned to the escalation owner, and the escalation is recorded with a reason.
5. **Given** a closed ticket, **When** the customer replies within 7 days, **Then** the ticket reopens.

---

### User Story 2 - Manage Customer Profiles (Priority: P1)

An agent looks up a customer, sees their profile and contact details, reviews all past
interactions and tickets, and adds notes or attachments.

**Why this priority**: Agents need customer context to resolve tickets; tickets are linked to customers.

**Independent Test**: Create a customer, add contacts, a note and an attachment, link a ticket,
and verify it all appears on the customer's profile timeline.

**Acceptance Scenarios**:

1. **Given** an agent, **When** they create a customer with name, type (individual/company), email
   and phone, **Then** the profile is saved and searchable by name, email, phone or reference.
2. **Given** a company customer, **When** the agent adds multiple contact persons, **Then** each contact
   can be selected when creating tickets.
3. **Given** a customer profile, **When** the agent opens it, **Then** they see an interaction history
   of all tickets, messages, notes and feedback in date order.
4. **Given** a customer profile, **When** the agent adds a note or uploads an attachment,
   **Then** it appears on the timeline with author and date.
5. **Given** a new ticket or message from an email or phone number that matches no customer,
   **Then** a new customer profile is created automatically and flagged for review.

---

### User Story 3 - Administer Users, Roles, Departments and Branches (Priority: P1)

An administrator sets up the organization: departments, branches, users, roles and permissions,
ticket categories, priorities, working hours, languages and branding. All sensitive actions are
recorded in an audit log.

**Why this priority**: Secure access and basic configuration are required before agents can work.

**Independent Test**: An admin creates a department, a branch, a user with the Agent role, and
verifies the agent can only perform permitted actions; the audit log shows these changes.

**Acceptance Scenarios**:

1. **Given** an administrator, **When** they create a user and assign a role (Administrator,
   Supervisor, Agent), a department and a branch, **Then** the user can sign in with only that
   role's permissions.
2. **Given** an Agent role user, **When** they try an admin-only action (e.g., delete a user),
   **Then** the action is denied and the attempt is logged.
3. **Given** an administrator, **When** they create or change categories, priorities, working hours
   or branding (logo, colors, system name), **Then** the change takes effect for all users.
4. **Given** any create/update/delete on users, roles, settings or customer data, **When** an
   administrator views the audit log, **Then** they see who did what, when, and the before/after values.
5. **Given** an agent assigned to one department, **When** they view tickets, **Then** by default they see
   only their department's tickets unless granted wider visibility.

---

### User Story 4 - Agent Dashboard and Team Collaboration (Priority: P2)

On signing in, an agent sees one dashboard with their assigned tickets, overdue items, tasks and
reminders, and can reply quickly using saved quick replies and collaborate with colleagues through
internal notes and mentions.

**Why this priority**: Improves agent productivity once tickets and customers exist.

**Independent Test**: With tickets assigned, an agent opens the dashboard, sees counts by status
and SLA risk, creates a reminder, uses a quick reply, and mentions a colleague who gets notified.

**Acceptance Scenarios**:

1. **Given** an agent with assigned tickets, **When** they open the dashboard, **Then** they see their
   tickets grouped by status, with SLA-at-risk and overdue tickets highlighted first.
2. **Given** a ticket open on screen, **When** the agent views it, **Then** the customer's key information
   and recent history are shown alongside it.
3. **Given** an agent, **When** they create a task or reminder with a due date (optionally linked to a
   ticket), **Then** they are notified at the due time.
4. **Given** saved quick replies, **When** the agent inserts one, **Then** placeholders such as customer
   name and ticket number are filled in automatically and the text can be edited before sending.
5. **Given** a ticket, **When** an agent adds an internal note mentioning a colleague, **Then** the
   colleague is notified and the note is never visible to the customer.

---

### User Story 5 - Customer Self-Service Portal and Web Forms (Priority: P2)

A customer submits a request through a web form or the customer portal, tracks its status,
views their past requests, reads FAQs, and rates the service after resolution.

**Why this priority**: Reduces agent workload and gives customers visibility.

**Independent Test**: A customer registers on the portal, submits a ticket, sees it update when
an agent replies, views history, and submits a satisfaction rating after it is resolved.

**Acceptance Scenarios**:

1. **Given** a visitor, **When** they submit the public web form with name, email, category and
   description, **Then** a ticket is created and they receive a confirmation with the reference number.
2. **Given** a registered customer, **When** they sign in to the portal, **Then** they see all their
   tickets with current status and can reply or add attachments to open tickets.
3. **Given** a customer typing a new request, **When** the subject matches published FAQs or articles,
   **Then** matching articles are suggested before the ticket is submitted.
4. **Given** a ticket marked Resolved, **When** the customer is asked for feedback, **Then** they can give
   a 1–5 rating and an optional comment, which is stored against the ticket and agent.

---

### User Story 6 - Multi-Channel Communication (Priority: P2)

Customers contact the business by email, WhatsApp, SMS, live chat on the website, or web forms.
Every incoming message becomes or updates a ticket, and agents reply from the CRM through the
same channel the customer used.

**Why this priority**: Brings all customer conversations into one place; depends on tickets and customers.

**Independent Test**: Send a message on each enabled channel and verify each creates a ticket
linked to the right customer; an agent's reply reaches the customer on the same channel.

**Acceptance Scenarios**:

1. **Given** a configured support email address, **When** a customer sends an email, **Then** a ticket
   is created (or the existing ticket is updated if the email replies to a ticket) with attachments.
2. **Given** configured WhatsApp and SMS numbers, **When** a customer sends a message, **Then** it is added
   to the customer's open ticket on that channel, or creates a new ticket if none is open.
3. **Given** the live chat widget on the business website, **When** a visitor starts a chat, **Then** an
   available agent receives it in real time and the transcript is saved to a ticket.
4. **Given** a ticket that originated on a channel, **When** the agent replies, **Then** the reply is sent
   on that channel and delivery failures are shown on the ticket.
5. **Given** an administrator, **When** they enable or disable a channel, **Then** the change applies
   without affecting other channels.

---

### User Story 7 - SLA Targets, Auto-Assignment, Escalation and Alerts (Priority: P2)

A supervisor defines response and resolution targets per priority, rules for automatically
assigning new tickets, and escalation rules. The system tracks deadlines, warns agents before a
breach, and escalates when a breach happens.

**Why this priority**: Ensures service commitments are met without manual monitoring.

**Independent Test**: Configure an SLA for "High" priority, create a High ticket, let the warning
and breach thresholds pass, and verify the notification, escalation and SLA breach record.

**Acceptance Scenarios**:

1. **Given** SLA targets per priority (e.g., High: first response 1 hour, resolution 8 hours) and
   working hours, **When** a ticket is created, **Then** its response and resolution due times are
   calculated using business hours only.
2. **Given** an auto-assignment rule for a department (round-robin among available agents),
   **When** a new ticket arrives for that department, **Then** it is assigned automatically.
3. **Given** a ticket at 80% of its SLA time with no response, **When** the threshold is reached,
   **Then** the assigned agent is alerted in-app and by email.
4. **Given** a ticket that breaches its SLA, **When** the breach occurs, **Then** the escalation rule
   notifies the supervisor, optionally reassigns the ticket, and records the breach.
5. **Given** a ticket in "Pending Customer" status, **When** time passes, **Then** the SLA clock is paused.

---

### User Story 8 - Knowledge Base (Priority: P3)

Staff write and publish FAQs, help articles and solution guides, organized by category, in Arabic
and English. Agents and customers search the knowledge base; articles can be internal-only or public.

**Why this priority**: Enables self-service and consistent answers; valuable once core support works.

**Independent Test**: Publish a public article and an internal article; verify customers find only
the public one via portal search while agents find both.

**Acceptance Scenarios**:

1. **Given** a supervisor, **When** they create an article with title, body, category, language and
   visibility (Public/Internal), **Then** it can be saved as Draft or Published.
2. **Given** published articles, **When** a user searches by keyword, **Then** matching articles are
   returned ranked by relevance, limited to what that user is allowed to see.
3. **Given** an agent working on a ticket, **When** they insert an article link into a reply,
   **Then** the customer receives a link they can open.
4. **Given** a customer reading an article, **When** they mark it "Helpful" or "Not helpful",
   **Then** the vote is counted for that article.

---

### User Story 9 - Reports and Management Dashboards (Priority: P3)

Managers view dashboards and reports on ticket volumes, SLA performance, agent performance and
customer satisfaction, filtered by date range, department, branch, channel and agent, and export them.

**Why this priority**: Supports management decisions; requires operational data from earlier stories.

**Independent Test**: With sample tickets, open each report, apply filters, verify totals match the
underlying tickets, and export to a spreadsheet file.

**Acceptance Scenarios**:

1. **Given** ticket data, **When** a manager opens the ticket report, **Then** they see counts by status,
   category, priority, channel and period.
2. **Given** SLA data, **When** they open the SLA report, **Then** they see the percentage of tickets meeting
   response and resolution targets and a list of breached tickets.
3. **Given** agent activity, **When** they open agent performance, **Then** they see tickets handled, average
   first response time, average resolution time and average satisfaction rating per agent.
4. **Given** any report, **When** the manager exports it, **Then** a spreadsheet file with the filtered data downloads.
5. **Given** a supervisor limited to one branch, **When** they view reports, **Then** only that branch's data is shown.

---

### User Story 10 - AI Assistance (Priority: P3)

The system helps agents with AI: summarizing long tickets, suggesting replies, suggesting a
category and priority for new tickets, and recommending knowledge-base solutions. An AI chatbot
answers common customer questions in the portal and live chat, and hands over to a human agent when needed.

**Why this priority**: Productivity gains on top of a working support process; optional per business.

**Independent Test**: Enable AI, open a long ticket and request a summary, request a suggested reply,
create a ticket and see a suggested category, and chat with the bot until it hands over to an agent.

**Acceptance Scenarios**:

1. **Given** a ticket with several messages, **When** the agent requests a summary, **Then** a short summary
   of the issue, actions taken and current state is shown within a few seconds.
2. **Given** an open ticket, **When** the agent requests a suggested reply, **Then** a draft reply in the
   customer's language is inserted for the agent to edit; nothing is sent without agent approval.
3. **Given** a new incoming ticket, **When** it is created, **Then** a suggested category and priority are
   applied or shown, and the agent can override them.
4. **Given** the chatbot in the portal or live chat, **When** it cannot answer or the customer asks for a
   human, **Then** the conversation is transferred to an agent with the full transcript.
5. **Given** an administrator, **When** they disable AI features, **Then** all AI functions are hidden and
   no customer data is sent to the AI service.

---

### User Story 11 - Integrations with External Systems (Priority: P3)

The business connects the CRM to other systems: an ERP for customer and order information, and
other systems through a documented API and event notifications.

**Why this priority**: Extends value into the wider business; core CRM works without it.

**Independent Test**: Using an API key, an external system creates a customer and a ticket through the
API, and receives a notification when the ticket status changes.

**Acceptance Scenarios**:

1. **Given** an administrator, **When** they create an API key with specific permissions, **Then** external
   systems can use it to read and create customers and tickets within those permissions.
2. **Given** an event subscription (e.g., ticket created, status changed), **When** the event occurs,
   **Then** the subscribed external system is notified, with retries if delivery fails.
3. **Given** an ERP connection, **When** an agent views a customer linked to the ERP, **Then** the customer's
   ERP reference and recent orders/invoices are shown read-only.
4. **Given** an API key, **When** an administrator revokes it, **Then** further requests with it are rejected.

---

### Edge Cases

- A customer emails from a new address or messages from a new phone number: a new profile is created
  and flagged as a possible duplicate; agents can merge duplicate customers, keeping all history.
- Two agents edit the same ticket at once: the second save is warned that the ticket changed and
  shown the latest version, with no silent overwrite.
- An attachment is too large (over 10 MB) or of a blocked file type: it is rejected with a clear message.
- An outbound message fails on a channel (e.g., invalid WhatsApp number): the failure is shown on the
  ticket and the agent can retry or pick another channel.
- No agent is available for auto-assignment: the ticket stays unassigned in the department queue and
  the supervisor is alerted.
- A ticket's priority changes after creation: SLA due times are recalculated from the original creation time.
- An agent is deactivated while holding open tickets: their tickets return to the department queue.
- The AI service is unavailable: AI buttons show a friendly "temporarily unavailable" message and all
  non-AI functions keep working.
- A customer submits the web form repeatedly within a short time: duplicate submissions are blocked
  and bot protection is applied.
- Arabic and English text mixed in one message: content displays correctly in both directions.

## Requirements *(mandatory)*

### Functional Requirements

**Customer Management**

- **FR-001**: System MUST allow creating, viewing, editing and deactivating customer profiles
  (individual or company) with name, email(s), phone(s), address, preferred language, branch and tags.
- **FR-002**: System MUST allow multiple contact persons per company customer.
- **FR-003**: System MUST show a chronological interaction history per customer, including tickets,
  messages on all channels, notes, attachments and feedback.
- **FR-004**: Users MUST be able to add notes and attach files (up to 10 MB each) to customers and tickets.
- **FR-005**: System MUST support searching customers by name, email, phone and reference number,
  and merging duplicate customers without losing history.

**Ticket Management**

- **FR-006**: System MUST allow creating tickets with subject, description, customer, channel, category,
  priority (Low, Medium, High, Urgent), department and attachments, and give each a unique reference number.
- **FR-007**: System MUST support ticket statuses New, Open, In Progress, Pending Customer, Resolved and
  Closed, and reopen a closed ticket if the customer replies within 7 days.
- **FR-008**: Supervisors MUST be able to assign and reassign tickets to agents or departments;
  agents MUST be able to take unassigned tickets from their department queue.
- **FR-009**: Users MUST be able to escalate a ticket manually with a reason; escalation increases the
  escalation level and reassigns it to the configured escalation owner.
- **FR-010**: System MUST record a complete, unchangeable history of every ticket change (field, old value,
  new value, user, time).
- **FR-011**: System MUST allow filtering and searching tickets by status, priority, category, agent,
  department, branch, channel, customer and date.

**Communication Channels**

- **FR-012**: System MUST receive customer messages from email, WhatsApp, SMS, website live chat and web
  forms, and convert each into a new ticket or add it to the related open ticket.
- **FR-013**: Agents MUST be able to reply from the ticket, with the reply delivered on the channel the
  customer used, and delivery status shown.
- **FR-014**: Administrators MUST be able to enable, disable and configure each channel independently.
- **FR-015**: Live chat MUST deliver messages between visitor and agent in real time and save the
  transcript to the ticket.

**Agent Dashboard & Collaboration**

- **FR-016**: System MUST provide each agent a dashboard with assigned tickets by status, SLA-at-risk and
  overdue tickets, and their tasks and reminders.
- **FR-017**: Users MUST be able to create tasks and reminders with a due date, optionally linked to a
  ticket or customer, and get notified when they are due.
- **FR-018**: System MUST provide shared and personal quick replies with placeholders (customer name,
  ticket number, agent name), in Arabic and English.
- **FR-019**: Users MUST be able to add internal notes to tickets, mention colleagues (who are notified),
  and these notes MUST never be visible to customers.

**SLA & Automation**

- **FR-020**: Supervisors MUST be able to define first-response and resolution targets per priority,
  optionally per department, using configured working hours and holidays.
- **FR-021**: System MUST calculate and display SLA due times on each ticket, pause the SLA clock in
  "Pending Customer" status, and record breaches.
- **FR-022**: System MUST support automatic assignment of new tickets by department and category using
  round-robin among active agents, or leave them in the queue if no rule applies.
- **FR-023**: System MUST support escalation rules triggered by SLA warning (configurable, default 80%)
  and breach, which notify specified users and can reassign the ticket or raise its priority.
- **FR-024**: System MUST send in-app and email notifications for assignment, mentions, reminders,
  SLA warnings, breaches and escalations; users MAY turn off non-critical notification types.

**Knowledge Base**

- **FR-025**: Authorized users MUST be able to create, edit, publish and unpublish FAQs, help articles
  and solution guides with category, language (Arabic/English) and visibility (Public/Internal).
- **FR-026**: System MUST provide keyword search across the knowledge base, returning only articles the
  searcher is allowed to see.
- **FR-027**: System MUST record article views and helpful/not-helpful votes.

**AI Features**

- **FR-028**: System MUST provide, on request, an AI summary of a ticket's conversation.
- **FR-029**: System MUST provide AI-suggested replies that agents review and edit before sending;
  the system MUST NOT send AI-generated replies to customers without agent approval (except via the chatbot).
- **FR-030**: System MUST suggest a category and priority for new tickets and recommend relevant
  knowledge-base articles; agents can accept or override the suggestions.
- **FR-031**: System MUST provide an AI chatbot in the portal and live chat that answers from published
  public knowledge-base content and hands over to a human agent on request or when it cannot answer.
- **FR-032**: Administrators MUST be able to turn AI features on or off as a whole and per feature.

**Customer Portal**

- **FR-033**: Customers MUST be able to register and sign in to a portal, submit tickets with attachments,
  track status, reply to their tickets, and view their ticket history.
- **FR-034**: The portal MUST show public FAQs and articles and suggest relevant ones while a customer
  writes a new request.
- **FR-035**: System MUST ask customers for a satisfaction rating (1–5) and optional comment when a ticket
  is resolved, and store it against the ticket and agent.
- **FR-036**: Customers MUST only be able to see their own tickets (and, for company contacts with
  permission, their company's tickets).

**Reports & Management**

- **FR-037**: System MUST provide reports for ticket volume, SLA compliance, agent performance and customer
  satisfaction, filterable by date range, department, branch, channel, category and agent.
- **FR-038**: System MUST provide a management dashboard with key figures: open tickets, new today,
  SLA compliance %, average first response time, average resolution time, and average satisfaction.
- **FR-039**: Users MUST be able to export any report to a spreadsheet file.
- **FR-040**: Report data MUST respect the viewer's department and branch access.

**Security & Administration**

- **FR-041**: System MUST provide user management (create, edit, deactivate, reset password) and
  require every staff user to sign in.
- **FR-042**: System MUST provide roles (default: Administrator, Supervisor, Agent, and Customer for the
  portal) with configurable permissions per role, and enforce them on every action.
- **FR-043**: System MUST record an audit log of sign-ins, failed sign-ins, permission denials, and all
  create/update/delete actions on users, roles, settings, customers and tickets; the log MUST be read-only.
- **FR-044**: Administrators MUST be able to configure categories, priorities, statuses' display names,
  working hours, holidays, channels, notification templates and SLA policies.
- **FR-045**: System MUST protect customer personal data: only authorized roles can view or export it,
  and exports are recorded in the audit log.

**Integrations**

- **FR-046**: System MUST provide a documented API allowing authorized external systems to manage
  customers and tickets, authenticated with revocable API keys that have limited permissions.
- **FR-047**: System MUST send event notifications (ticket created, updated, status changed, customer
  created) to subscribed external systems, retrying failed deliveries.
- **FR-048**: System MUST support linking a customer to an ERP record and displaying read-only ERP
  information (customer reference, recent orders/invoices) on the profile.

**Platform**

- **FR-049**: The whole system (staff app, portal, web forms, chat widget, notifications, knowledge base)
  MUST be available in Arabic (right-to-left) and English, with each user choosing their language.
- **FR-050**: All screens MUST be usable on desktop, tablet and mobile browsers.
- **FR-051**: System MUST support multiple departments and branches; tickets, customers, agents, queues,
  SLA policies and reports can be scoped by department and branch.
- **FR-052**: Administrators MUST be able to apply custom branding (logo, primary colors, system name,
  email footer) to the staff app, portal, chat widget and outgoing messages.

### Key Entities

- **Customer**: A person or company receiving support; name, type, contacts, language, branch, tags,
  optional ERP reference. Has many tickets, notes, attachments and feedback.
- **Contact Person**: An individual belonging to a company customer; name, email, phone, role.
- **Ticket**: A support request; reference number, subject, description, channel, category, priority,
  status, department, branch, assigned agent, escalation level, SLA due times. Belongs to a customer.
- **Message**: A single communication on a ticket; direction (in/out), channel, content, attachments,
  delivery status, sender. Internal notes are messages marked internal.
- **Ticket History Entry**: An unchangeable record of one change on a ticket.
- **Note / Attachment**: Free text or a file linked to a customer or ticket, with author and date.
- **Task / Reminder**: A to-do for a user with due date, optionally linked to a ticket or customer.
- **Quick Reply**: Reusable reply text with placeholders; shared or personal; language.
- **Category / Priority**: Configurable classification values for tickets.
- **SLA Policy**: Response and resolution targets per priority (and optionally department), with working hours.
- **Assignment Rule / Escalation Rule**: Automation settings that assign, notify, reassign or reprioritize tickets.
- **Notification**: A message to a user about an event, in-app and/or email, with read status.
- **Knowledge Article**: FAQ, help article or guide; title, body, category, language, visibility, status, votes.
- **Feedback**: A customer's satisfaction rating and comment for a resolved ticket.
- **User**: A staff member or portal customer account; role, department(s), branch, language, status.
- **Role / Permission**: Named sets of allowed actions.
- **Department / Branch**: Organizational units used for routing, access and reporting.
- **Channel Configuration**: Settings for each communication channel and its enabled state.
- **Audit Log Entry**: Who did what, when, on which record, with before/after values.
- **API Key / Event Subscription**: Credentials and notification targets for external systems.
- **Branding Settings**: Logo, colors, system name and email footer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An agent can create and assign a complete ticket in under 1 minute.
- **SC-002**: An agent can find any customer and see their full interaction history in under 10 seconds.
- **SC-003**: 100% of messages received on enabled channels appear as a ticket or ticket update within 1 minute.
- **SC-004**: SLA warning notifications are delivered before 100% of breaches for tickets whose SLA was configured.
- **SC-005**: At least 90% of tickets meet their first-response target within the first 3 months of use.
- **SC-006**: At least 20% of customer questions are answered through the knowledge base or chatbot
  without an agent within 6 months.
- **SC-007**: Search results (customers, tickets, knowledge base) appear in under 2 seconds for a business
  with up to 100,000 customers and 500,000 tickets.
- **SC-008**: The system supports at least 200 staff users and 1,000 simultaneous portal/chat visitors
  without noticeable slowdown.
- **SC-009**: 100% of screens are fully usable in both Arabic (right-to-left) and English, on mobile and desktop.
- **SC-010**: 100% of changes to users, permissions, settings, customers and tickets appear in the audit log.
- **SC-011**: New agents can handle their first ticket end to end without training beyond a 30-minute walkthrough.
- **SC-012**: Average customer satisfaction rating of 4 out of 5 or higher is measurable per agent and department.

## Assumptions

- **Target business**: a single organization (one company), up to about 200 staff users, with multiple
  departments and branches. Serving multiple separate companies from one installation is out of scope.
- **"Simple" scope**: automation uses fixed rule types (round-robin assignment, SLA-based escalation)
  rather than a visual workflow builder; reports are predefined with filters rather than a custom
  report designer.
- **Default roles**: Administrator, Supervisor, Agent and Customer; admins can adjust permissions per role.
- **Channel providers**: the business will provide its own accounts for email, WhatsApp Business, SMS
  and AI services; the CRM connects to them through configurable settings.
- **ERP integration**: limited to linking customers and read-only display of ERP data; writing back to
  the ERP is out of scope for this version.
- **Staff sign-in**: email and password with optional two-factor authentication; single sign-on with
  corporate directories may be added later.
- **Data retention**: tickets, customer data and audit logs are kept indefinitely unless an administrator
  archives them; attachments are limited to 10 MB each.
- **Working hours and SLA** are calculated in the organization's local time zone
  (default: Arabia Standard Time, UTC+3).
- **Satisfaction surveys** are sent once per resolved ticket.
- **Native mobile apps** are out of scope; mobile use is through responsive web.
- **AI features** are optional and can be fully disabled; when enabled, only the needed ticket content is
  sent to the AI service and is not used to train external models.
- **Suggested delivery order** follows story priority: P1 (Tickets, Customers, Administration),
  then P2 (Dashboard, Portal, Channels, SLA), then P3 (Knowledge Base, Reports, AI, Integrations).
