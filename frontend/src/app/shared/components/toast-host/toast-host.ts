import { Component, inject } from '@angular/core';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-toast-host',
  imports: [],
  templateUrl: './toast-host.html',
  styleUrl: './toast-host.scss'
})
export class ToastHost {
  protected readonly toastService = inject(ToastService);
}
