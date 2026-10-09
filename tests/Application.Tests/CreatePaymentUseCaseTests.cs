using Microsoft.Extensions.Logging.Abstractions;
using PaymentProcessingService.Application.Tests.TestDoubles;
using PaymentProcessingService.Application.UseCases;
using PaymentProcessingService.Domain;
using Xunit;

namespace PaymentProcessingService.Application.Tests;

public class CreatePaymentUseCaseTests
{
    private static CreatePaymentRequest CrearSolicitudValida(string idempotencyKey = "key-001") => new(
        MerchantId: "merchant-001",
        Amount: 15000m,
        Currency: "CLP",
        CardNumber: "4111111111111234",
        CardBrand: "Visa",
        IdempotencyKey: idempotencyKey);

    [Fact]
    public async Task ExecuteAsync_SolicitudNueva_QuedaAprobadaYSePersisteUnaSolaVez()
    {
        var repository = new FakeTransactionRepository();
        var acquirer = FakeAcquirerClient.ThatApproves();
        var useCase = new CreatePaymentUseCase(repository, acquirer, NullLogger<CreatePaymentUseCase>.Instance);

        var response = await useCase.ExecuteAsync(CrearSolicitudValida());

        Assert.Equal(TransactionStatus.Approved, response.Status);
        Assert.Equal("00", response.AcquirerResponseCode);
        Assert.Equal(1, repository.Count);
        Assert.Equal(1, acquirer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MismaIdempotencyKey_NoCreaUnaSegundaTransaccion()
    {
        var repository = new FakeTransactionRepository();
        var acquirer = FakeAcquirerClient.ThatApproves();
        var useCase = new CreatePaymentUseCase(repository, acquirer, NullLogger<CreatePaymentUseCase>.Instance);

        var primeraRespuesta = await useCase.ExecuteAsync(CrearSolicitudValida("key-duplicada"));
        var segundaRespuesta = await useCase.ExecuteAsync(CrearSolicitudValida("key-duplicada"));

        Assert.Equal(primeraRespuesta.TransactionId, segundaRespuesta.TransactionId);
        Assert.Equal(1, repository.Count);
        Assert.Equal(1, acquirer.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MismaIdempotencyKeyConDatosDistintos_LanzaIdempotencyConflictException()
    {
        var repository = new FakeTransactionRepository();
        var acquirer = FakeAcquirerClient.ThatApproves();
        var useCase = new CreatePaymentUseCase(repository, acquirer, NullLogger<CreatePaymentUseCase>.Instance);

        await useCase.ExecuteAsync(CrearSolicitudValida("key-conflicto"));

        var solicitudConMontoDistinto = new CreatePaymentRequest(
            MerchantId: "merchant-001",
            Amount: 99999m,
            Currency: "CLP",
            CardNumber: "4111111111111234",
            CardBrand: "Visa",
            IdempotencyKey: "key-conflicto");

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            useCase.ExecuteAsync(solicitudConMontoDistinto));

        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public async Task ExecuteAsync_AdquirenteRechaza_TransaccionQuedaDeclined()
    {
        var repository = new FakeTransactionRepository();
        var acquirer = FakeAcquirerClient.ThatDeclines();
        var useCase = new CreatePaymentUseCase(repository, acquirer, NullLogger<CreatePaymentUseCase>.Instance);

        var response = await useCase.ExecuteAsync(CrearSolicitudValida());

        Assert.Equal(TransactionStatus.Declined, response.Status);
        Assert.Equal("51", response.AcquirerResponseCode);
    }

    [Fact]
    public async Task ExecuteAsync_AdquirenteFallaSiempre_ReintentaTresVecesYQuedaFailed()
    {
        var repository = new FakeTransactionRepository();
        var acquirer = FakeAcquirerClient.ThatAlwaysFails();
        var useCase = new CreatePaymentUseCase(repository, acquirer, NullLogger<CreatePaymentUseCase>.Instance);

        var response = await useCase.ExecuteAsync(CrearSolicitudValida());

        Assert.Equal(TransactionStatus.Failed, response.Status);
        Assert.Equal(3, acquirer.CallCount);
    }
}