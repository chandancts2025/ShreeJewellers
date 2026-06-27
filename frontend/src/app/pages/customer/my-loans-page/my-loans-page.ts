import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/services/auth';
import { LoanService } from '../../../core/services/loan';

@Component({
  selector: 'app-my-loans-page',
  imports: [CommonModule, RouterLink],
  templateUrl: './my-loans-page.html',
  styleUrl: './my-loans-page.scss'
})
export class MyLoansPage {
  private readonly auth = inject(AuthService);
  private readonly loanService = inject(LoanService);
  private readonly destroyRef = inject(DestroyRef);
  loans: any[] = [];

  constructor() {
    const userId = this.auth.session()?.userId ?? '';
    this.loanService.getCustomerLoans(userId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((loans) => {
      this.loans = loans;
    });
  }
}
