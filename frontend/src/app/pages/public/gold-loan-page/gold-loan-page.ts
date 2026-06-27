import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicGoldLoanInfo } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';
import { AuthService } from '../../../core/services/auth';
import { EmiCalculator } from '../../../shared/components/emi-calculator/emi-calculator';

@Component({
  selector: 'app-gold-loan-page',
  imports: [EmiCalculator, RouterLink],
  templateUrl: './gold-loan-page.html',
  styleUrl: './gold-loan-page.scss'
})
export class GoldLoanPage {
  private readonly publicData = inject(PublicDataService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  data?: PublicGoldLoanInfo;

  constructor() {
    this.publicData.getGoldLoanInfo().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }

  get isAuthenticated(): boolean {
    return this.auth.isAuthenticated();
  }

  get isCustomer(): boolean {
    return this.auth.hasAnyRole(['Customer']);
  }

  get isStaffOrAdmin(): boolean {
    return this.auth.hasAnyRole(['Staff', 'Admin', 'SuperAdmin']);
  }
}
