using Microsoft.EntityFrameworkCore;
using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Infrastructure.Persistence;

public class PaymentProcessingDbContext : DbContext
{
    public PaymentProcessingDbContext(DbContextOptions<PaymentProcessingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("transactions");

            entity.HasKey(t => t.Id);

            entity.Property(t => t.MerchantId).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Amount).HasColumnType("numeric(18,2)").IsRequired();
            entity.Property(t => t.Currency).IsRequired().HasMaxLength(3);
            entity.Property(t => t.CardLast4).IsRequired().HasMaxLength(4);
            entity.Property(t => t.CardBrand).IsRequired().HasMaxLength(20);
            entity.Property(t => t.Status).HasConversion<string>().IsRequired();
            entity.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.Property(t => t.CorrelationId).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
            entity.Property(t => t.UpdatedAt).IsRequired();
            entity.Property(t => t.AcquirerResponseCode).HasMaxLength(50);
            entity.Property(t => t.AcquirerMessage).HasMaxLength(500);

            entity.HasIndex(t => t.IdempotencyKey).IsUnique();
            entity.HasIndex(t => t.MerchantId);
        });
    }
}