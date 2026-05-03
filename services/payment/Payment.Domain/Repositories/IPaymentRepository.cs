using PaymentEntity = Payment.Domain.Entities.Payment;

namespace Payment.Domain.Repositories;

public interface IPaymentRepository
{
    Task AddAsync(PaymentEntity payment, CancellationToken cancellationToken = default);
    Task<PaymentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
