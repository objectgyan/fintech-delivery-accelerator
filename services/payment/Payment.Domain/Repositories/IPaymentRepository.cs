using Payment.Domain.Entities;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Repositories;

public interface IPaymentRepository
{
    Task AddAsync(PaymentEntity payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(PaymentEntity payment, CancellationToken cancellationToken = default);
    Task<PaymentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAttemptAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<OutboxMessage>> GetPendingOutboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default);
    Task UpdateOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task CompletePaymentAsync(PaymentEntity payment, PaymentAttempt attempt, OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
    Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default);
    Task AddIdempotencyKeyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
    Task UpdateIdempotencyKeyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
    Task CompleteWithIdempotencyAsync(PaymentEntity payment, PaymentAttempt attempt, OutboxMessage outboxMessage, IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default);
}
