import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminService } from '../../../core/services/admin';

@Component({
  selector: 'app-admin-dashboard-page',
  imports: [CommonModule],
  templateUrl: './admin-dashboard-page.html',
  styleUrl: './admin-dashboard-page.scss'
})
export class AdminDashboardPage {
  private readonly admin = inject(AdminService);
  private readonly destroyRef = inject(DestroyRef);
  data: any;

  constructor() {
    this.admin.getDashboard().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }
}
