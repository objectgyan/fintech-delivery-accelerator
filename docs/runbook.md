# FinTech Delivery Accelerator — Runbook

Operational playbook for the on-call engineer. Each alert below corresponds to a metric alert defined in `infra/modules/alerts.bicep`.

---

## Alert: `fintech-{env}-payment-5xx-rate`

**Trigger:** ≥ 5 failed HTTP requests (5xx) recorded by App Insights against the Payment API in any 5-minute window.

### First 5 minutes — triage

1. Open the App Insights **Failures** blade for `fintech-{env}-ai`. Sort by *Top 5 failures* — confirm whether the spike is Stripe-related, SQL-related, or something else.
2. Copy a recent `traceId` from the failure table and open the **End-to-end transaction** view. Look at which span actually threw — Stripe call, SQL write, Service Bus publish, or fraud HTTP call.
3. Check Stripe status: <https://status.stripe.com>. If Stripe is degraded, retries + circuit-breaker should be absorbing some of it — confirm by checking the `payment.retry.count` and `payment.stripe_error.count` metrics under the Custom Metrics tab.

### Likely causes & first remediations

| Symptom | Likely cause | First action |
|---------|--------------|--------------|
| Spike in `BrokenCircuitException` | Stripe outage / network partition | Wait 30 s — circuit re-probes automatically. If sustained > 5 min, page Stripe support. |
| Spike in `IDX10500`-style auth errors | Stripe key rotated/revoked | Verify `Stripe:SecretKey` secret in ACA. Rotate via Stripe dashboard if compromised. |
| Spike in SQL timeouts | Azure SQL serverless cold-start (60-min pause) | Expected on first call after pause. If sustained, scale up tier temporarily. |
| `FraudDeclinedException` rate up | Fraud rule overly aggressive OR genuine attack | Check `Fraud:BlockedDomains` config. Pull recent FraudDecisions for pattern. |

### KQL queries

App Insights `Logs` blade:

```kql
// Top failures in last hour
requests
| where timestamp > ago(1h) and success == false
| summarize count() by resultCode, name
| order by count_ desc
```

```kql
// Failure rate by minute
requests
| where timestamp > ago(1h)
| summarize total=count(), failed=countif(success == false) by bin(timestamp, 1m)
| extend failureRate = todouble(failed) / total
| render timechart
```

---

## Alert: `fintech-{env}-sb-dlq-depth`

**Trigger:** Any message lands in the Service Bus dead-letter queue on the `notification` subscription (DLQ depth > 0 over 15 min).

### First 5 minutes — triage

1. Open the Service Bus namespace `fintech-{env}-bus` → Topic `payment-authorized` → Subscription `notification` → **Dead-letter messages**.
2. Peek the top DLQ message — check `DeadLetterReason` and `DeadLetterErrorDescription` in user properties.
3. Copy `paymentId` from the message body and pull the corresponding `Payments` row.

### Likely causes & first remediations

| `DeadLetterReason` | Likely cause | First action |
|---------------------|--------------|--------------|
| `MaxDeliveryCountExceeded` | Consumer throws repeatedly (deserialization, downstream failure) | Find the exception in App Insights for `Notification.Worker` matching the message timestamp. Fix root cause, re-enqueue from DLQ. |
| `HeaderSizeExceeded` | Producer sending oversized message | Check `OutboxMessages.PayloadJson` size. If a payload is bloated, fix at source. |

### Replay procedure

After fixing the consumer bug, drain DLQ back to the active subscription:

```bash
# Use ServiceBusExplorer or the SDK — no built-in az CLI for DLQ replay.
# Demo accelerator uses a small script at scripts/Replay-Dlq.ps1 (TBD when first needed).
```

---

## Custom metrics quick reference

All emitted via `Fintech.Application` Meter, scraped by Azure Monitor.

| Metric | Type | Where emitted | Used in alert? |
|--------|------|---------------|----------------|
| `payment.request.count` | Counter | `CreatePaymentCommandHandler` entry | No (informational) |
| `payment.success.count` | Counter | After `Authorize()` | No |
| `payment.failure.count` | Counter | All failure paths (tagged by `reason`) | Future |
| `payment.latency.ms` | Histogram | End of every request | Future — P95 alert |
| `payment.stripe_error.count` | Counter | Polly retry + final failure | No |
| `payment.retry.count` | Counter | Polly `OnRetry` callback | No |
| `payment.outbox.published.count` | Counter | `OutboxRelayService` success path | No |
| `payment.outbox.publish_failed.count` | Counter | `OutboxRelayService` catch | No |

Service Bus `DeadletteredMessages` is a platform metric — no app code needed.

---

## Escalation

- Stripe / payment provider outage → @objectgyan (Slack DM), then Stripe support
- Azure regional outage → check <https://azure.status.microsoft>; failover not implemented in dev
- Anything not covered above → page the engineer on call
