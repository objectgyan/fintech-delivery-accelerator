# CLAUDE.md - FinTech Delivery Accelerator

## Current Phase
**Phase 3 — Real System Feel** (code complete, pending demo validation)

Phase 2 (Reliability) is complete. Phase 3 code is shipped across PRs #26–#32: Fraud service (rule engine + payment integration), email notifications via SendGrid, structured JSON logging, OpenTelemetry distributed tracing, custom metrics + Azure Monitor alerts, runbook, and the Phase 3 demo script + accelerator extraction docs. Phase 3 stays "in review" until the engineer runs the full 6-scenario demo twice and captures evidence (see `docs/demo/phase3.md`).

Do NOT add: managed identity, private endpoints, APIM, blue-green, or any Phase 4+ patterns yet.

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
- .NET 10, C# 14, ASP.NET Core minimal APIs
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
- **README architecture diagrams updated** if this ticket adds/changes a service, table, infra resource, or data flow

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
- No Co-authored-by or attribution trailers in commit messages
- Never log secrets, card-like data, or raw stack traces

## Branch Workflow
- Every ticket gets its own feature branch before any files are touched
- Branch name comes from the Linear issue's `gitBranchName` field exactly
- Never commit directly to `main`
