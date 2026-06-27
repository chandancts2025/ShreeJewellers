import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LoanService } from '../../../core/services/loan';

@Component({
  selector: 'app-gold-loan-management-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './gold-loan-management-page.html',
  styleUrl: './gold-loan-management-page.scss'
})
export class GoldLoanManagementPage {
  private readonly loanService = inject(LoanService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  activeLoans: any[] = [];
  dueThisWeek: any[] = [];
  selectedLoan: any = null;
  eligibilityResult: any = null;
  actionMessage = '';

  readonly createLoanForm = this.fb.nonNullable.group({
    customerUserId: [''],
    loanDate: [new Date().toISOString().slice(0, 10)],
    principalAmount: [0],
    goldPurity: ['22K'],
    notes: ['']
  });

  readonly loanItemForm = this.fb.group({
    itemDescription: ['Gold chain'],
    weightGrams: [0],
    purity: ['22K'],
    estimatedValue: [0],
    hallmarkNumber: [''],
    imageBase64: [null],
    imageExtension: ['jpg']
  });

  readonly eligibilityForm = this.fb.nonNullable.group({
    customerUserId: [''],
    itemDescription: ['Gold item'],
    weightGrams: [0],
    purity: ['22K'],
    estimatedValue: [0]
  });

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
    this.load();
  }

  load(): void {
    this.loanService.getActiveLoans().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((response) => {
      this.activeLoans = response ?? [];
    });

    this.loanService.getDueThisWeek().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((response) => {
      this.dueThisWeek = response ?? [];
    });
  }

  createLoan(): void {
    const loanItem = this.loanItemForm.getRawValue();
    const payload = {
      ...this.createLoanForm.getRawValue(),
      goldItems: [{
        itemDescription: loanItem.itemDescription,
        weightGrams: loanItem.weightGrams,
        purity: loanItem.purity,
        estimatedValue: loanItem.estimatedValue,
        hallmarkNumber: loanItem.hallmarkNumber,
        imageBase64: loanItem.imageBase64,
        imageExtension: loanItem.imageExtension
      }]
    };

    this.loanService.createLoan(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.resetCreateForm();
      this.load();
      this.actionMessage = 'Loan created successfully.';
    });
  }

  selectLoan(loan: any): void {
    this.selectedLoan = loan;
    this.actionMessage = '';
  }

  checkEligibility(): void {
    const payload = {
      customerUserId: this.eligibilityForm.controls.customerUserId.value,
      goldItems: [{
        itemDescription: this.eligibilityForm.controls.itemDescription.value,
        weightGrams: this.eligibilityForm.controls.weightGrams.value,
        purity: this.eligibilityForm.controls.purity.value,
        estimatedValue: this.eligibilityForm.controls.estimatedValue.value,
        hallmarkNumber: null,
        imageBase64: null,
        imageExtension: null
      }]
    };

    this.loanService.checkEligibility(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((result) => {
      this.eligibilityResult = result;
      this.actionMessage = result.isEligible ? 'Loan eligibility verified.' : `Not eligible: ${result.ineligibilityReason}`;
    });
  }

  recordRepayment(): void {
    if (!this.selectedLoan) return;

    const payload = {
      goldLoanId: this.selectedLoan.id,
      repaymentDate: new Date().toISOString().slice(0, 10),
      amountPaid: this.repaymentForm.controls.amountPaid.value,
      paymentMode: this.repaymentForm.controls.paymentMode.value,
      notes: this.repaymentForm.controls.notes.value
    };

    this.loanService.recordRepayment(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Repayment recorded successfully.';
      this.load();
    });
  }

  extendLoan(): void {
    if (!this.selectedLoan) return;

    const payload = {
      loanId: this.selectedLoan.id,
      newMaturityDate: this.extendForm.controls.newMaturityDate.value,
      reason: this.extendForm.controls.reason.value
    };

    this.loanService.extendLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Loan maturity date extended.';
      this.load();
    });
  }

  closeLoan(): void {
    if (!this.selectedLoan) return;

    const payload = {
      loanId: this.selectedLoan.id,
      notes: this.closeForm.controls.notes.value,
      goldReturned: this.closeForm.controls.goldReturned.value
    };

    this.loanService.closeLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Loan closed successfully.';
      this.load();
    });
  }

  auctionLoan(): void {
    if (!this.selectedLoan) return;

    const payload = {
      loanId: this.selectedLoan.id,
      auctionProceeds: this.auctionForm.controls.auctionProceeds.value,
      auctionDate: this.auctionForm.controls.auctionDate.value,
      notes: this.auctionForm.controls.notes.value
    };

    this.loanService.auctionLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.actionMessage = 'Auction recorded and loan closed.';
      this.load();
    });
  }

  private resetCreateForm(): void {
    this.createLoanForm.reset({
      customerUserId: '',
      loanDate: new Date().toISOString().slice(0, 10),
      principalAmount: 0,
      goldPurity: '22K',
      notes: ''
    });

    this.loanItemForm.reset({
      itemDescription: 'Gold chain',
      weightGrams: 0,
      purity: '22K',
      estimatedValue: 0,
      hallmarkNumber: '',
      imageBase64: null,
      imageExtension: 'jpg'
    });
  }
}
