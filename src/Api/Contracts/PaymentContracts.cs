using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Api.Contracts;

public record CreatePaymentHttpRequest(
    string MerchantId,
    decimal Amount,
    string Currency,
    string CardNumber,
    string CardBrand);

public record TransactionResponse(
    Guid TransactionId,
    string MerchantId,
    decimal Amount,
    string Currency,
    string CardLast4,
    string CardBrand,
    TransactionStatus Status,
    Guid CorrelationId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? AcquirerResponseCode,
    string? AcquirerMessage)
{
    public static TransactionResponse FromDomain(Transaction t) => new(
        t.Id, t.MerchantId, t.Amount, t.Currency, t.CardLast4, t.CardBrand,
        t.Status, t.CorrelationId, t.CreatedAt, t.UpdatedAt,
        t.AcquirerResponseCode, t.AcquirerMessage);
}