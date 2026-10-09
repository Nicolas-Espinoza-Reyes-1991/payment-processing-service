namespace PaymentProcessingService.Domain;

public class Transaction
{
    public Guid Id { get; private set; }
    public string MerchantId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string CardLast4 { get; private set; } = string.Empty;
    public string CardBrand { get; private set; } = string.Empty;
    public TransactionStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? AcquirerResponseCode { get; private set; }
    public string? AcquirerMessage { get; private set; }

    private Transaction() { }

    public static Transaction Create(
        string merchantId,
        decimal amount,
        string currency,
        string cardLast4,
        string cardBrand,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(merchantId))
            throw new ArgumentException("merchant_id es obligatorio.", nameof(merchantId));

        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("La moneda es obligatoria.", nameof(currency));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("idempotency_key es obligatorio.", nameof(idempotencyKey));

        var now = DateTime.UtcNow;

        return new Transaction
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Amount = amount,
            Currency = currency,
            CardLast4 = cardLast4,
            CardBrand = cardBrand,
            Status = TransactionStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CorrelationId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkAsProcessing()
    {
        if (Status != TransactionStatus.Pending)
            throw new InvalidOperationException(
                $"Solo una transacción en estado Pending puede pasar a Processing. Estado actual: {Status}.");

        Status = TransactionStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve(string acquirerResponseCode, string acquirerMessage)
    {
        if (Status != TransactionStatus.Processing)
            throw new InvalidOperationException(
                $"Solo una transacción en estado Processing puede ser aprobada. Estado actual: {Status}.");

        Status = TransactionStatus.Approved;
        AcquirerResponseCode = acquirerResponseCode;
        AcquirerMessage = acquirerMessage;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Decline(string acquirerResponseCode, string acquirerMessage)
    {
        if (Status != TransactionStatus.Processing)
            throw new InvalidOperationException(
                $"Solo una transacción en estado Processing puede ser rechazada. Estado actual: {Status}.");

        Status = TransactionStatus.Declined;
        AcquirerResponseCode = acquirerResponseCode;
        AcquirerMessage = acquirerMessage;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = TransactionStatus.Failed;
        AcquirerMessage = reason;
        UpdatedAt = DateTime.UtcNow;
    }
}