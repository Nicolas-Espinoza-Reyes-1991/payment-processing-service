import { Injectable, signal } from '@angular/core';

export interface Toast {
  message: string;
  type: 'success' | 'error';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly _toast = signal<Toast | null>(null);
  readonly toast = this._toast.asReadonly();

  show(message: string, type: 'success' | 'error'): void {
    this._toast.set({ message, type });
    setTimeout(() => this._toast.set(null), 4000);
  }
}
