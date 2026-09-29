# Outbound Webhook Events Contract

Subscriptions are managed via `/api/v1/admin/webhooks` (see [openapi.yaml](openapi.yaml)).

## Delivery

- `POST {subscription.url}` over HTTPS, `Content-Type: application/json`, timeout 10 s.
- Headers:
  - `X-CRM-Event`: event type, e.g. `ticket.created`
  - `X-CRM-Delivery`: delivery id (uuid) — idempotency key for receivers
  - `X-CRM-Timestamp`: Unix seconds
  - `X-CRM-Signature`: `sha256=` + hex(HMAC-SHA256(secret, `{timestamp}.{rawBody}`))
- Success: any `2xx`. Otherwise retried at 1 m, 5 m, 30 m, 2 h, 12 h; then marked
  `failedPermanently` and visible in `/admin/webhooks/{id}/deliveries`.
- At-least-once delivery; order is not guaranteed — receivers should use `occurredAt` and `version`.
- Receivers must reject signatures older than 5 minutes (replay protection).

## Envelope

```json
{
  "id": "5b3c7e7a-3b8a-4c64-9a8d-1f6f4b2f9e11",
  "type": "ticket.status_changed",
  "occurredAt": "2026-09-29T10:15:00Z",
  "apiVersion": "v1",
  "data": { }
}
```

## Event types and `data`

| Type | `data` |
|---|---|
| `ticket.created` | `TicketSummary` (openapi) |
| `ticket.updated` | `{ ticket: TicketSummary, changedFields: string[] }` |
| `ticket.status_changed` | `{ ticket: TicketSummary, oldStatus, newStatus, reason? }` |
| `customer.created` | `CustomerSummary` (openapi) |

Personal data included is limited to what the subscribing API key's permissions allow;
internal notes and message bodies are never included in webhook payloads.
