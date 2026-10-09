namespace PaymentProcessingService.Domain;

public enum TransactionStatus
{
    Pending,
    Processing,
    Approved,
    Declined,
    Failed
}