# Accelerator — Reusable Assets

This folder is the extracted, vertical-agnostic core of the FinTech Delivery Accelerator. Use it as the starting point when bootstrapping a similar delivery (Insurance, Healthcare, AECO).

## Contents

| File | What it gives you |
|------|-------------------|
| [`CLAUDE.md.template`](CLAUDE.md.template) | Skeleton `CLAUDE.md` for a new project — fill in stack, phases, ask-before list |
| [`bicep-modules.md`](bicep-modules.md) | Per-resource Bicep guide — what each module does, what to tweak per vertical |
| [`phase-template.md`](phase-template.md) | Boilerplate Linear ticket layout — Goal / Scope / AC / Validation / Evidence |
| [`patterns/outbox.md`](patterns/outbox.md) | Transactional outbox pattern with code references |
| [`patterns/idempotency.md`](patterns/idempotency.md) | Idempotency keys + concurrency safety |
| [`patterns/observability.md`](patterns/observability.md) | JSON logging + OTel + metrics + alerts wiring |
| [`known-limitations.md`](known-limitations.md) | Sharp edges to fix before a vertical goes to production |

## How to fork

1. Copy the `infra/` folder — tweak `dev.bicepparam` for the new project prefix
2. Copy `shared/observability/Fintech.Observability` — rename namespace to match the new domain
3. Copy `services/payment/` as a template for any vertical's primary transaction service (rename, repurpose Stripe → vertical-appropriate provider)
4. Drop in this folder's `CLAUDE.md.template` and fill it out
5. Open the first ticket from `phase-template.md` and follow the working style
