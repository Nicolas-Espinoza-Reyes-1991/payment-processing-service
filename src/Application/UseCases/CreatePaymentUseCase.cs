using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Application.UseCases;

public record CreatePaymentRequest(
    string MerchantId,
    decimal Amount,
    string Currency,
    string CardNumber,
    string CardBrand,
    string IdempotencyKey);

public record CreatePaymentResponse(
    Guid TransactionId,
    TransactionStatus Status,
    Guid CorrelationId,
    string? AcquirerResponseCode,
    string? AcquirerMessage);

public class CreatePaymentUseCase
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAcquirerClient _acquirerClient;

    public CreatePaymentUseCase(ITransactionRepository transactionRepository, IAcquirerClient acquirerClient)
    {
        _transactionRepository = transactionRepository;
        _acquirerClient = acquirerClient;
    }

    public async Task<CreatePaymentResponse> ExecuteAsync(CreatePaymentRequest request)
    {
        var existing = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
        if (existing is not null)
        {
            return MapToResponse(existing);
        }

        var cardLast4 = request.CardNumber.Length >= 4
            ? request.CardNumber[^4..]
            : request.CardNumber;

        var transaction = Transaction.Create(
            request.MerchantId,
            request.Amount,
            request.Currency,
            cardLast4,
            request.CardBrand,
            request.IdempotencyKey);

        await _transactionRepository.AddAsync(transaction);

        transaction.MarkAsProcessing();
        await _transactionRepository.UpdateAsync(transaction);

        var acquirerResult = await _acquirerClient.AuthorizeAsync(
            transaction.Id, transaction.Amount, transaction.Currency, transaction.CardLast4);

        if (acquirerResult.IsApproved)
        {
            transaction.Approve(acquirerResult.ResponseCode, acquirerResult.Message);
        }
        else
        {
            transaction.Decline(acquirerResult.ResponseCode, acquirerResult.Message);
        }

        await _transactionRepository.UpdateAsync(transaction);

        return MapToResponse(transaction);
    }

    private static CreatePaymentResponse MapToResponse(Transaction transaction) =>
        new(transaction.Id, transaction.Status, transaction.CorrelationId,
            transaction.AcquirerResponseCode, transaction.AcquirerMessage);
}