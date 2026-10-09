export interface Transaction {
  transactionId: string;
  merchantId: string;
  amount: number;
  currency: string;
  cardLast4: string;
  cardBrand: string;
  status: number;
  correlationId: string;
  createdAt: string;
  updatedAt: string;
  acquirerResponseCode: string | null;
  acquirerMessage: string | null;
}

export const TRANSACTION_STATUS_LABELS: Record<number, string> = {
  0: 'Pending',
  1: 'Processing',
  2: 'Approved',
  3: 'Declined',
  4: 'Failed',
};

export interface CreatePaymentRequest {
  merchantId: string;
  amount: number;
  currency: string;
  cardNumber: string;
  cardBrand: string;
}

export interface CreatePaymentResponse {
  transactionId: string;
  status: number;
  correlationId: string;
  acquirerResponseCode: string | null;
  acquirerMessage: string | null;
}
