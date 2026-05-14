# Pattern: Idempotency Keys

**Problem:** A client retries a `POST /payments` after a timeout — without protection, they get charged twice.

**Solution:** Client supplies an `Idempotency-Key` header. Server stores the key + a hash of the request body + the cached response. Subsequent requests with the same key replay the cached response.

## Implementation map

| File | Role |
|------|------|
| `Payment.Domain/Entities/IdempotencyKey.cs` | Entity — `Key` (string, ≤ 128), `RequestHash`, `Status` (`Pending`/`Completed`), cached response JSON, status code |
| `Payment.Infrastructure/Data/Configurations/IdempotencyKeyConfiguration.cs` | **Unique index `UX_IdempotencyKeys_Key`** — DB-level guard against concurrent dupes |
| `Payment.Api/Endpoints/PaymentEndpoints.cs` | Reads `Idempotency-Key` header, validates length / charset before passing to handler |
| `Payment.Application/Commands/CreatePayment/CreatePaymentCommandHandler.cs` | Receives `IdempotencyKeyEntity` in the command; on success calls `CompleteWithIdempotencyAsync` which writes both the payment + the cached response in one transaction |
| `Payment.Infrastructure/Repositories/PaymentRepository.cs` | Catches `DbUpdateException` on unique-index violation → returns existing key for replay |

## Three outcomes for the same key

| Body hash matches existing key? | Outcome |
|----------------------------------|---------|
| Yes, status=Completed | Return cached 201 response — no Stripe call |
| No (different body) | `409 Conflict` with `code: "idempotency_mismatch"` |
| Concurrent insert (race) | DB unique index throws → second writer reads existing row → 409 or replay depending on status |

## Why not a distributed lock?

A SQL unique index *is* the lock — at the storage layer where it belongs. No Redis, no leader election, no clock skew. Lower-latency than any out-of-band lock for this purpose.

## Retention

Phase 3 keeps idempotency keys indefinitely. Production should age them out after some period (typical: 24h–7d). Add a cleanup service modeled on `OutboxCleanupService` when this matters.
