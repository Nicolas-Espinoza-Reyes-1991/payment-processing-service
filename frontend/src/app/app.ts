import { Component, inject, signal } from '@angular/core';
import { TransactionsService } from './services/transactions.service';
import { Transaction } from './models/transaction.model';
import { TransactionListComponent, SearchFilters } from './components/transaction-list/transaction-list';
import { PaymentFormModalComponent } from './components/payment-form-modal/payment-form-modal';
import { ToastComponent } from './components/toast/toast';

@Component({
  selector: 'app-root',
  imports: [TransactionListComponent, PaymentFormModalComponent, ToastComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly transactionsService = inject(TransactionsService);

  protected readonly transactions = signal<Transaction[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showModal = signal(false);

  private currentFilters: SearchFilters = { merchantId: '', status: '' };

  constructor() {
    this.search(this.currentFilters);
  }

  protected search(filters: SearchFilters): void {
    this.currentFilters = filters;
    this.loading.set(true);
    this.error.set(null);

    this.transactionsService.search(filters.merchantId, filters.status).subscribe({
      next: (result) => {
        this.transactions.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('No se pudieron cargar las transacciones. ¿Está el backend corriendo?');
        this.loading.set(false);
      }
    });
  }

  protected refresh(): void {
    this.search(this.currentFilters);
  }

  protected openModal(): void {
    this.showModal.set(true);
  }

  protected closeModal(): void {
    this.showModal.set(false);
  }
}
