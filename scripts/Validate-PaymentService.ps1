#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end validation script for the Payment service.

.DESCRIPTION
    Runs all validation scenarios against a locally running Payment.Api instance:
      1. Health check
      2. Happy path (pm_card_visa) — verifies 201 + SQL row
      2c. Second consecutive payment — verifies singleton ServiceBusClient is not disposed between requests
      3. Declined card (pm_card_chargeDeclined) — verifies 402
      4. Invalid request body — verifies 400 + validation errors

.PARAMETER BaseUrl
    Base URL of the Payment.Api. Defaults to http://localhost:5101

.PARAMETER SqlConnectionString
    Azure SQL connection string. If omitted, read from user secrets
    (UserSecretsId: 58feadca-517d-4083-8762-2b5964656567).

.EXAMPLE
    .\Validate-PaymentService.ps1
    .\Validate-PaymentService.ps1 -BaseUrl http://localhost:5101
#>
param(
    [string]$BaseUrl = "http://localhost:5101",
    [string]$SqlConnectionString = ""
)

$ErrorActionPreference = "Stop"
$passed = 0
$failed = 0

function Write-Pass([string]$msg) { Write-Host "  PASS  $msg" -ForegroundColor Green; $script:passed++ }
function Write-Fail([string]$msg) { Write-Host "  FAIL  $msg" -ForegroundColor Red;  $script:failed++ }
function Write-Section([string]$msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

# ── Resolve SQL connection string ────────────────────────────────────────────
if ([string]::IsNullOrEmpty($SqlConnectionString)) {
    $secretsPath = "$env:APPDATA\Microsoft\UserSecrets\58feadca-517d-4083-8762-2b5964656567\secrets.json"
    if (Test-Path $secretsPath) {
        $secrets = Get-Content $secretsPath | ConvertFrom-Json
        $SqlConnectionString = $secrets."ConnectionStrings:Payments"
    }
}

$hasSql = -not [string]::IsNullOrEmpty($SqlConnectionString)

# ── TEST 1: Health ────────────────────────────────────────────────────────────
Write-Section "TEST 1: GET /health"
try {
    $r = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 10
    if ($r.status -eq "ok" -and $r.service -eq "payment") {
        Write-Pass "status=ok  service=payment"
    } else {
        Write-Fail "Unexpected response: $($r | ConvertTo-Json -Compress)"
    }
} catch {
    Write-Fail "Request failed: $($_.Exception.Message)"
}

# ── TEST 2: Happy path ────────────────────────────────────────────────────────
Write-Section "TEST 2: POST /payments (pm_card_visa) → 201"
$happyPaymentId = $null
$happyProviderRef = $null
try {
    $body = @{
        amount             = 5000
        currency           = "USD"
        customerEmail      = "validate@example.com"
        paymentMethodToken = "pm_card_visa"
        description        = "Validation script test"
    } | ConvertTo-Json

    $r = Invoke-RestMethod -Uri "$BaseUrl/payments" -Method POST -ContentType "application/json" -Body $body -TimeoutSec 60
    $happyPaymentId   = $r.paymentId
    $happyProviderRef = $r.providerReference

    if ($r.status -eq "authorized" -and $r.provider -eq "stripe" -and $r.providerReference -like "pi_*") {
        Write-Pass "paymentId=$happyPaymentId  providerReference=$happyProviderRef  status=$($r.status)"
    } else {
        Write-Fail "Unexpected response: $($r | ConvertTo-Json -Compress)"
    }
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Write-Fail "HTTP $code — $($_.ErrorDetails.Message)"
}

# ── TEST 2b: SQL row verification ─────────────────────────────────────────────
Write-Section "TEST 2b: SQL row for paymentId=$happyPaymentId"
if (-not $hasSql) {
    Write-Host "  SKIP  No SQL connection string available" -ForegroundColor Yellow
} elseif ($null -eq $happyPaymentId) {
    Write-Host "  SKIP  Happy path failed — no paymentId to query" -ForegroundColor Yellow
} else {
    try {
        $conn = New-Object System.Data.SqlClient.SqlConnection($SqlConnectionString)
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT Id, Amount, Currency, Status, Provider, ProviderReference FROM Payments WHERE Id = @id"
        $cmd.Parameters.AddWithValue("@id", $happyPaymentId) | Out-Null
        $reader = $cmd.ExecuteReader()
        if ($reader.Read()) {
            $dbRef = $reader["ProviderReference"]
            if ($dbRef -eq $happyProviderRef) {
                Write-Pass "Row found — ProviderReference matches API response ($dbRef)"
            } else {
                Write-Fail "ProviderReference mismatch: DB=$dbRef  API=$happyProviderRef"
            }
        } else {
            Write-Fail "No row found in Payments for Id=$happyPaymentId"
        }
        $reader.Close()
        $conn.Close()
    } catch {
        Write-Fail "SQL error: $_"
    }
}

# ── TEST 2c: Second consecutive payment (guards against singleton dispose bug) ─
Write-Section "TEST 2c: POST /payments (second consecutive) → 201"
try {
    $body = @{
        amount             = 25.00
        currency           = "USD"
        customerEmail      = "validate2@example.com"
        paymentMethodToken = "pm_card_visa"
        description        = "Consecutive payment test"
    } | ConvertTo-Json

    $r = Invoke-RestMethod -Uri "$BaseUrl/payments" -Method POST -ContentType "application/json" -Body $body -TimeoutSec 60
    if ($r.status -eq "authorized" -and $r.providerReference -like "pi_*") {
        Write-Pass "Second payment succeeded — paymentId=$($r.paymentId)  ref=$($r.providerReference)"
    } else {
        Write-Fail "Unexpected response: $($r | ConvertTo-Json -Compress)"
    }
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Write-Fail "HTTP $code — $($_.ErrorDetails.Message) (ServiceBusClient may have been disposed after first request)"
}

# ── TEST 3: Declined card → 402 ───────────────────────────────────────────────
Write-Section "TEST 3: POST /payments (pm_card_chargeDeclined) → 402"
try {
    $body = @{
        amount             = 5000
        currency           = "USD"
        customerEmail      = "validate@example.com"
        paymentMethodToken = "pm_card_chargeDeclined"
        description        = "Decline test"
    } | ConvertTo-Json

    Invoke-RestMethod -Uri "$BaseUrl/payments" -Method POST -ContentType "application/json" -Body $body -TimeoutSec 30 | Out-Null
    Write-Fail "Expected 402 but got 2xx"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $body = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
    if ($code -eq 402 -and $body.code -eq "card_declined") {
        Write-Pass "HTTP 402  code=card_declined"
    } else {
        Write-Fail "Expected HTTP 402 card_declined, got HTTP $code — $($_.ErrorDetails.Message)"
    }
}

# ── TEST 4: Invalid request → 400 ────────────────────────────────────────────
Write-Section "TEST 4: POST /payments (invalid body) → 400"
try {
    $body = @{ amount = 0; currency = "XX"; customerEmail = "bad" } | ConvertTo-Json

    Invoke-RestMethod -Uri "$BaseUrl/payments" -Method POST -ContentType "application/json" -Body $body -TimeoutSec 10 | Out-Null
    Write-Fail "Expected 400 but got 2xx"
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    $errBody = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
    if ($code -eq 400 -and $errBody.errors) {
        $errKeys = ($errBody.errors.PSObject.Properties.Name) -join ", "
        Write-Pass "HTTP 400  validation errors: $errKeys"
    } else {
        Write-Fail "Expected HTTP 400 with errors, got HTTP $code — $($_.ErrorDetails.Message)"
    }
}

# ── Summary ───────────────────────────────────────────────────────────────────
$total = $passed + $failed
Write-Host "`n$('─' * 50)"
Write-Host "Results: $passed/$total passed" -ForegroundColor ($failed -eq 0 ? "Green" : "Yellow")
if ($failed -gt 0) {
    Write-Host "$failed test(s) FAILED" -ForegroundColor Red
    exit 1
}
