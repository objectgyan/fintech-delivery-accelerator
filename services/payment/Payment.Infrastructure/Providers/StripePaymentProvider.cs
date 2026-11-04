using System.Net;
using Microsoft.Extensions.Configuration;
using Payment.Application.Providers;
using Stripe;

namespace Payment.Infrastructure.Providers;

public sealed class StripePaymentProvider : IPaymentProvider
{
    private readonly PaymentIntentService _paymentIntentService;

    // https://docs.stripe.com/currencies#zero-decimal
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA",
        "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    public StripePaymentProvider(IConfiguration configuration)
    {
        var apiKey = configuration["Stripe:SecretKey"]
            ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");
        _paymentIntentService = new PaymentIntentService(new StripeClient(apiKey));
    }

    public async Task<ProviderResult> AuthorizeAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = ToMinorUnits(amount, currency),
                Currency = currency.ToLowerInvariant(),
                PaymentMethod = paymentMethodToken,
                Confirm = true,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            };

            var intent = await _paymentIntentService.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = idempotencyKey },
                cancellationToken);

            return intent.Status == "succeeded"
                ? new ProviderResult(true, intent.Id, null, null, false)
                : new ProviderResult(false, null, "payment_failed", $"Unexpected status: {intent.Status}", false);
        }
        catch (StripeException ex) when ((int)ex.HttpStatusCode >= 500)
        {
            return new ProviderResult(false, null, ex.StripeError?.Code, ex.Message, true);
        }
        catch (StripeException ex)
        {
            return new ProviderResult(false, null, ex.StripeError?.Code, ex.StripeError?.Message ?? ex.Message, false);
        }
    }

    // Stripe expects the smallest currency unit; round rather than truncate (19.999 -> 2000, not 1999)
    internal static long ToMinorUnits(decimal amount, string currency)
    {
        var factor = ZeroDecimalCurrencies.Contains(currency) ? 1m : 100m;
        return (long)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
    }
}
