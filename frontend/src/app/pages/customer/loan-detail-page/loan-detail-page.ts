import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/services/auth';
import { LoanService } from '../../../core/services/loan';

@Component({
  selector: 'app-loan-detail-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './loan-detail-page.html',
  styleUrl: './loan-detail-page.scss'
})
export class LoanDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly loanService = inject(LoanService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  detail: any;
  outstanding: any;
  schedule: any;
  actionMessage = '';

  readonly repaymentForm = this.fb.nonNullable.group({
    amountPaid: [0],
    paymentMode: ['Cash'],
    notes: ['']
  });

  readonly extendForm = this.fb.nonNullable.group({
    newMaturityDate: [new Date(new Date().setMonth(new Date().getMonth() + 3)).toISOString().slice(0, 10)],
    reason: ['Loan extension requested']
  });

  readonly closeForm = this.fb.nonNullable.group({
    goldReturned: [true],
    notes: ['Customer returned pledged gold successfully.']
  });

  readonly auctionForm = this.fb.nonNullable.group({
    auctionProceeds: [0],
    auctionDate: [new Date().toISOString().slice(0, 10)],
    notes: ['Auctioned pledged gold to recover outstanding balance.']
  });

  constructor() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loanService.getLoan(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.detail = data;
    });
    this.loanService.getOutstanding(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.outstanding = data;
    });
    this.loanService.getSchedule(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.schedule = data;
    });
  }

  get canManageLoan(): boolean {
    return this.auth.hasAnyRole(['Staff', 'Admin', 'SuperAdmin']);
  }

  recordRepayment(): void {
    if (!this.detail) return;

    const payload = {
      goldLoanId: this.detail.id,
      repaymentDate: new Date().toISOString().slice(0, 10),
      amountPaid: this.repaymentForm.controls.amountPaid.value,
      paymentMode: this.repaymentForm.controls.paymentMode.value,
      notes: this.repaymentForm.controls.notes.value
    };

    this.loanService.recordRepayment(this.detail.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Repayment recorded successfully.';
      this.refreshDetails();
    });
  }

  extendLoan(): void {
    if (!this.detail) return;

    const payload = {
      loanId: this.detail.id,
      newMaturityDate: this.extendForm.controls.newMaturityDate.value,
      reason: this.extendForm.controls.reason.value
    };

    this.loanService.extendLoan(this.detail.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Loan maturity date extended.';
      this.refreshDetails();
    });
  }

  closeLoan(): void {
    if (!this.detail) return;

    const payload = {
      loanId: this.detail.id,
      notes: this.closeForm.controls.notes.value,
      goldReturned: this.closeForm.controls.goldReturned.value
    };

    this.loanService.closeLoan(this.detail.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Loan closed successfully.';
      this.refreshDetails();
    });
  }

  auctionLoan(): void {
    if (!this.detail) return;

    const payload = {
      loanId: this.detail.id,
      auctionProceeds: this.auctionForm.controls.auctionProceeds.value,
      auctionDate: this.auctionForm.controls.auctionDate.value,
      notes: this.auctionForm.controls.notes.value
    };

    this.loanService.auctionLoan(this.detail.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Auction recorded successfully.';
      this.refreshDetails();
    });
  }

  private refreshDetails(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loanService.getLoan(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.detail = data;
    });
    this.loanService.getOutstanding(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.outstanding = data;
    });
  }
}
