using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;
using Payment.Domain.Repositories;
using Payment.Infrastructure.Data;

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
}
