# CLAUDE.md - FinTech Delivery Accelerator

## Current Phase
**Phase 0 - Foundation Setup**

We are establishing the deployment pipeline and cloud infrastructure.
Do NOT add: outbox, idempotency, OpenTelemetry, fraud, retries, structured
logging, or any Phase 1+ patterns yet.

## Complexity Guardrail
Before adding any new component, answer:
1. Is this required for the current phase demo?
2. Does it unblock a current ticket?
3. Is there a simpler option?

If no to (1) and (2), do not implement it yet. Never implement features
from future phases unless the ticket explicitly says so.

## Project Context
A FinTech payment platform built as a reusable accelerator. Solo backend
engineer + Claude Code. Demo-first delivery. Six weeks to working demo.
Eventual reuse target: Insurance, Healthcare, AECO verticals.

## Stack & Conventions
- .NET 8, C# 12, ASP.NET Core minimal APIs
- Clean Architecture per service: Api / Application / Domain / Infrastructure
- CQRS via MediatR for command/query separation
- EF Core for migrations and writes; Dapper for read-heavy queries
- FluentValidation for request validation
- Polly v8 for retry / circuit breaker (Phase 2+)
- xUnit + FluentAssertions for tests
- Bicep modules in infra/. One module per resource type.
- Secret naming: FINTECH--SERVICE--SETTING

## Definition of Ready
- Goal, Scope, AC, Validation, Evidence filled in
- Dependencies complete or mocked
- Current Phase correct in this file
- No future-phase work unless explicitly approved
- Required secrets/accounts available or mock path defined

## Definition of Done
- Implementation matches scope only
- Build green locally and in CI
- Validation complete (unit/integration/API as appropriate)
- Deploy to active environment succeeds for deployable changes
- Evidence posted to Linear (output, screenshot, trace ID, log)
- Engineer verifies evidence before marking Done

## Ask Before Doing
- Database migrations beyond simple table creation
- Cost-impacting Azure SKU upgrades
- Authentication or networking changes
- Breaking API or event schema changes
- Adding new top-level dependencies
- Any future-phase work

## Working Style
- Generate a short plan first; engineer approves before coding
- Prefer small PRs over large ones
- When unsure, ask in chat before writing speculative code
- Trailer commits with: Co-authored-by: claude-code[bot]
- Never log secrets, card-like data, or raw stack traces
