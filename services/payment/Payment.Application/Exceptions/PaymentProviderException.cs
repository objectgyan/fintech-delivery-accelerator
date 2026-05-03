namespace Payment.Application.Exceptions;

public sealed class PaymentProviderException : Exception
{
    public string? ErrorCode { get; }
    public bool IsTransient { get; }

    public PaymentProviderException(string? errorCode, string? message, bool isTransient)
        : base(message ?? "Payment provider error")
    {
        ErrorCode = errorCode;
        IsTransient = isTransient;
    }
}
