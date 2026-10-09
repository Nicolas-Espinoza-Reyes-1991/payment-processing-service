using Microsoft.EntityFrameworkCore;
using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Infrastructure.Persistence;

public class TransactionRepository : ITransactionRepository
{
    private readonly PaymentProcessingDbContext _context;

    public TransactionRepository(PaymentProcessingDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id) =>
        await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey) =>
        await _context.Transactions.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

    public async Task<IEnumerable<Transaction>> SearchAsync(string? merchantId, TransactionStatus? status)
    {
        var query = _context.Transactions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(merchantId))
            query = query.Where(t => t.MerchantId == merchantId);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        return await query.ToListAsync();
    }

    public async Task AddAsync(Transaction transaction)
    {
        await _context.Transactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }
}