# Known Limitations

Honest list of sharp edges in the accelerator as of Phase 3 close. These are not bugs — they're conscious deferrals. Fix before any vertical goes to production.

## Infra

- **Azure SQL is not in Bicep.** Server `objectgyan.database.windows.net` was provisioned manually in Phase 0 (and lives in West US 2 while everything else is East US). Adding it to `infra/main.bicep` is deferred because the next deploy would risk creating a duplicate. Phase 4 work.
- **Key Vault is empty.** Secrets live in ACA env-var refs and GitHub Secrets today. Phase 4 will move them into KV with managed-identity reads.
- **No private endpoints / VNet integration.** All resources are reachable on public endpoints, secured by RBAC. Phase 4+.
- **Single environment.** Only `dev` is parameterised. `staging` / `prod` parameters exist in name but no pipeline deploys to them.

## Security

- **No authentication on Payment API.** The endpoint is public. Add API key middleware or Entra ID auth before exposing externally.
- **No PCI scope.** Stripe card tokens are accepted but no PCI-DSS controls (logging filters, network segmentation, key rotation) are implemented.
- **Card-like data filter is a convention, not a guard.** The JSON logger doesn't actively redact — it just doesn't include card fields. A bug logging `customer.card.number` would leak. Add an active redactor before production.

## Reliability

- **Email retries are not implemented.** SendGrid failure is logged to `NotificationLogs` but not retried. Phase 4: Polly around SendGrid + DLQ-replay for the message.
- **Outbox relay polling is fixed at 5 s.** Under burst load that's a 5 s P99 floor on event delivery. Adjustable in code but not configurable.
- **No multi-region failover.** A single regional Azure outage takes the whole system down.
- **Stripe circuit breaker is per-instance.** Each ACA replica has its own breaker — no shared state. Acceptable for the demo, may surprise at scale.

## Observability

- **App Insights ingestion cap is 1 GB/day.** Sized for dev. Re-evaluate before any prod load — and add an alert on ingestion approaching the cap.
- **No log redaction filter.** Relies on developer discipline (don't log secrets). Add a `ILogEnricher` filter before any vertical accepts user-supplied free-text fields.
- **No SLOs defined.** "Payment 5xx alert" fires on 5 errors / 5 min but there's no documented SLO (e.g., 99.5% success). Phase 4 work.

## Process

- **`Suspend-DevEnvironment.ps1` / `Resume-DevEnvironment.ps1`** exist but are not automated. Engineer remembers to suspend ACA replicas + pause SQL manually.
- **Fallback demo recording must be captured manually** every time scope changes.
- **No load testing.** Nothing in the project has been hit with sustained concurrent load. Latency numbers in the demo are single-request only.

## Documentation

- **README architecture diagram lags reality.** Per CLAUDE.md DoD it must be updated when services/tables change. Easy to forget — consider a CI check that diffs the diagram against `infra/main.bicep` modules.
