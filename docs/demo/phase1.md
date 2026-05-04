# Phase 1 Demo Guide — Thin Vertical Slice

Demonstrates the full payment flow: **POST /payments → Stripe authorize → SQL persist → PaymentAuthorized.v1 event → Notification.Worker log**.

## Prerequisites

| Requirement | Notes |
|---|---|
| Stripe sandbox key | `sk_test_...` — in Payment.Api user secrets |
| Azure SQL running | `fintech-payments-sql` — serverless, may need 30-60s cold start |
| Azure Service Bus | `fintech-dev-bus`, topic `payment-authorized`, subscription `notification` |
| ACA apps running | `fintech-dev-payment` (public), `fintech-dev-notification` (worker) |

---

## Option A: ACA Demo (deployed environment)

**Payment service URL:** `https://fintech-dev-payment.purplestone-2afd62bc.eastus.azurecontainerapps.io`

### Step 1 — Health check
```bash
curl https://fintech-dev-payment.purplestone-2afd62bc.eastus.azurecontainerapps.io/health
# Expected: {"status":"ok","service":"payment"}
```

### Step 2 — Submit payment (Postman)
Open `tests/postman/payment-happy-path/payment-happy-path.postman_collection.json` in Postman.

The collection variable `base_url` defaults to the ACA URL. Run the full collection:

| Request | Expected |
|---|---|
| GET /health | 200 `{"status":"ok","service":"payment"}` |
| POST /payments — happy path | 201 with `paymentId`, `status=authorized`, `providerReference=pi_...` |
| POST /payments — declined card | 402 with `error=card_declined` |
| POST /payments — invalid body | 400 with validation errors |

### Step 3 — Verify DB row
Copy the `paymentId` from Step 2. Open `tests/integration/sql-validation.sql`, paste the ID, and run against the `fintech-payments` database.

Expected: one row with `Status=Authorized`, `Provider=stripe`, `ProviderReference` starting with `pi_`.

### Step 4 — Verify notification worker
```bash
az containerapp logs show \
  --name fintech-dev-notification \
  --resource-group fintech-dev-rg \
  --tail 20
```

Expected log line:
```
PaymentAuthorized received — eventId=<guid> paymentId=<same-id-as-step-2> amount=99.99 USD email=demo@phase1test.com
```

---

## Option B: Local Demo

Requires: Payment.Api running locally on port 5101 with user secrets configured.

```powershell
# Start Payment.Api
cd services/payment/Payment.Api
dotnet run

# In a new terminal, run the validation script
cd scripts
.\Validate-PaymentService.ps1 -BaseUrl http://localhost:5101
# Expected: 5/5 tests passed
```

---

## Known Limitations (Phase 2 work)

These are intentional deferrals — not bugs:

| Limitation | Phase 2 ticket |
|---|---|
| Events published directly (no transactional outbox) | FT-203/205 — if the publish fails after DB write, the event is lost |
| No idempotency — duplicate requests create duplicate payments | FT-204 |
| No retry on Stripe transient errors | FT-206 |
| No circuit breaker on Stripe calls | FT-207 |
| Notification.Worker does not send email — logs only | FT-301 (Phase 3) |
