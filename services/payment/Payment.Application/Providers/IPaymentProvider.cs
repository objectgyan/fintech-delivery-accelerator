namespace Payment.Application.Providers;

public interface IPaymentProvider
{
    Task<ProviderResult> AuthorizeAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
