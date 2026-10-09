using PaymentProcessingService.Application.Abstractions;

namespace PaymentProcessingService.Application.Tests.TestDoubles;

public class FakeAcquirerClient : IAcquirerClient
{
    private readonly AcquirerResult? _resultToReturn;
    private readonly Exception? _exceptionToThrow;

    public int CallCount { get; private set; }

    private FakeAcquirerClient(AcquirerResult? result, Exception? exception)
    {
        _resultToReturn = result;
        _exceptionToThrow = exception;
    }

    public static FakeAcquirerClient ThatApproves() =>
        new(new AcquirerResult(true, "00", "Aprobada (fake)"), null);

    public static FakeAcquirerClient ThatDeclines() =>
        new(new AcquirerResult(false, "51", "Rechazada (fake)"), null);

    public static FakeAcquirerClient ThatAlwaysFails() =>
        new(null, new TimeoutException("Fallo simulado (fake)"));

    public Task<AcquirerResult> AuthorizeAsync(Guid transactionId, decimal amount, string currency, string cardLast4)
    {
        CallCount++;

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(_resultToReturn!);
    }
}