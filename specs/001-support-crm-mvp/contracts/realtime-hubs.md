# Real-time Contract (SignalR)

Transport: ASP.NET Core SignalR, JSON protocol, automatic reconnect. Payloads reuse the DTO
schemas from [openapi.yaml](openapi.yaml).

## `/hubs/agent` — staff (Bearer JWT required)

Server joins each connection to groups: `user:{userId}`, `dept:{departmentId}` (each of the user's
departments), and `ticket:{ticketId}` on demand.

### Client → Server

| Method | Args | Purpose |
|---|---|---|
| `WatchTicket` | `ticketId: uuid` | Join `ticket:{id}` to receive live updates / typing |
| `UnwatchTicket` | `ticketId: uuid` | Leave group |
| `SetAvailability` | `isAvailable: bool` | Toggle availability for auto-assignment & chat routing |
| `AcceptChat` | `chatSessionId: uuid` | Take a waiting chat (first accept wins) |
| `SendChatMessage` | `chatSessionId: uuid, body: string` | Agent message in live chat (≤ 5,000 chars) |
| `Typing` | `chatSessionId: uuid` | Typing indicator to visitor |
| `EndChat` | `chatSessionId: uuid` | End chat; transcript remains on the ticket |

### Server → Client

| Event | Payload | When |
|---|---|---|
| `NotificationReceived` | `Notification` | Assignment, mention, reminder, SLA warning/breach, escalation |
| `TicketUpdated` | `TicketSummary` | Any change to a watched ticket or a ticket in user's queue |
| `MessageAdded` | `Message` | New message on a watched ticket |
| `MessageDeliveryChanged` | `{ messageId, deliveryStatus, deliveryError }` | Channel status callback |
| `ChatWaiting` | `{ chatSessionId, visitorName, language, waitingSince }` | Visitor asked for a human (to `dept:*`) |
| `ChatAccepted` | `{ chatSessionId, agentId }` | Another agent accepted (remove from list) |
| `ChatMessage` | `{ chatSessionId, body, sender, sentAt }` | Visitor or bot message |
| `ChatEnded` | `{ chatSessionId, ticketId }` | Chat closed |
| `TicketLocked` | `{ ticketId, byUser }` | Another agent is editing (advisory only; rowversion is authoritative) |

## `/hubs/chat` — website visitors (anonymous)

Auth: `access_token` query parameter = chat session token from `POST /api/v1/public/chat/sessions`.
Connection limited to its own `chat:{sessionId}` group. Rate-limited (20 messages/min).

### Client → Server

| Method | Args | Purpose |
|---|---|---|
| `SendMessage` | `body: string` (≤ 2,000) | Visitor message; routed to bot or agent |
| `RequestHuman` | – | Skip/hand off from bot to an agent |
| `Typing` | – | Typing indicator |
| `End` | – | Visitor closes chat |

### Server → Client

| Event | Payload |
|---|---|
| `Message` | `{ body, senderKind: Agent|Bot|System, senderName, sentAt, articleLinks?: [{title,url}] }` |
| `StatusChanged` | `{ status: Bot|Waiting|Active|Ended, queuePosition?: int }` |
| `AgentTyping` | – |

## Rules

- Internal notes are never broadcast to `/hubs/chat`.
- All hub method arguments are validated like REST input; failures raise `HubException` with a
  localized message and `code`.
- Events are emitted after the database transaction commits (outbox → dispatcher).
