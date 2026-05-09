using MediatR;

namespace Payment.Application.Commands.CreatePayment;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string PaymentMethodToken,
    string? Description,
    string? TraceId = null,
    string? IdempotencyKey = null,
    string? RequestHash = null
) : IRequest<CreatePaymentResult>;
