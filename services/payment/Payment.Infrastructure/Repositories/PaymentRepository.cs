using Microsoft.EntityFrameworkCore;
using Payment.Application.Exceptions;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;
using Payment.Infrastructure.Data;
using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Infrastructure.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context) => _context = context;

    public async Task AddAsync(Payment.Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        await _context.Payments.AddAsync(payment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Payment.Domain.Entities.Payment payment, CancellationToken cancellationToken = default)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Payment.Domain.Entities.Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Payments.FindAsync([id], cancellationToken);
    }

    public async Task AddAttemptAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        await _context.PaymentAttempts.AddAsync(attempt, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetPendingOutboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateOutboxMessageAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompletePaymentAsync(PaymentEntity payment, PaymentAttempt attempt, OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        _context.Payments.Update(payment);
        await _context.PaymentAttempts.AddAsync(attempt, cancellationToken);
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IdempotencyKey?> GetIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken);
    }

    public async Task AddIdempotencyKeyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        await _context.IdempotencyKeys.AddAsync(idempotencyKey, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UX_IdempotencyKeys_Key") == true
                                           || ex.InnerException?.Message.Contains("unique") == true)
        {
            // Two concurrent requests raced past the read check — treat as in-progress conflict
            _context.ChangeTracker.Clear();
            throw new IdempotencyConflictException();
        }
    }

    public async Task UpdateIdempotencyKeyAsync(IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        _context.IdempotencyKeys.Update(idempotencyKey);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteWithIdempotencyAsync(PaymentEntity payment, PaymentAttempt attempt, OutboxMessage outboxMessage, IdempotencyKey idempotencyKey, CancellationToken cancellationToken = default)
    {
        _context.Payments.Update(payment);
        await _context.PaymentAttempts.AddAsync(attempt, cancellationToken);
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        _context.IdempotencyKeys.Update(idempotencyKey);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
