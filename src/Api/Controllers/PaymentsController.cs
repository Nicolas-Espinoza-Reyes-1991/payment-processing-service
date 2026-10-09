using Microsoft.AspNetCore.Mvc;
using PaymentProcessingService.Api.Contracts;
using PaymentProcessingService.Application.Abstractions;
using PaymentProcessingService.Application.UseCases;
using PaymentProcessingService.Domain;

namespace PaymentProcessingService.Api.Controllers;

[ApiController]
[Route("payments")]
public class PaymentsController : ControllerBase
{
    private readonly CreatePaymentUseCase _createPaymentUseCase;
    private readonly ITransactionRepository _transactionRepository;

    public PaymentsController(
        CreatePaymentUseCase createPaymentUseCase,
        ITransactionRepository transactionRepository)
    {
        _createPaymentUseCase = createPaymentUseCase;
        _transactionRepository = transactionRepository;
    }

    [HttpPost]
    public async Task<ActionResult<CreatePaymentResponse>> Create(
        [FromBody] CreatePaymentHttpRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { error = "El header Idempotency-Key es obligatorio." });
        }

        var useCaseRequest = new CreatePaymentRequest(
            request.MerchantId,
            request.Amount,
            request.Currency,
            request.CardNumber,
            request.CardBrand,
            idempotencyKey);

        var result = await _createPaymentUseCase.ExecuteAsync(useCaseRequest);

        return CreatedAtAction(nameof(GetById), new { id = result.TransactionId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionResponse>> GetById(Guid id)
    {
        var transaction = await _transactionRepository.GetByIdAsync(id);

        if (transaction is null)
        {
            return NotFound(new { error = $"No se encontró la transacción {id}." });
        }

        return Ok(TransactionResponse.FromDomain(transaction));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> Search(
        [FromQuery(Name = "merchant_id")] string? merchantId,
        [FromQuery] string? status)
    {
        TransactionStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<TransactionStatus>(status, ignoreCase: true, out var parsed))
            {
                return BadRequest(new { error = $"Estado inválido: {status}" });
            }
            parsedStatus = parsed;
        }

        var transactions = await _transactionRepository.SearchAsync(merchantId, parsedStatus);

        return Ok(transactions.Select(TransactionResponse.FromDomain));
    }
}