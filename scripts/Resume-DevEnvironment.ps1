#Requires -Version 7
<#
.SYNOPSIS
    Restores ACA apps to their normal running replica counts.

.DESCRIPTION
    Resumes the dev environment after a Suspend. Restores:
      - Payment API     min=0 max=3  (scales to zero when idle, up on first request)
      - Notification    min=1 max=3  (always running to consume Service Bus messages)

    Optionally triggers a CD re-deploy to ensure the latest image is running.

.PARAMETER TriggerDeploy
    Re-run the latest successful CD workflow run after scaling up.

.EXAMPLE
    .\scripts\Resume-DevEnvironment.ps1
    .\scripts\Resume-DevEnvironment.ps1 -TriggerDeploy
#>

param(
    [string]$ResourceGroup = "fintech-dev-rg",
    [string]$PaymentApp    = "fintech-dev-payment",
    [string]$NotifApp      = "fintech-dev-notification",
    [switch]$TriggerDeploy
)

$ErrorActionPreference = "Stop"

Write-Host "`n== Resuming dev environment ==" -ForegroundColor Cyan

Write-Host "`nRestoring $PaymentApp (min=0, max=3)..."
az containerapp update `
    --name $PaymentApp `
    --resource-group $ResourceGroup `
    --min-replicas 0 `
    --max-replicas 3 `
    --output none

Write-Host "Restoring $NotifApp (min=1, max=3)..."
az containerapp update `
    --name $NotifApp `
    --resource-group $ResourceGroup `
    --min-replicas 1 `
    --max-replicas 3 `
    --output none

if ($TriggerDeploy) {
    Write-Host "`nTriggering latest CD run to redeploy current images..."
    $latestRun = gh run list --workflow=cd.yml --limit 1 --json databaseId --jq ".[0].databaseId"
    gh run rerun $latestRun --failed
    Write-Host "CD run $latestRun triggered. Monitor with: gh run watch $latestRun"
}

Write-Host "`n== Environment resumed ==" -ForegroundColor Green

$paymentUrl = az containerapp show `
    --name $PaymentApp `
    --resource-group $ResourceGroup `
    --query "properties.configuration.ingress.fqdn" -o tsv

Write-Host "Payment API: https://$paymentUrl"
Write-Host "Note: Payment API scales from zero — first request after idle takes ~5-10s.`n"
