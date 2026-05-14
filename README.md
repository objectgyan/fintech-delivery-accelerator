# FinTech Delivery Accelerator

A production-quality FinTech payment platform built as a reusable accelerator.

Solo backend engineer + Claude Code. Demo-first delivery across 5 phases. Target: working demo in ~6 weeks.

## What This Is

An end-to-end payment processing system demonstrating (✅ = shipped, 🔄 = in progress, ⏳ = upcoming):

- ✅ Stripe payment authorisation
- ✅ Azure Service Bus event messaging
- ✅ Transactional outbox pattern
- ✅ Idempotency and retry/circuit-breaker reliability
- ✅ Fraud rule engine (synchronous, in payment flow)
- ✅ Email notifications via SendGrid
- ✅ Structured JSON logging + OpenTelemetry distributed tracing
- ✅ Custom metrics + Azure Monitor alerts + runbook

## Stack

- **.NET 10** — ASP.NET Core minimal APIs, Clean Architecture per service
- **Azure Container Apps** — hosting, scale-to-zero
- **Azure SQL** — persistence (EF Core + Dapper)
- **Azure Service Bus** — event messaging (Standard, topics + subscriptions)
- **Stripe** — payment provider (sandbox)
- **SendGrid** — email delivery
- **Polly v8** — retry + circuit breaker
- **OpenTelemetry + Azure Monitor** — distributed tracing, custom metrics
- **Bicep** — infrastructure as code (including alerts + action groups)
- **GitHub Actions** — CI/CD (OIDC, ACR, ACA)

## Phases

| Phase | Goal | Status |
|-------|------|--------|
| 0 — Foundation | Repo, Azure, CI/CD, placeholder service | ✅ Done |
| 1 — Thin Vertical Slice | POST /payments → Stripe → SQL → Event → Notification | ✅ Done |
| 2 — Reliability | Outbox, idempotency, retry, circuit breaker | ✅ Done |
| 3 — Real System Feel | Fraud, email notifications, JSON logging, OTel, metrics + alerts | ✅ Code complete (demo validation pending) |
| 4 — Enterprise Hardening | Managed identity, private endpoints, blue-green | ⏳ Pending |

---

## Architecture

### System Overview

```mermaid
flowchart TB
    subgraph ext["External"]
        Client(["Client\nPostman / App"])
        Stripe(["Stripe\nPayment Provider\nsandbox"])
    end

    subgraph cicd["CI/CD — GitHub Actions"]
        CI["CI\nbuild · test · docker push\non every push to main"]
        CD["CD\naz containerapp update\ntriggered after CI passes"]
        CI --> CD
    end

    subgraph azure["Azure — fintech-dev-rg · East US"]
        ACR[("Container Registry\nfintechdevacr\nBasic SKU")]

        subgraph aca["ACA Environment — fintech-dev-aca-env"]
            PayAPI["Payment API\n────────────────\nASP.NET Core Minimal API\nClean Architecture\nMediatR · EF Core · FluentValidation\nPolly v8 retry + circuit breaker\nPort 8080 · public ingress"]
            OutboxRelay["Outbox Relay\n────────────────\nBackgroundService\n(inside Payment API)\npolls OutboxMessages every 5s"]
            FraudAPI["Fraud API\n────────────────\nRule engine + EF\nsync HTTP from Payment\ndecision logged"]
            NotifW["Notification Worker\n────────────────\nBackgroundService\nService Bus consumer\nSendGrid email\nNotificationLogs"]
        end

        subgraph store["Data & Messaging"]
            SQL[("Azure SQL\nfintech-payments\nPayments · PaymentAttempts\nOutboxMessages · IdempotencyKeys\nFraudDecisions · NotificationLogs\nServerless · auto-pause 60 min")]
            SB[("Service Bus\nfintech-dev-bus · Standard\ntopic: payment-authorized\nsubscription: notification")]
        end

        subgraph obs["Observability & Security"]
            AI["Application Insights\nfintech-dev-ai\nbacked by Log Analytics\n1 GB/day cap"]
            ALR["Metric Alerts\npayment-5xx-rate\nsb-dlq-depth\n→ email action group"]
            KV["Key Vault\nfintech-dev-kv\nRBAC mode\nactive from Phase 4"]
        end

        subgraph ext2["External (Phase 3)"]
            SendGrid(["SendGrid\nemail provider"])
        end
    end

    Client -->|"POST /payments\n+ Idempotency-Key\nHTTPS"| PayAPI
    PayAPI <-->|"PaymentIntent\ncreate + confirm"| Stripe
    PayAPI -->|"POST /fraud/evaluate\nsync, 5s timeout"| FraudAPI
    FraudAPI -->|"FraudDecisions write"| SQL
    PayAPI -->|"EF Core write\nPayments + OutboxMessages\n+ IdempotencyKeys\n(one transaction)"| SQL
    PayAPI -.->|"hosted in\nsame process"| OutboxRelay
    OutboxRelay -->|"poll pending\noutbox rows"| SQL
    OutboxRelay -->|"PaymentAuthorized.v1\njson event"| SB
    SB -->|"consume\nsubscription"| NotifW
    NotifW -->|"send email"| SendGrid
    NotifW -->|"NotificationLogs"| SQL
    PayAPI -->|"JSON logs · traces · metrics"| AI
    FraudAPI -->|"JSON logs · traces"| AI
    NotifW -->|"JSON logs · traces"| AI
    AI --> ALR
    CI -->|"az acr build\ncloud-side Docker build"| ACR
    ACR -.->|"AcrPull\nsystem-assigned\nmanaged identity"| PayAPI
    ACR -.->|"AcrPull\nsystem-assigned\nmanaged identity"| NotifW
    CD -->|"new revision"| PayAPI
    CD -->|"new revision"| NotifW
```

> **Key design decisions**
> - ACA scale-to-zero for both apps (min=0) — cost-saving for dev; outbox relay ensures no events are lost during downtime (messages stay Pending until relay comes back up)
> - Service Bus **Standard SKU** — minimum required for topics + subscriptions (Basic only has queues)
> - ACR auth via managed identity — no stored passwords anywhere in the pipeline
> - Azure SQL **Serverless** — auto-pauses after 60 min idle; expect 30-60 s cold-start on first connection
> - All secrets injected at runtime via ACA secret refs — never baked into images
> - Log Analytics capped at 1 GB/day to prevent runaway dev ingestion cost

---

### Phase 2 — Reliability Pattern ✅

Phase 1 published events directly inside the command handler — if the Service Bus call failed after the SQL write, the event was silently lost. Phase 2 replaces this with the **transactional outbox pattern** and adds full idempotency and resilience.

```mermaid
flowchart LR
    subgraph handler["Command Handler\nsame DB transaction"]
        W1["Write Payment\nto Payments table"]
        W2["Write event\nto OutboxMessages table\nStatus=Pending"]
        W3["Write IdempotencyKey\nwith response cached"]
        W1 --> W2
        W1 --> W3
    end

    subgraph relay["Outbox Relay\nBackgroundService · every 5s"]
        R1["Poll OutboxMessages\nWHERE Status=Pending"]
        R2["Publish to\nService Bus"]
        R3["Mark\nStatus=Published"]
        R1 --> R2 --> R3
    end

    subgraph cleanup["Outbox Cleanup\nBackgroundService · every 24h"]
        C1["Delete Published rows\nolder than 7 days"]
    end

    subgraph resilience["Stripe Resilience\nPolly v8"]
        P1["Retry\n3x exponential backoff\n1s · 2s · 4s + jitter"]
        P2["Circuit Breaker\n≥50% failures in 30s\n→ break 30s"]
        P1 --> P2
    end

    Client -->|"POST /payments\n+ Idempotency-Key"| IdempCheck
    IdempCheck{"IdempotencyKeys\ntable check"} -->|"same key\nsame hash"| CachedResp["Return cached\n201 response"]
    IdempCheck -->|"same key\ndiff hash"| Conflict["409 Conflict"]
    IdempCheck -->|"concurrent\nduplicate"| Conflict
    IdempCheck -->|"new key"| handler
    handler -->|"authorize"| resilience
    resilience -->|"Stripe call"| Stripe(["Stripe"])
    handler -->|"commit"| SQL[("Azure SQL")]
    R1 -->|"read"| SQL
    R3 -->|"update"| SQL
    C1 -->|"delete old rows"| SQL
    relay -->|"publish"| SB[("Service Bus")]
    SB -->|"consume"| NotifW["Notification\nWorker"]
```

> **What this buys**: event delivery is atomic with the payment write. Idempotency keys prevent duplicate charges. Polly retries recover from transient Stripe errors. The circuit breaker prevents cascade failures. Concurrent duplicate requests are safe via DB-level unique constraint.

---

## Phase 0 — Foundation ✅

**Goal:** Repo structure, Azure baseline, CI/CD pipeline, placeholder service.

| Ticket | Deliverable |
|--------|-------------|
| FT-001 | Repo structure, CLAUDE.md, branch workflow |
| FT-002 | Azure resource group, ACR, ACA environment, Service Bus, Key Vault, App Insights |
| FT-003 | Bicep modules for all infra resources |
| FT-004 | GitHub Actions CI (build + Docker push) |
| FT-005 | GitHub Actions CD (ACA deploy via OIDC) |
| FT-006 | Placeholder service running on ACA |

---

## Phase 1 — Thin Vertical Slice ✅

**Goal:** Full payment flow running end-to-end in Azure: API → Stripe → SQL → Service Bus → Notification.

| Ticket | Deliverable |
|--------|-------------|
| FT-101 | Payment.Api skeleton — minimal API, health endpoint, Clean Architecture layers |
| FT-102 | `POST /payments` contract + FluentValidation |
| FT-103 | Azure SQL + Payments table (EF Core migrations, auto-run on startup) |
| FT-104 | Stripe sandbox integration — PaymentIntent create+confirm, error classification |
| FT-105 | Happy path persistence — Stripe authorize + SQL write in one handler |
| FT-106 | `PaymentAuthorized.v1` event contract (shared/contracts/) |
| FT-107 | Service Bus publisher — `IEventPublisher` abstraction, `ServiceBusEventPublisher`, `NullEventPublisher` fallback |
| FT-108 | Notification.Worker — BackgroundService consuming `PaymentAuthorized.v1` from Service Bus topic |
| FT-109 | Deploy Payment + Notification to Azure Container Apps (Bicep + CD) |
| FT-110 | Postman collection, SQL validation script, `docs/demo/phase1.md` |
| FT-111 | Phase 1 demo verified twice end-to-end on ACA |

### Phase 1 — Live Endpoint

| Service | URL |
|---------|-----|
| Payment API | `https://payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io` |
| Health check | `GET /health` → `{"status":"ok","service":"payment"}` |
| Notification Worker | Internal ACA worker, no public ingress |

### Phase 1 — Demo

See [`docs/demo/phase1.md`](docs/demo/phase1.md) for step-by-step instructions.

---

## Phase 2 — Reliability ✅

**Goal:** Make the payment flow production-safe: transactional outbox, idempotency keys, payment attempt tracking, Polly retry + circuit breaker.

| Ticket | Deliverable |
|--------|-------------|
| FT-201 | Schema design doc — OutboxMessages, IdempotencyKeys, PaymentAttempts |
| FT-202 | PaymentAttempts table — tracks every provider call attempt |
| FT-203 | OutboxMessages table — EF config, filtered index on Status=Pending |
| FT-204 | Outbox Relay BackgroundService — polls every 5s, publishes, marks Published |
| FT-205 | Transactional handler — outbox write replaces direct Service Bus publish |
| FT-206 | Idempotency-Key header validation — required on all POST /payments |
| FT-207 | IdempotencyKeys table — store + replay cached 201 response |
| FT-208 | Concurrency-safe idempotency — DB unique index guard, 409 on race |
| FT-209 | Polly v8 retry — 3x exponential backoff, PaymentAttempts row per attempt |
| FT-210 | Polly v8 circuit breaker — singleton, 50% failure ratio, 30s break, state logging |
| FT-211 | Provider failure simulation — dev-only `Payment:SimulateTransientFailures` config flag |
| FT-212 | Outbox cleanup service — BackgroundService, 7-day retention, `ExecuteDeleteAsync` |
| FT-213 | Phase 2 demo guide — `docs/demo/phase2.md`, four runnable demo flows |

### Phase 2 — Demo

See [`docs/demo/phase2.md`](docs/demo/phase2.md) for four runnable demo flows: outbox relay, idempotency replay, concurrent idempotency, and retry + circuit breaker.

---

## Phase 3 — Real System Feel ✅

**Goal:** Round out the platform with fraud screening, email notifications, and a production-shaped observability story.

| Ticket | Deliverable |
|--------|-------------|
| FT-301 | Fraud.Api skeleton — Domain/Application/Infrastructure, decision contract, CI/CD inclusion |
| FT-302 | Fraud rule engine — score-based decisions, EF persistence, `POST /fraud/evaluate`, 7 unit tests |
| FT-303 | Fraud integration into payment flow — synchronous, graceful degradation, declines abort before Stripe |
| FT-304 | SendGrid email on `PaymentAuthorized` — `NotificationLogs` table, idempotent delivery, null fallback |
| FT-305 | Structured JSON logging shared formatter — required schema, traceId, propagation across all three services |
| FT-306 | OpenTelemetry tracing — W3C TraceContext across HTTP + Service Bus, App Insights exporter, `X-Trace-Id` response header |
| FT-307 | Custom metrics + alerts — 8 metrics via `FintechMeter`, Payment-5xx + SB-DLQ alerts, action group, runbook |
| FT-308 | Phase 3 demo verification + accelerator extraction |

### Phase 3 — Observability Architecture

```mermaid
flowchart LR
    subgraph services["Services emit"]
        PAY["Payment.Api"]
        FRD["Fraud.Api"]
        NOT["Notification.Worker"]
    end

    subgraph shared["shared/observability/Fintech.Observability"]
        JSON["FintechJsonConsoleFormatter\nrequired schema:\ntimestampUtc · level · service\ntraceId · operation · message"]
        OTEL["AddFintechTelemetry\nAspNetCore · HttpClient · SqlClient\n+ Azure SDK ActivitySource"]
        METER["FintechMeter\npayment.* + outbox.*\ncounters + histograms"]
    end

    subgraph azure["Azure"]
        AI["Application Insights\nfintech-dev-ai"]
        ALERTS["Metric Alerts\npayment-5xx-rate\nsb-dlq-depth"]
        AG["Action Group\nemail receiver"]
    end

    PAY & FRD & NOT --> JSON
    PAY & FRD & NOT --> OTEL
    PAY --> METER
    JSON --> AI
    OTEL --> AI
    METER --> AI
    AI --> ALERTS
    ALERTS --> AG
```

> **Trace coverage**: a single trace ID follows a payment through Payment → Fraud (HTTP) → SQL → Service Bus → Notification → SendGrid. Copy the `X-Trace-Id` response header to find the full transaction in App Insights.

### Phase 3 — Demo

See [`docs/demo/phase3.md`](docs/demo/phase3.md) for the six demo scenarios (success / failure+retry / idempotency / fraud / email / trace) and evidence checklist.

### Phase 3 — Accelerator Extraction

[`docs/accelerator/`](docs/accelerator/README.md) contains the reusable assets: `CLAUDE.md` template, Bicep modules guide, outbox + idempotency + observability patterns, phase template, and a frank list of known limitations.

---

## Getting Started

See `docs/demo/` for phase-specific demo instructions and `scripts/Validate-PaymentService.ps1` for automated local validation.

For ops triage, see [`docs/runbook.md`](docs/runbook.md).

## Rule of Thumb

Ship the simplest thing that proves the next demo. Architecture earns its complexity by paying for it in observable value.
