-- Phase 1 SQL Validation Script
-- Database: fintech-payments (Azure SQL, server: fintech-payments-sql)
-- Run after POST /payments to verify the payment row was persisted correctly.
--
-- Usage:
--   1. Run POST /payments and copy the returned paymentId
--   2. Paste it into @PaymentId below
--   3. Execute both queries

DECLARE @PaymentId UNIQUEIDENTIFIER = '<paste-payment-id-here>';

-- 1. Verify specific payment row
SELECT
    Id,
    Amount,
    Currency,
    CustomerEmail,
    Status,
    Provider,
    ProviderReference,
    CreatedAtUtc,
    UpdatedAtUtc
FROM Payments
WHERE Id = @PaymentId;

-- Expected: one row with Status='Authorized', Provider='stripe', ProviderReference starting with 'pi_'

-- 2. Show last 10 payments (useful for demo overview)
SELECT TOP 10
    Id,
    Amount,
    Currency,
    CustomerEmail,
    Status,
    Provider,
    ProviderReference,
    CreatedAtUtc
FROM Payments
ORDER BY CreatedAtUtc DESC;
