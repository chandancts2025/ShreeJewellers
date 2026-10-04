import { Injectable, signal } from '@angular/core';

export type ToastLevel = 'success' | 'error' | 'warning' | 'info';

export interface ToastMessage {
  id: number;
  title: string;
  message: string;
  level: ToastLevel;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly messages = signal<ToastMessage[]>([]);

  show(level: ToastLevel, title: string, message: string): void {
    const id = Date.now() + Math.floor(Math.random() * 1000);
    this.messages.update((items) => [...items, { id, title, message, level }]);
    setTimeout(() => this.dismiss(id), 4500);
  }

  success(title: string, message: string): void {
    this.show('success', title, message);
  }

  error(title: string, message: string): void {
    this.show('error', title, message);
  }

  warning(title: string, message: string): void {
    this.show('warning', title, message);
  }

  info(title: string, message: string): void {
    this.show('info', title, message);
  }

  dismiss(id: number): void {
    this.messages.update((items) => items.filter((item) => item.id !== id));
  }
}
