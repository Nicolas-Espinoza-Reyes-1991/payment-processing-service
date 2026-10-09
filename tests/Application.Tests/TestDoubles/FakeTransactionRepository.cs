using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Application.Tests.TestDoubles;

public class FakeTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    public Task<Transaction?> GetByIdAsync(Guid id) =>
        Task.FromResult(_transactions.FirstOrDefault(t => t.Id == id));

    public Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey) =>
        Task.FromResult(_transactions.FirstOrDefault(t => t.IdempotencyKey == idempotencyKey));

    public Task<IEnumerable<Transaction>> SearchAsync(string? merchantId, TransactionStatus? status)
    {
        var query = _transactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(merchantId))
            query = query.Where(t => t.MerchantId == merchantId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        return Task.FromResult(query);
    }

    public Task AddAsync(Transaction transaction)
    {
        _transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Transaction transaction) => Task.CompletedTask;

    public int Count => _transactions.Count;
}