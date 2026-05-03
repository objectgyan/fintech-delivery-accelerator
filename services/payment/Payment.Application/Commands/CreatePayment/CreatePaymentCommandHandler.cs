using MediatR;
using Payment.Application.Exceptions;
using Payment.Application.Providers;
using Payment.Domain.Repositories;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, CreatePaymentResult>
{
    private readonly IPaymentProvider _provider;
    private readonly IPaymentRepository _repository;

    public CreatePaymentCommandHandler(IPaymentProvider provider, IPaymentRepository repository)
    {
        _provider = provider;
        _repository = repository;
    }

    public async Task<CreatePaymentResult> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = Domain.Entities.Payment.Create(request.Amount, request.Currency, request.CustomerEmail);

        var providerResult = await _provider.AuthorizeAsync(
            request.Amount,
            request.Currency,
            request.PaymentMethodToken,
            cancellationToken);

        if (!providerResult.Success)
            throw new PaymentProviderException(providerResult.ErrorCode, providerResult.ErrorMessage, providerResult.IsTransient);

        payment.Authorize(providerResult.ProviderReference!);

        await _repository.AddAsync(payment, cancellationToken);

        return new CreatePaymentResult(
            PaymentId: payment.Id,
            Status: payment.Status.ToString().ToLowerInvariant(),
            Provider: payment.Provider,
            ProviderReference: payment.ProviderReference,
            CreatedAtUtc: payment.CreatedAtUtc
        );
    }
}
