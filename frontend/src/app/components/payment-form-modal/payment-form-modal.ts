import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { TransactionsService } from '../../services/transactions.service';
import { ToastService } from '../../services/toast.service';
import { TRANSACTION_STATUS_LABELS } from '../../models/transaction.model';

@Component({
  selector: 'app-payment-form-modal',
  imports: [FormsModule],
  templateUrl: './payment-form-modal.html',
  styleUrl: './payment-form-modal.css'
})
export class PaymentFormModalComponent {
  private readonly transactionsService = inject(TransactionsService);
  private readonly toastService = inject(ToastService);

  isOpen = input.required<boolean>();
  closed = output<void>();
  created = output<void>();

  protected submitting = signal(false);
  protected submitError = signal<string | null>(null);

  protected formMerchantId = 'merchant-001';
  protected formAmount = 10000;
  protected formCurrency = 'CLP';
  protected formCardNumber = '4111111111111234';
  protected formCardBrand = 'Visa';

  protected close(): void {
    this.submitError.set(null);
    this.closed.emit();
  }

  protected submitPayment(form: NgForm): void {
    if (form.invalid) {
      Object.values(form.controls).forEach((control) => control.markAsTouched());
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);

    this.transactionsService.create({
      merchantId: this.formMerchantId,
      amount: this.formAmount,
      currency: this.formCurrency.toUpperCase(),
      cardNumber: this.formCardNumber,
      cardBrand: this.formCardBrand
    }).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.toastService.show(
          `Pago ${TRANSACTION_STATUS_LABELS[result.status]}: ${result.acquirerMessage}`,
          result.status === 2 ? 'success' : 'error'
        );
        this.closed.emit();
        this.created.emit();
      },
      error: (err) => {
        this.submitting.set(false);
        const message = err.error?.error ?? 'Ocurrió un error al crear el pago.';
        this.submitError.set(message);
        this.toastService.show(message, 'error');
      }
    });
  }
}
