namespace PaymentProcessingService.Application.Abstractions;

public record AcquirerResult(bool IsApproved, string ResponseCode, string Message);

public interface IAcquirerClient
{
    Task<AcquirerResult> AuthorizeAsync(Guid transactionId, decimal amount, string currency, string cardLast4);
}