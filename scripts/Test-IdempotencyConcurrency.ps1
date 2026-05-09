<#
.SYNOPSIS
    Fires N concurrent POST /payments requests with the same Idempotency-Key.
    Validates that exactly one payment is created and all responses are 201 or 409.

.PARAMETER BaseUrl
    Base URL of the Payment API (default: https://payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io)

.PARAMETER Concurrency
    Number of simultaneous requests (default: 15)

.PARAMETER PaymentMethodToken
    Stripe test card token (default: tok_visa)

.EXAMPLE
    .\Test-IdempotencyConcurrency.ps1
    .\Test-IdempotencyConcurrency.ps1 -BaseUrl http://localhost:5000 -Concurrency 20
#>
param(
    [string]$BaseUrl = "https://payment-api.gentlerock-6e5c3f99.eastus.azurecontainerapps.io",
    [int]$Concurrency = 15,
    [string]$PaymentMethodToken = "tok_visa"
)

$ErrorActionPreference = "Stop"

$sharedKey = [System.Guid]::NewGuid().ToString()
$endpoint  = "$BaseUrl/payments"

$body = @{
    amount             = 42.00
    currency           = "USD"
    customerEmail      = "concurrency-test@example.com"
    paymentMethodToken = $PaymentMethodToken
    description        = "Concurrency idempotency test"
} | ConvertTo-Json

Write-Host ""
Write-Host "Idempotency concurrency test" -ForegroundColor Cyan
Write-Host "  Endpoint   : $endpoint"
Write-Host "  Key        : $sharedKey"
Write-Host "  Concurrency: $Concurrency requests"
Write-Host ""

# Fire all requests in parallel using runspaces for true concurrency
$pool = [runspacefactory]::CreateRunspacePool(1, $Concurrency)
$pool.Open()

$jobs = 1..$Concurrency | ForEach-Object {
    $ps = [powershell]::Create()
    $ps.RunspacePool = $pool
    [void]$ps.AddScript({
        param($url, $key, $payload)
        try {
            $response = Invoke-WebRequest -Uri $url -Method POST `
                -ContentType "application/json" `
                -Headers @{ "Idempotency-Key" = $key } `
                -Body $payload `
                -SkipHttpErrorCheck
            [PSCustomObject]@{
                StatusCode = [int]$response.StatusCode
                Body       = $response.Content
                Error      = $null
            }
        } catch {
            [PSCustomObject]@{
                StatusCode = 0
                Body       = $null
                Error      = $_.Exception.Message
            }
        }
    })
    [void]$ps.AddParameters(@{ url = $endpoint; key = $sharedKey; payload = $body })
    @{ PS = $ps; Handle = $ps.BeginInvoke() }
}

# Collect results
$results = $jobs | ForEach-Object {
    $r = $_.PS.EndInvoke($_.Handle)
    $_.PS.Dispose()
    $r
}
$pool.Close()

# Analyse
$created  = $results | Where-Object { $_.StatusCode -eq 201 }
$conflict = $results | Where-Object { $_.StatusCode -eq 409 }
$errors   = $results | Where-Object { $_.StatusCode -notin @(201, 409) }

Write-Host "Results summary" -ForegroundColor Cyan
Write-Host "  201 Created : $($created.Count)"
Write-Host "  409 Conflict: $($conflict.Count)"
Write-Host "  Other       : $($errors.Count)"

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Unexpected responses:" -ForegroundColor Yellow
    $errors | ForEach-Object { Write-Host "  HTTP $($_.StatusCode) — $($_.Error ?? $_.Body)" }
}

# Validation
$pass = $true

if ($created.Count -ne 1) {
    Write-Host ""
    Write-Host "FAIL: Expected exactly 1 x 201 Created, got $($created.Count)" -ForegroundColor Red
    $pass = $false
}

if ($errors.Count -gt 0) {
    Write-Host "FAIL: $($errors.Count) unexpected status codes (expected only 201/409)" -ForegroundColor Red
    $pass = $false
}

if ($created.Count -eq 1) {
    $paymentId = ($created[0].Body | ConvertFrom-Json).paymentId
    Write-Host ""
    Write-Host "Payment ID: $paymentId" -ForegroundColor Green
    Write-Host ""
    Write-Host "Verify with:"
    Write-Host "  SELECT COUNT(*) FROM Payments WHERE Id = '$paymentId'  -- expect 1"
    Write-Host "  SELECT COUNT(*) FROM IdempotencyKeys WHERE [Key] = '$sharedKey'  -- expect 1"
}

if ($pass) {
    Write-Host ""
    Write-Host "PASS: Concurrency idempotency validated" -ForegroundColor Green
    exit 0
} else {
    exit 1
}
