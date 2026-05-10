# Phase 2 Demo Guide — Reliability

Demonstrates the four reliability patterns shipped in Phase 2:
1. **Transactional Outbox** — event survives a relay restart
2. **Idempotency** — duplicate request returns cached 201, one DB row
3. **Retry + Circuit Breaker** — simulated Stripe failures trigger retries; repeated failures open the breaker
4. **Concurrent Idempotency** — 15 parallel same-key requests → exactly one payment created

## Prerequisites

| Requirement | Notes |
|---|---|
| Payment.Api running | ACA (`payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io`) or local port 5000 |
| Azure SQL accessible | `fintech-payments-sql` serverless — allow 30–60s cold-start on first request |
| Azure Service Bus | `fintech-dev-bus`, topic `payment-authorized` |
| Stripe sandbox key | `sk_test_...` — in user secrets |
| PowerShell 7+ | For demo scripts in `scripts/` |

---

## Demo 1 — Transactional Outbox (relay restart)

Verifies that the `payment_authorized` event is published even if the relay worker is restarted mid-flight.

### Steps

```powershell
# 1. Submit a payment
$key = [guid]::NewGuid().ToString()
$response = Invoke-RestMethod `
  -Uri "https://payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io/payments" `
  -Method POST `
  -ContentType "application/json" `
  -Headers @{ "Idempotency-Key" = $key } `
  -Body '{"amount":10.00,"currency":"USD","customerEmail":"demo@phase2test.com","paymentMethodToken":"tok_visa"}'

$paymentId = $response.paymentId
Write-Host "Payment ID: $paymentId"
```

```sql
-- 2. Verify outbox row is Pending (before relay picks it up)
SELECT Id, EventType, Status, RetryCount, CreatedAtUtc
FROM OutboxMessages
WHERE Status = 'Pending'
ORDER BY CreatedAtUtc DESC
```

```powershell
# 3. (Optional) Restart the relay to simulate a crash between write and publish
az containerapp revision restart `
  --name payment-api `
  --resource-group fintech-dev-rg `
  --revision $(az containerapp show -n payment-api -g fintech-dev-rg --query "properties.latestRevisionName" -o tsv)
```

```sql
-- 4. After relay resumes, verify outbox row is Published
SELECT Id, EventType, Status, ProcessedAtUtc
FROM OutboxMessages
WHERE Id IN (SELECT Id FROM OutboxMessages WHERE Status = 'Published')
ORDER BY ProcessedAtUtc DESC
```

**Expected:** `Status = Published`, `ProcessedAtUtc` populated.

---

## Demo 2 — Idempotency (duplicate request)

Verifies that replaying a request with the same `Idempotency-Key` returns the original response and creates exactly one `Payments` row.

### Steps

```powershell
$key = [guid]::NewGuid().ToString()
$body = '{"amount":25.00,"currency":"USD","customerEmail":"demo@phase2test.com","paymentMethodToken":"tok_visa","description":"idempotency demo"}'
$headers = @{ "Idempotency-Key" = $key }
$url = "https://payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io/payments"

# First request
$r1 = Invoke-RestMethod -Uri $url -Method POST -ContentType "application/json" -Headers $headers -Body $body
Write-Host "First:  $($r1.paymentId) status=$($r1.status)"

# Replay (same key, same body)
$r2 = Invoke-RestMethod -Uri $url -Method POST -ContentType "application/json" -Headers $headers -Body $body
Write-Host "Replay: $($r2.paymentId) status=$($r2.status)"

# Verify same payment ID returned
if ($r1.paymentId -eq $r2.paymentId) { Write-Host "PASS: same paymentId" -ForegroundColor Green }
else { Write-Host "FAIL: different paymentId" -ForegroundColor Red }
```

```sql
-- Verify one Payments row and one IdempotencyKeys row
SELECT COUNT(*) AS PaymentRows FROM Payments WHERE Id = '<paymentId>'       -- expect 1
SELECT Key, RequestHash, PaymentId, ResponseStatusCode, CompletedAtUtc
FROM IdempotencyKeys WHERE [Key] = '<idempotency-key>'                      -- expect 1 row, StatusCode=201
```

**Expected:** Both responses have identical `paymentId`. DB shows one `Payments` row and one `IdempotencyKeys` row.

### Hash mismatch (409)

```powershell
# Same key, different body → 409 Conflict
$differentBody = '{"amount":99.00,"currency":"USD","customerEmail":"other@test.com","paymentMethodToken":"tok_visa"}'
try {
    Invoke-RestMethod -Uri $url -Method POST -ContentType "application/json" -Headers $headers -Body $differentBody
} catch {
    Write-Host "HTTP $($_.Exception.Response.StatusCode) — expected 409" -ForegroundColor Yellow
}
```

---

## Demo 3 — Concurrent Idempotency

Verifies that 15 simultaneous requests with the same key produce exactly one payment.

```powershell
.\scripts\Test-IdempotencyConcurrency.ps1
# Or against local:
.\scripts\Test-IdempotencyConcurrency.ps1 -BaseUrl http://localhost:5000 -Concurrency 15
```

**Expected output:**
```
Results summary
  201 Created : 1
  409 Conflict: 14
  Other       : 0

PASS: Concurrency idempotency validated
```

```sql
-- Confirm one row
SELECT COUNT(*) FROM Payments WHERE Id = '<paymentId from script output>'   -- expect 1
SELECT COUNT(*) FROM IdempotencyKeys WHERE [Key] = '<key from script output>'  -- expect 1
```

---

## Demo 4 — Retry + Circuit Breaker (simulated failures)

### 4a — Retry with recovery

Enables the failure simulator so the first 2 calls fail transiently, the 3rd succeeds.

```powershell
# Requires local run with simulation enabled in appsettings.Development.json:
# "Payment": { "SimulateTransientFailures": true, "SimulateFailCount": 2 }

$key = [guid]::NewGuid().ToString()
Invoke-RestMethod `
  -Uri "http://localhost:5000/payments" `
  -Method POST `
  -ContentType "application/json" `
  -Headers @{ "Idempotency-Key" = $key } `
  -Body '{"amount":50.00,"currency":"USD","customerEmail":"demo@retry.com","paymentMethodToken":"tok_visa"}'
```

**Expected logs:**
```
[SIMULATION] Injecting transient failure on call 1/2
WARN  Transient provider failure on attempt 1 for payment <id>. Retrying in 1.0s (error: simulated_transient_error)
[SIMULATION] Injecting transient failure on call 2/2
WARN  Transient provider failure on attempt 2 for payment <id>. Retrying in 2.0s (error: simulated_transient_error)
[SIMULATION] Call 3 passes through to real provider
```

```sql
-- Two failed attempts + one succeeded attempt
SELECT AttemptNo, Status, ErrorCode, StartedAtUtc, CompletedAtUtc
FROM PaymentAttempts
WHERE PaymentId = '<paymentId>'
ORDER BY AttemptNo
-- Rows: 1=TransientError, 2=TransientError, 3=Succeeded
```

### 4b — Circuit breaker opening

Set `SimulateFailCount` to a high value (e.g. 20) and fire multiple requests quickly. After enough exhausted retry batches (≥50% failure ratio in 30s window with min 5 calls), the circuit opens.

```powershell
# In appsettings.Development.json: "SimulateFailCount": 20

1..8 | ForEach-Object {
    $key = [guid]::NewGuid().ToString()
    try {
        Invoke-RestMethod -Uri "http://localhost:5000/payments" -Method POST `
          -ContentType "application/json" `
          -Headers @{ "Idempotency-Key" = $key } `
          -Body '{"amount":1.00,"currency":"USD","customerEmail":"demo@circuit.com","paymentMethodToken":"tok_visa"}' `
          -ErrorAction Stop
    } catch { Write-Host "Request $_: HTTP $($_.Exception.Response.StatusCode)" }
}
```

**Expected logs after enough failures:**
```
WARN  Payment.StripeCircuitBreaker: Stripe circuit breaker OPENED — break duration 30.0s
WARN  Circuit breaker open for payment <id> — failing fast
INFO  Payment.StripeCircuitBreaker: Stripe circuit breaker HALF-OPEN — probing next call
INFO  Payment.StripeCircuitBreaker: Stripe circuit breaker CLOSED
```

---

## Outbox Cleanup Verification

The cleanup service deletes Published rows older than 7 days. To verify it works in dev without waiting 7 days, seed an old row and run a targeted SQL query:

```sql
-- Seed an old published row
INSERT INTO OutboxMessages (Id, EventType, PayloadJson, Status, RetryCount, CreatedAtUtc, ProcessedAtUtc)
VALUES (NEWID(), 'PaymentAuthorized.v1', '{"test":"cleanup"}', 'Published', 0,
        DATEADD(day, -8, GETUTCDATE()), DATEADD(day, -8, GETUTCDATE()))

-- Before: count published rows older than 7d
SELECT COUNT(*) FROM OutboxMessages
WHERE Status = 'Published' AND ProcessedAtUtc < DATEADD(day, -7, GETUTCDATE())

-- Restart the API (cleanup runs on startup) then re-check
SELECT COUNT(*) FROM OutboxMessages
WHERE Status = 'Published' AND ProcessedAtUtc < DATEADD(day, -7, GETUTCDATE())
-- Expected: 0 (seeded row deleted, Pending/Failed rows untouched)
```

---

## Phase 2 Reliability — What Was Shipped

| Ticket | Pattern | PR |
|---|---|---|
| FT-202 | PaymentAttempts table | #11 |
| FT-203 | OutboxMessages table | #12 |
| FT-204 | Outbox Relay Worker (BackgroundService) | #13 |
| FT-205 | Transactional handler (outbox replaces direct publish) | #14 |
| FT-206 | Idempotency-Key header validation | #15 |
| FT-207 | IdempotencyKeys table + dedup + cached response | #17 |
| FT-208 | Concurrency-safe idempotency (DB unique index guard) | #18 |
| FT-209 | Polly v8 retry (exponential backoff, attempt rows) | #20 |
| FT-210 | Polly v8 circuit breaker (singleton, state logging) | #21 |
| FT-211 | Provider failure simulation mode (dev-only) | #22 |
| FT-212 | Outbox cleanup service (7-day retention) | #23 |
