using MediatR;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    public Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        // Stub — Stripe authorization wired in FT-104
        return Task.FromResult(new CreatePaymentResult(
            PaymentId: Guid.NewGuid(),
            Status: "pending",
            Provider: "stripe",
            ProviderReference: null,
            CreatedAtUtc: DateTimeOffset.UtcNow
        ));
    }
}
