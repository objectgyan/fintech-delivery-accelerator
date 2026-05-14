# Phase 3 Demo — Real System Feel

Closing demo for Phase 3. Six scenarios run end-to-end on Azure, demonstrating the full payment platform with fraud, email notifications, and observability.

**Run the full sequence twice in a row** before declaring done. Capture a fallback screen recording on the second clean run.

## Prerequisites

- Payment, Fraud, Notification services deployed to ACA (current revisions)
- Azure SQL `fintech-payments` not paused (run a warmup `GET /health` first)
- SendGrid API key set on the notification worker
- App Insights dashboard open in a second tab
- Postman collection imported: `docs/postman/fintech.postman_collection.json`

---

## Scenario 1 — Successful payment

**Goal:** End-to-end happy path: API → Stripe → SQL → outbox → Service Bus → notification → email.

```http
POST /payments
Idempotency-Key: demo-1-{date}
Content-Type: application/json

{
  "amount": 49.99,
  "currency": "USD",
  "customerEmail": "demo@example.com",
  "paymentMethodToken": "tok_visa"
}
```

**Expected:**
- `201 Created` with `paymentId`, `status: "authorized"`, `fraudDecision: "approve"`, `fraudScore: 0`
- `X-Trace-Id` header on the response
- Postman test: `pm.expect(pm.response.code).to.eql(201)`

**Verify:**
1. SQL — `SELECT TOP 1 * FROM Payments ORDER BY CreatedAtUtc DESC` — new row, `Status = 'Authorized'`
2. SQL — `SELECT * FROM OutboxMessages WHERE Status = 'Published' ORDER BY CreatedAtUtc DESC` — one row
3. SQL — `SELECT * FROM NotificationLogs ORDER BY CreatedAtUtc DESC` — one row, `Status = 'sent'`
4. Inbox — confirmation email received

---

## Scenario 2 — Failure + retry + circuit breaker

**Goal:** Demonstrate Polly resilience: retries on transient errors, breaker trips after sustained failure.

Set `Payment:SimulateTransientFailures=true` and `Payment:SimulateFailCount=10` via ACA env var, restart payment revision.

Fire 6 payments in quick succession (any payload).

**Expected:**
- First 3-4 requests: each retries 3× then returns `502 Bad Gateway` with provider failure
- After breaker threshold (≥50% failures in 30s, min throughput 5): `502` with `code: "circuit_open"`, near-instant
- App Insights: `payment.retry.count` rises; `payment.failure.count{reason=circuit_open}` rises
- `Logs` blade: `Stripe circuit breaker OPENED` log line

**Reset:** clear `Payment:SimulateTransientFailures` flag and restart revision.

---

## Scenario 3 — Idempotency

**Goal:** Same `Idempotency-Key` returns same response without duplicate work.

```http
POST /payments
Idempotency-Key: demo-3-fixed
{ ...identical body... }
```

Send the same request twice within 24 h.

**Expected:**
- Both responses: identical `paymentId`
- SQL — exactly one row in `Payments` with this `Id`
- SQL — `IdempotencyKeys` table has one row with cached response
- Second request: server log shows `Idempotency replay`

Send a third request with the **same key but different body**:
- `409 Conflict` with `code: "idempotency_mismatch"`

---

## Scenario 4 — Fraud screening

**Goal:** Fraud rule engine declines a payment; decision is persisted.

Set in fraud service config: `Fraud:BlockedDomains=["blocked.com"]`.

```http
POST /payments
Idempotency-Key: demo-4-{date}
{ "amount": 50, "currency": "USD", "customerEmail": "test@blocked.com", "paymentMethodToken": "tok_visa" }
```

**Expected:**
- `422 Unprocessable Entity` with `code: "fraud_declined"`, `score: 100`, `rulesTriggered: ["blocked-domain"]`
- SQL — `Payments` row exists with `Status = 'Failed'`
- SQL — `FraudDecisions` row exists with `Score=100, Decision='Decline'`
- No outbox message (event not published)
- No email sent

Run a `high-amount` decline next (amount: 5000, score 40 — review tier; demo as a flag, not a decline).

---

## Scenario 5 — Email notification

**Goal:** SendGrid delivers a real email on the happy path; delivery is logged.

Run Scenario 1 again with `customerEmail` set to your real inbox.

**Expected:**
- Email arrives within ~30 s
- Subject: `Payment of 49.99 USD authorised`
- SQL — `NotificationLogs` row, `Status='sent'`, `RecipientEmail` matches

Replay the same event manually (e.g., re-enqueue from Service Bus Explorer):
- Idempotency guard fires
- `NotificationLogs` still has only one row
- Notification worker logs `Skipping duplicate email`

---

## Scenario 6 — Distributed trace

**Goal:** One trace ID follows a payment across all three services.

Run Scenario 1. Copy the `X-Trace-Id` from the response header.

Open App Insights → **Search** → paste trace ID into the operation_Id filter.

**Expected:** Single end-to-end transaction containing:
- `POST /payments` (payment-api)
- `HTTP POST /fraud/evaluate` (fraud-api, child of payment)
- SQL `INSERT Payments` + `INSERT OutboxMessages` + `INSERT IdempotencyKeys`
- Stripe HTTP call
- Service Bus `Send` (outbox relay)
- Service Bus `Receive` + processing in notification worker (child via W3C link)
- SendGrid HTTP call

Screenshot the transaction view — that goes in the evidence package.

---

## Evidence checklist

For each scenario, capture:
- [ ] HTTP response body / headers
- [ ] SQL row screenshot for the relevant table(s)
- [ ] App Insights snippet (Failures / Search / Custom Metrics tab)

Plus:
- [ ] One end-to-end fallback screen recording (~5 min) covering all 6 scenarios
- [ ] Two clean dry-runs back-to-back before considering Phase 3 done

## Known limitations

Tracked in [`docs/accelerator/known-limitations.md`](../accelerator/known-limitations.md). The major ones:

- Azure SQL is provisioned manually (not in Bicep) and lives in West US 2 — all other resources are in East US
- Fraud service has no caching — duplicate `customerEmail` queries hit SQL each time
- Notification worker uses one connection string secret; managed-identity migration is Phase 4
- No private endpoints / VNet integration yet (Phase 4)
- Email retries are not implemented; SendGrid failures are logged but not retried
