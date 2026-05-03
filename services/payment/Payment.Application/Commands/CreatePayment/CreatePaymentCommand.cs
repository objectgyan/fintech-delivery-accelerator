using MediatR;

namespace Payment.Application.Commands.CreatePayment;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string PaymentMethodToken,
    string? Description
) : IRequest<CreatePaymentResult>;
