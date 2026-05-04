# FinTech Delivery Accelerator

A production-quality FinTech payment platform built as a reusable accelerator.

Solo backend engineer + Claude Code. Demo-first delivery across 5 phases. Target: working demo in ~6 weeks.

## What This Is

An end-to-end payment processing system demonstrating:
- Stripe payment authorisation
- Azure Service Bus event messaging
- Transactional outbox pattern
- Idempotency and retry/circuit-breaker reliability
- Fraud scoring
- Distributed tracing via OpenTelemetry + Application Insights

## Stack

- **.NET 10** — ASP.NET Core minimal APIs, Clean Architecture per service
- **Azure Container Apps** — hosting
- **Azure SQL** — persistence (EF Core + Dapper)
- **Azure Service Bus** — event messaging (Standard, topics + subscriptions)
- **Stripe** — payment provider (sandbox)
- **Bicep** — infrastructure as code
- **GitHub Actions** — CI/CD (OIDC, ACR, ACA)

## Phases

| Phase | Goal | Status |
|-------|------|--------|
| 0 — Foundation | Repo, Azure, CI/CD, placeholder service | ✅ Done |
| 1 — Thin Vertical Slice | POST /payments → Stripe → SQL → Event → Notification | ✅ Done |
| 2 — Reliability | Outbox, idempotency, retry, circuit breaker | 🔄 In progress |
| 3 — Real System Feel | Fraud, email, OpenTelemetry | ⏳ Pending |
| 4 — Enterprise Hardening | Managed identity, private endpoints, blue-green | ⏳ Pending |

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

### Phase 1 — Live Endpoints

| Service | URL |
|---------|-----|
| Payment API | `https://fintech-dev-payment.purplestone-2afd62bc.eastus.azurecontainerapps.io` |
| Health check | `GET /health` → `{"status":"ok","service":"payment"}` |
| Notification Worker | Internal ACA worker, no public ingress |

### Phase 1 — Demo

See [`docs/demo/phase1.md`](docs/demo/phase1.md) for step-by-step instructions.

Quick smoke test:
```bash
curl https://fintech-dev-payment.purplestone-2afd62bc.eastus.azurecontainerapps.io/health
```

### Phase 1 — Known Limitations (Phase 2 work)

| Limitation | Ticket |
|------------|--------|
| Events published directly — no transactional outbox (event lost if publish fails after DB write) | FT-203/205 |
| No idempotency — duplicate requests create duplicate payments | FT-204 |
| No retry on Stripe transient errors | FT-206 |
| No circuit breaker | FT-207 |
| Notification.Worker logs only — no email | FT-301 (Phase 3) |

---

## Phase 2 — Reliability 🔄

**Goal:** Make the payment flow production-safe: transactional outbox, idempotency keys, payment attempt tracking, Polly retry + circuit breaker.

*In progress — see Linear for current ticket status.*

---

## Getting Started

See `docs/demo/` for phase-specific demo instructions and `scripts/Validate-PaymentService.ps1` for automated local validation.

## Rule of Thumb

Ship the simplest thing that proves the next demo. Architecture earns its complexity by paying for it in observable value.
