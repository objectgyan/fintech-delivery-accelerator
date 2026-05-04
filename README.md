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
- **Azure Service Bus** — event messaging
- **Stripe** — payment provider (sandbox)
- **Bicep** — infrastructure as code
- **GitHub Actions** — CI/CD

## Phases

| Phase | Goal | Status |
|-------|------|--------|
| 0 — Foundation | Repo, Azure, CI/CD, dummy service | ✅ Done |
| 1 — Thin Vertical Slice | POST /payments → Stripe → SQL → Event → Notification | ✅ Done |
| 2 — Reliability | Outbox, idempotency, retry, circuit breaker | 🔄 In progress |
| 3 — Real System Feel | Fraud, email, OpenTelemetry | ⏳ |
| 4 — Enterprise Hardening | Managed identity, private endpoints, blue-green (optional) | ⏳ |

## Getting Started

See `docs/demo/` for phase-specific demo instructions.

## Rule of Thumb

Ship the simplest thing that proves the next demo. Architecture earns its complexity by paying for it in observable value.
