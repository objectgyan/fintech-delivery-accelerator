# Pattern: Transactional Outbox

**Problem:** "Save to DB, then publish event" has a window where the DB write succeeds but the publish fails — event is lost.

**Solution:** Write the event to an `OutboxMessages` table in the *same transaction* as the business write. A separate background process reads pending rows and publishes them.

## Implementation map

| File | Role |
|------|------|
| `Payment.Domain/Entities/OutboxMessage.cs` | Entity with `Status` enum (`Pending`, `Published`, `Failed`), retry counter |
| `Payment.Infrastructure/Data/Configurations/OutboxMessageConfiguration.cs` | EF config — **filtered index on `Status = 'Pending'`** is the critical perf knob |
| `Payment.Infrastructure/Messaging/OutboxRelayService.cs` | `BackgroundService`, polls every 5 s, batch size 10, publishes to Service Bus, marks `Published` |
| `Payment.Infrastructure/Messaging/OutboxCleanupService.cs` | `BackgroundService`, runs daily, deletes `Published` rows older than 7 days via `ExecuteDeleteAsync` |
| `Payment.Application/Commands/CreatePayment/CreatePaymentCommandHandler.cs` | Calls `_repository.CompletePaymentAsync(payment, attempt, outboxMessage, ...)` — single transaction |
| `Payment.Infrastructure/Repositories/PaymentRepository.cs` | All multi-write methods commit in one `SaveChangesAsync` |

## Key design choices

- **Polling, not change feed.** SQL Change Streams aren't available in serverless SQL. Polling every 5 s is good enough for the demo; tune down for higher volume.
- **In-process, not separate service.** OutboxRelay runs inside Payment.Api — keeps deployment simple. If write volume justifies, extract to its own worker later.
- **Idempotent publish.** Service Bus dedupes on `MessageId = outboxMessage.Id` — re-publishing the same row is safe.
- **Cleanup is a separate service.** Don't mix `DELETE` into the relay loop — separation of concerns and easier failure isolation.

## When the relay is offline

Scale-to-zero is safe: rows stay `Pending` in SQL. When the replica wakes, it picks them up. No events are lost.

## What to watch

- `payment.outbox.published.count` — flat or zero means the relay is stuck
- `payment.outbox.publish_failed.count` — non-zero indicates Service Bus problems
- SQL query — `SELECT COUNT(*) FROM OutboxMessages WHERE Status = 'Pending' AND CreatedAtUtc < DATEADD(MINUTE, -10, SYSDATETIMEOFFSET())` — anything > 0 is a backlog
