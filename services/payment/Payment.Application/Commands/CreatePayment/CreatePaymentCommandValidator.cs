using FluentValidation;

namespace Payment.Application.Commands.CreatePayment;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Matches(@"^[A-Z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code (e.g. CAD, USD).");

        RuleFor(x => x.CustomerEmail)
            .NotEmpty()
            .EmailAddress().WithMessage("CustomerEmail must be a valid email address.");

        RuleFor(x => x.PaymentMethodToken)
            .NotEmpty().WithMessage("PaymentMethodToken is required.");
    }
}
