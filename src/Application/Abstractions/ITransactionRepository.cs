using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Application.Abstractions;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<IEnumerable<Transaction>> SearchAsync(string? merchantId, TransactionStatus? status);
    Task AddAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
}