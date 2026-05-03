using System.Net;
using Microsoft.Extensions.Configuration;
using Payment.Application.Providers;
using Stripe;

namespace Payment.Infrastructure.Providers;

public sealed class StripePaymentProvider : IPaymentProvider
{
    private readonly PaymentIntentService _paymentIntentService;

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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Stripe uses smallest currency unit (cents)
                Currency = currency.ToLowerInvariant(),
                PaymentMethod = paymentMethodToken,
                Confirm = true,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            };

            var intent = await _paymentIntentService.CreateAsync(options, cancellationToken: cancellationToken);

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
}
