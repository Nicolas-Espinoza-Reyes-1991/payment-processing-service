import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe, SlicePipe } from '@angular/common';
import { Transaction, TRANSACTION_STATUS_LABELS } from '../../models/transaction.model';

export interface SearchFilters {
  merchantId: string;
  status: string;
}

@Component({
  selector: 'app-transaction-list',
  imports: [FormsModule, DecimalPipe, DatePipe, SlicePipe],
  templateUrl: './transaction-list.html',
  styleUrl: './transaction-list.css'
})
export class TransactionListComponent {
  transactions = input.required<Transaction[]>();
  loading = input(false);
  error = input<string | null>(null);

  search = output<SearchFilters>();

  protected merchantIdFilter = '';
  protected statusFilter = '';
  protected readonly statusOptions = ['Pending', 'Processing', 'Approved', 'Declined', 'Failed'];
  protected readonly statusLabels = TRANSACTION_STATUS_LABELS;

  protected emitSearch(): void {
    this.search.emit({ merchantId: this.merchantIdFilter, status: this.statusFilter });
  }
}
