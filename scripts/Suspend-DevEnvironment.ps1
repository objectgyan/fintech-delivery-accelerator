#Requires -Version 7
<#
.SYNOPSIS
    Scales all ACA apps to zero replicas to stop compute costs.

.DESCRIPTION
    Suspends the dev environment by setting min/max replicas to 0 on both
    ACA apps. Config, secrets, and env vars are preserved — Resume brings
    everything back in seconds.

    What keeps running (minimal cost):
      - Service Bus namespace  ~$10/month base fee
      - ACR Basic              ~$5/month storage
      - Azure SQL              auto-pauses after 60 min idle
      - App Insights / KV      negligible

.EXAMPLE
    .\scripts\Suspend-DevEnvironment.ps1
#>

param(
    [string]$ResourceGroup = "fintech-dev-rg",
    [string]$PaymentApp    = "fintech-dev-payment",
    [string]$NotifApp      = "fintech-dev-notification"
)

$ErrorActionPreference = "Stop"

Write-Host "`n== Suspending dev environment ==" -ForegroundColor Cyan

Write-Host "`nScaling $PaymentApp to 0 replicas..."
az containerapp update `
    --name $PaymentApp `
    --resource-group $ResourceGroup `
    --min-replicas 0 `
    --max-replicas 0 `
    --output none

Write-Host "Scaling $NotifApp to 0 replicas..."
az containerapp update `
    --name $NotifApp `
    --resource-group $ResourceGroup `
    --min-replicas 0 `
    --max-replicas 0 `
    --output none

Write-Host "`n== Environment suspended ==" -ForegroundColor Green
Write-Host "Both ACA apps are at 0 replicas. No compute charges accruing."
Write-Host "Run .\scripts\Resume-DevEnvironment.ps1 to bring it back up.`n"
