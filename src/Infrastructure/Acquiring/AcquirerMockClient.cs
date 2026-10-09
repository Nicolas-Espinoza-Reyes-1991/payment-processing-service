using PaymentProcessingService.Application.Abstractions;

namespace PaymentProcessingService.Infrastructure.Acquiring;

public class AcquirerMockClient : IAcquirerClient
{
    private const decimal MaxApprovedAmount = 1_000_000m;

    public async Task<AcquirerResult> AuthorizeAsync(Guid transactionId, decimal amount, string currency, string cardLast4)
    {
        await Task.Delay(150);

        if (amount > MaxApprovedAmount)
        {
            return new AcquirerResult(false, "51", "Fondos insuficientes (simulado por Acquirer Mock)");
        }

        return new AcquirerResult(true, "00", "Transacción aprobada (simulado por Acquirer Mock)");
    }
}