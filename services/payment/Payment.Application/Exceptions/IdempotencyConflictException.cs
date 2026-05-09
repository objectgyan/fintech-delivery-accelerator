namespace Payment.Application.Exceptions;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException()
        : base("A payment with this Idempotency-Key is already being processed.") { }
}
