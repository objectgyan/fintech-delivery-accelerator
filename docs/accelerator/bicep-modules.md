# Bicep Modules Guide

Each file in `infra/modules/` is a single-resource module. `main.bicep` composes them. This keeps every resource definition reusable across verticals.

## Module catalogue

| Module | What it provisions | Tweak per vertical |
|--------|--------------------|----------------------|
| `log-analytics.bicep` | Workspace, 30-day retention, **1 GB/day daily cap** | Raise cap if ingestion is heavier; production probably wants 5–10 GB/day to start |
| `app-insights.bicep` | Workspace-based App Insights component | None — keep as workspace-based |
| `acr.bicep` | Azure Container Registry, Basic SKU, admin disabled | Bump to Standard if you need geo-replication or content trust |
| `key-vault.bicep` | Key Vault in **RBAC mode** | RBAC is correct — do not switch back to access policies |
| `service-bus.bicep` | Standard SKU namespace + topic + subscription | Standard is the minimum for topics. Premium adds VNet + dedicated capacity (Phase 4+) |
| `aca-environment.bicep` | ACA managed environment, log-forwarding wired to Log Analytics | None |
| `aca-app.bicep` | Web container app — external ingress, port 8080 | Per-service replicas and env vars |
| `aca-worker.bicep` | Background container app — **no ingress** | Per-service replicas and env vars |
| `sql.bicep` | Azure SQL serverless DB (auto-pause 60 min) | **Currently not called from main** — server lives outside Bicep. Phase 4 work to merge cleanly. |
| `alerts.bicep` | Action group + 2 metric alerts (payment 5xx, SB DLQ) | Tune thresholds; add more alerts as new metrics land |

## Conventions

- **One resource per module.** Composing multiple resources in one module is a code smell — split.
- **Outputs are explicit.** Each module emits the IDs and names downstream modules need (never use string interpolation against another module's name).
- **Tags propagate.** `main.bicep` passes a `tags` object into every module. Don't redefine tags inside modules.
- **`namePrefix` is `{projectPrefix}-{environment}`.** Resource names compose from that — never hard-code environment in module logic.

## Scale-to-zero pattern

Both `aca-app.bicep` and `aca-worker.bicep` accept `minReplicas` and `maxReplicas`. Default to `min=0, max=3` for dev. Production should set `min=1` for primary services so cold-start latency doesn't hit users.

Outbox relay (inside the payment service) is the safety net — events stay `Pending` in SQL until a replica wakes up, so scale-to-zero loses no work.

## When to add a new module

Add a new module file when:
- You're provisioning a resource type that doesn't exist yet
- An existing module is being asked to do two things (split it)

Don't add a module for:
- Variations of an existing resource (use parameters instead)
- One-off resources that don't fit the "one per type" pattern (add directly in `main.bicep` only as a last resort, with a comment explaining why)
