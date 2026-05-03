using MediatR;
using Payment.Application.Exceptions;
using Payment.Application.Providers;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IPaymentProvider _provider;

    public CreatePaymentCommandHandler(IPaymentProvider provider)
    {
        _provider = provider;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var result = await _provider.AuthorizeAsync(
            request.Amount,
            request.Currency,
            request.PaymentMethodToken,
            cancellationToken);

        if (!result.Success)
            throw new PaymentProviderException(result.ErrorCode, result.ErrorMessage, result.IsTransient);

        // TODO: Phase 1 (FT-105) — persist to DB before returning
        return new CreatePaymentResult(
            PaymentId: Guid.NewGuid(),
            Status: "authorized",
            Provider: "stripe",
            ProviderReference: result.ProviderReference,
            CreatedAtUtc: DateTimeOffset.UtcNow
        );
    }
}
