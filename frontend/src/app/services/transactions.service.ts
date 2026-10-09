import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Transaction, CreatePaymentRequest, CreatePaymentResponse } from '../models/transaction.model';

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://localhost:5165/payments';

  search(merchantId?: string, status?: string): Observable<Transaction[]> {
    let params = new HttpParams();
    if (merchantId) {
      params = params.set('merchant_id', merchantId);
    }
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<Transaction[]>(this.baseUrl, { params });
  }

  create(request: CreatePaymentRequest): Observable<CreatePaymentResponse> {
    const idempotencyKey = crypto.randomUUID();

    return this.http.post<CreatePaymentResponse>(this.baseUrl, request, {
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  }
}
