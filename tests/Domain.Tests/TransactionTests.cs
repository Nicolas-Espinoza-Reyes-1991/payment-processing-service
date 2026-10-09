using PaymentProcessingService.Domain;
using Xunit;

namespace PaymentProcessingService.Domain.Tests;

public class TransactionTests
{
    [Fact]
    public void Create_ConDatosValidos_DevuelveTransaccionEnPending()
    {
        var transaction = Transaction.Create(
            merchantId: "merchant-001",
            amount: 1000m,
            currency: "CLP",
            cardLast4: "1234",
            cardBrand: "Visa",
            idempotencyKey: "key-001");

        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Equal("merchant-001", transaction.MerchantId);
        Assert.NotEqual(Guid.Empty, transaction.Id);
        Assert.NotEqual(Guid.Empty, transaction.CorrelationId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_ConMontoInvalido_LanzaArgumentException(decimal montoInvalido)
    {
        Assert.Throws<ArgumentException>(() =>
            Transaction.Create("merchant-001", montoInvalido, "CLP", "1234", "Visa", "key-001"));
    }

    [Fact]
    public void Create_ConMerchantIdVacio_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Transaction.Create("", 1000m, "CLP", "1234", "Visa", "key-001"));
    }

    [Fact]
    public void MarkAsProcessing_DesdePending_CambiaAProcessing()
    {
        var transaction = CrearTransaccionValida();

        transaction.MarkAsProcessing();

        Assert.Equal(TransactionStatus.Processing, transaction.Status);
    }

    [Fact]
    public void MarkAsProcessing_DesdeUnEstadoQueNoEsPending_LanzaInvalidOperationException()
    {
        var transaction = CrearTransaccionValida();
        transaction.MarkAsProcessing();

        Assert.Throws<InvalidOperationException>(() => transaction.MarkAsProcessing());
    }

    [Fact]
    public void Approve_DesdeProcessing_CambiaAApprovedYGuardaDatosDelAdquirente()
    {
        var transaction = CrearTransaccionValida();
        transaction.MarkAsProcessing();

        transaction.Approve("00", "Aprobada");

        Assert.Equal(TransactionStatus.Approved, transaction.Status);
        Assert.Equal("00", transaction.AcquirerResponseCode);
        Assert.Equal("Aprobada", transaction.AcquirerMessage);
    }

    [Fact]
    public void Approve_DesdePending_LanzaInvalidOperationException()
    {
        var transaction = CrearTransaccionValida();

        Assert.Throws<InvalidOperationException>(() => transaction.Approve("00", "Aprobada"));
    }

    [Fact]
    public void Decline_DesdeProcessing_CambiaADeclined()
    {
        var transaction = CrearTransaccionValida();
        transaction.MarkAsProcessing();

        transaction.Decline("51", "Fondos insuficientes");

        Assert.Equal(TransactionStatus.Declined, transaction.Status);
        Assert.Equal("51", transaction.AcquirerResponseCode);
    }

    [Fact]
    public void Fail_CambiaAFailedYGuardaElMotivo()
    {
        var transaction = CrearTransaccionValida();

        transaction.Fail("Timeout del adquirente");

        Assert.Equal(TransactionStatus.Failed, transaction.Status);
        Assert.Equal("Timeout del adquirente", transaction.AcquirerMessage);
    }

    private static Transaction CrearTransaccionValida() =>
        Transaction.Create("merchant-001", 1000m, "CLP", "1234", "Visa", "key-001");
}