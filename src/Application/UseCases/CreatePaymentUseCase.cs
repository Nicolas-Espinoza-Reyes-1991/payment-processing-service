using Microsoft.Extensions.Logging;
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
    private const int MaxAcquirerAttempts = 3;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IAcquirerClient _acquirerClient;
    private readonly ILogger<CreatePaymentUseCase> _logger;

    public CreatePaymentUseCase(
        ITransactionRepository transactionRepository,
        IAcquirerClient acquirerClient,
        ILogger<CreatePaymentUseCase> logger)
    {
        _transactionRepository = transactionRepository;
        _acquirerClient = acquirerClient;
        _logger = logger;
    }

    public async Task<CreatePaymentResponse> ExecuteAsync(CreatePaymentRequest request)
    {
        var existing = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Idempotencia: solicitud repetida para {IdempotencyKey}, devolviendo transacción existente {TransactionId} (CorrelationId: {CorrelationId})",
                request.IdempotencyKey, existing.Id, existing.CorrelationId);

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

        _logger.LogInformation(
            "Transaccion {TransactionId} creada en estado Pending (CorrelationId: {CorrelationId})",
            transaction.Id, transaction.CorrelationId);

        transaction.MarkAsProcessing();
        await _transactionRepository.UpdateAsync(transaction);

        _logger.LogInformation(
            "Transaccion {TransactionId} pasó a Processing (CorrelationId: {CorrelationId})",
            transaction.Id, transaction.CorrelationId);

        AcquirerResult? acquirerResult = null;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxAcquirerAttempts; attempt++)
        {
            try
            {
                acquirerResult = await _acquirerClient.AuthorizeAsync(
                    transaction.Id, transaction.Amount, transaction.Currency, transaction.CardLast4);

                lastException = null;
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;

                _logger.LogWarning(
                    "Intento {Attempt}/{MaxAttempts} fallido contra el adquirente para transaccion {TransactionId} (CorrelationId: {CorrelationId}): {ErrorMessage}",
                    attempt, MaxAcquirerAttempts, transaction.Id, transaction.CorrelationId, ex.Message);

                if (attempt < MaxAcquirerAttempts)
                {
                    await Task.Delay(200 * attempt);
                }
            }
        }

        if (acquirerResult is null)
        {
            transaction.Fail(lastException?.Message ?? "Error desconocido al contactar al adquirente.");
            await _transactionRepository.UpdateAsync(transaction);

            _logger.LogError(
                "Transaccion {TransactionId} marcada como Failed tras {MaxAttempts} intentos fallidos (CorrelationId: {CorrelationId})",
                transaction.Id, MaxAcquirerAttempts, transaction.CorrelationId);

            return MapToResponse(transaction);
        }

        if (acquirerResult.IsApproved)
        {
            transaction.Approve(acquirerResult.ResponseCode, acquirerResult.Message);
        }
        else
        {
            transaction.Decline(acquirerResult.ResponseCode, acquirerResult.Message);
        }

        await _transactionRepository.UpdateAsync(transaction);

        _logger.LogInformation(
            "Transaccion {TransactionId} finalizada con estado {Status} (CorrelationId: {CorrelationId})",
            transaction.Id, transaction.Status, transaction.CorrelationId);

        return MapToResponse(transaction);
    }

    private static CreatePaymentResponse MapToResponse(Transaction transaction) =>
        new(transaction.Id, transaction.Status, transaction.CorrelationId,
            transaction.AcquirerResponseCode, transaction.AcquirerMessage);
}