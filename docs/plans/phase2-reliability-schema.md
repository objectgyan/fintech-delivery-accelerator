# Phase 2 — Reliability Schema Design

**Ticket:** OBJ-22 / FT-201  
**Status:** Awaiting engineer approval before FT-202/203 begin  
**Author:** claude-code[bot]  
**Date:** 2026-05-09

---

## Context

Phase 1 left three reliability gaps that this schema closes:

| Gap | Root cause | Fix |
|-----|-----------|-----|
| Event lost if Service Bus call fails after SQL write | Direct publish inside handler, not transactional | `OutboxMessages` + relay service |
| Duplicate charge on client retry | No deduplication at API layer | `IdempotencyKeys` |
| No visibility into Stripe call history per payment | Single status field on `Payments` | `PaymentAttempts` |

All three tables are **additive** — nothing in the existing `Payments` table changes.

---

## Existing Baseline

From `20260503222505_InitialPayments`, the established conventions are:

- Primary keys: `uniqueidentifier` (GUID), not identity int
- Timestamps: `datetimeoffset` (not `datetime2`) — preserves UTC offset
- Enums stored as `nvarchar` strings — readable in SQL, no int-to-meaning mapping needed
- String columns carry explicit `maxLength` — no unbounded `nvarchar(max)` except for JSON payloads
- All EF entity configuration via `IEntityTypeConfiguration<T>` in `Payment.Infrastructure/Data/Configurations/`

---

## Table 1 — OutboxMessages

**Purpose:** Stores domain events alongside the payment write in the same DB transaction. An `OutboxRelay` background service (FT-206) reads `Pending` rows, publishes to Service Bus, and marks them `Published`. Guarantees at-least-once delivery even if Service Bus is temporarily unavailable.

### Schema

```sql
CREATE TABLE OutboxMessages (
    Id              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    EventType       NVARCHAR(100)       NOT NULL,   -- e.g. "PaymentAuthorized.v1"
    PayloadJson     NVARCHAR(MAX)       NOT NULL,   -- serialised event envelope
    Status          NVARCHAR(20)        NOT NULL,   -- Pending | Published | Failed
    RetryCount      INT                 NOT NULL DEFAULT 0,
    CreatedAtUtc    DATETIMEOFFSET      NOT NULL,
    ProcessedAtUtc  DATETIMEOFFSET      NULL,       -- set when Status → Published/Failed
    CONSTRAINT PK_OutboxMessages PRIMARY KEY (Id)
);

CREATE INDEX IX_OutboxMessages_Status_CreatedAtUtc
    ON OutboxMessages (Status, CreatedAtUtc)
    WHERE Status = 'Pending';          -- filtered index — relay only scans Pending rows
```

### C# Entity

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payloadJson) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        PayloadJson = payloadJson,
        Status = OutboxStatus.Pending,
        RetryCount = 0,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public void MarkPublished() { Status = OutboxStatus.Published; ProcessedAtUtc = DateTimeOffset.UtcNow; }
    public void MarkFailed()    { Status = OutboxStatus.Failed;    ProcessedAtUtc = DateTimeOffset.UtcNow; }
    public void IncrementRetry() => RetryCount++;
}

public enum OutboxStatus { Pending, Published, Failed }
```

### Index rationale

The filtered index on `Status = 'Pending'` means the relay's poll query (`WHERE Status = 'Pending' ORDER BY CreatedAtUtc`) touches only unprocessed rows. Published rows (the large majority over time) are excluded from the index entirely.

### Lifecycle / cleanup

- Rows with `Status = 'Published'` older than 7 days can be hard-deleted.
- Rows with `Status = 'Failed'` should be retained for 30 days for incident investigation.
- Cleanup is out of scope for Phase 2 — a scheduled job or SQL Agent task is a Phase 3+ concern.

---

## Table 2 — IdempotencyKeys

**Purpose:** Caches the response for each unique `Idempotency-Key` request header. If a client retries with the same key, the cached response is returned without re-charging Stripe. Prevents duplicate payments on network timeouts or client retries.

### Schema

```sql
CREATE TABLE IdempotencyKeys (
    Id              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    IdempotencyKey  NVARCHAR(128)       NOT NULL,   -- value from Idempotency-Key header
    RequestHash     NVARCHAR(64)        NOT NULL,   -- SHA-256 of request body (detects body mismatch)
    PaymentId       UNIQUEIDENTIFIER    NULL,        -- FK to Payments.Id once payment created
    ResponseJson    NVARCHAR(MAX)       NULL,        -- serialised HTTP response body
    StatusCode      INT                 NOT NULL DEFAULT 0,
    CreatedAtUtc    DATETIMEOFFSET      NOT NULL,
    CONSTRAINT PK_IdempotencyKeys PRIMARY KEY (Id),
    CONSTRAINT UQ_IdempotencyKeys_Key UNIQUE (IdempotencyKey)
);
```

### C# Entity

```csharp
public sealed class IdempotencyKey
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid? PaymentId { get; private set; }
    public string? ResponseJson { get; private set; }
    public int StatusCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(string key, string requestHash) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        RequestHash = requestHash,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public void Complete(Guid paymentId, string responseJson, int statusCode)
    {
        PaymentId = paymentId;
        ResponseJson = responseJson;
        StatusCode = statusCode;
    }
}
```

### Index rationale

The `UNIQUE` constraint on `IdempotencyKey` is the primary lookup path — `WHERE IdempotencyKey = @key` will use this. No additional index needed.

### Body mismatch detection

If a client sends the same `Idempotency-Key` with a different request body (different amount, currency, etc.), the `RequestHash` comparison catches it and returns HTTP 422 — same key, different request is always an error.

### Lifecycle / cleanup

- Rows older than 24 hours can be deleted — idempotency keys are only meaningful within a retry window. Phase 2 scope: no cleanup job, rows accumulate. Phase 3+ adds cleanup.

---

## Table 3 — PaymentAttempts

**Purpose:** Records every call made to Stripe per payment, including transient failures that are retried. Gives full visibility into: how many attempts were made, which failed transiently vs terminally, and timing. Required for Polly retry instrumentation (FT-207).

### Schema

```sql
CREATE TABLE PaymentAttempts (
    Id              UNIQUEIDENTIFIER    NOT NULL DEFAULT NEWSEQUENTIALID(),
    PaymentId       UNIQUEIDENTIFIER    NOT NULL,   -- FK to Payments.Id
    AttemptNo       INT                 NOT NULL,   -- 1-based, increments per payment
    Provider        NVARCHAR(50)        NOT NULL,   -- "stripe"
    Status          NVARCHAR(20)        NOT NULL,   -- Succeeded | Failed | TransientError
    ErrorCode       NVARCHAR(100)       NULL,
    ErrorMessage    NVARCHAR(500)       NULL,
    StartedAtUtc    DATETIMEOFFSET      NOT NULL,
    CompletedAtUtc  DATETIMEOFFSET      NULL,
    CONSTRAINT PK_PaymentAttempts PRIMARY KEY (Id),
    CONSTRAINT FK_PaymentAttempts_Payments
        FOREIGN KEY (PaymentId) REFERENCES Payments(Id)
);

CREATE INDEX IX_PaymentAttempts_PaymentId
    ON PaymentAttempts (PaymentId);
```

### C# Entity

```csharp
public sealed class PaymentAttempt
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public int AttemptNo { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public AttemptStatus Status { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private PaymentAttempt() { }

    public static PaymentAttempt Start(Guid paymentId, int attemptNo, string provider) => new()
    {
        Id = Guid.NewGuid(),
        PaymentId = paymentId,
        AttemptNo = attemptNo,
        Provider = provider,
        Status = AttemptStatus.InProgress,
        StartedAtUtc = DateTimeOffset.UtcNow
    };

    public void Succeed()
    {
        Status = AttemptStatus.Succeeded;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Fail(string? errorCode, string? errorMessage, bool isTransient)
    {
        Status = isTransient ? AttemptStatus.TransientError : AttemptStatus.Failed;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        CompletedAtUtc = DateTimeOffset.UtcNow;
    }
}

public enum AttemptStatus { InProgress, Succeeded, Failed, TransientError }
```

### Index rationale

`IX_PaymentAttempts_PaymentId` covers the common query: "give me all attempts for payment X". The FK constraint ensures referential integrity — no orphaned attempts.

### Lifecycle / cleanup

Retain indefinitely — attempt history is audit data. Volume is low (1–3 rows per payment in normal operation).

---

## Migration Plan

Three additive EF Core migrations, one per table, applied in this sequence:

| Migration name | Ticket | Adds |
|---|---|---|
| `AddPaymentAttempts` | FT-202 | `PaymentAttempts` table + index + FK |
| `AddOutboxMessages` | FT-203 | `OutboxMessages` table + filtered index |
| `AddIdempotencyKeys` | FT-204 | `IdempotencyKeys` table + unique constraint |

Each migration is generated via `dotnet ef migrations add <Name>` and auto-applied on startup (existing pattern from `InitialPayments`). No manual SQL scripts.

**Zero downtime:** all three tables are new — no columns added to `Payments`, no existing indexes changed. The running Phase 1 app is unaffected by any of these migrations.

---

## EF Configuration Files

One `IEntityTypeConfiguration<T>` per entity in `Payment.Infrastructure/Data/Configurations/`:

| File | Entity |
|---|---|
| `OutboxMessageConfiguration.cs` | `OutboxMessage` |
| `IdempotencyKeyConfiguration.cs` | `IdempotencyKey` |
| `PaymentAttemptConfiguration.cs` | `PaymentAttempt` |

`PaymentDbContext` already calls `ApplyConfigurationsFromAssembly` — no changes to `PaymentDbContext.cs` beyond adding the three `DbSet<T>` properties.

---

## Repository Interface Changes

`IPaymentRepository` (in `Payment.Domain/Repositories/`) will gain new methods as each ticket ships:

| Method | Added in |
|---|---|
| `AddAttemptAsync(PaymentAttempt, CancellationToken)` | FT-202 |
| `GetAttemptsAsync(Guid paymentId, CancellationToken)` | FT-202 |
| `AddOutboxMessageAsync(OutboxMessage, CancellationToken)` | FT-203 |
| `GetPendingOutboxMessagesAsync(int batchSize, CancellationToken)` | FT-206 (relay) |
| `GetByIdempotencyKeyAsync(string key, CancellationToken)` | FT-204 |
| `AddIdempotencyKeyAsync(IdempotencyKey, CancellationToken)` | FT-204 |

These are defined here for awareness — the interface is only updated in the ticket that first uses each method.

---

## What This Ticket Does NOT Include

- No EF entity classes written yet (each ticket writes its own)
- No migrations generated yet
- No changes to `CreatePaymentCommandHandler.cs`
- No changes to `PaymentDbContext.cs`
- No `IPaymentRepository` changes yet

This document is the approval gate. FT-202 and FT-203 begin only after sign-off.
