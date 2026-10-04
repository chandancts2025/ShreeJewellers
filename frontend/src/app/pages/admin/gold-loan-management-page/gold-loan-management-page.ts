import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators, FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LoanService } from '../../../core/services/loan';
import { AdminService } from '../../../core/services/admin';
import { ToastService } from '../../../core/services/toast';
import { CustomerListItem, GoldLoanResponse, OutstandingBalance } from '../../../core/models';

type LoanTab = 'active' | 'due' | 'defaulted';

@Component({
  selector: 'app-gold-loan-management-page',
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './gold-loan-management-page.html',
  styleUrl: './gold-loan-management-page.scss'
})
export class GoldLoanManagementPage {
  private readonly loanService = inject(LoanService);
  private readonly adminService = inject(AdminService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  activeTab: LoanTab = 'active';
  activeLoans: any[] = [];
  dueThisWeek: any[] = [];
  defaultedLoans: any[] = [];
  customers: CustomerListItem[] = [];
  searchTerm = '';

  selectedLoan: any = null;
  selectedLoanDetail: GoldLoanResponse | null = null;
  selectedLoanOutstanding: OutstandingBalance | null = null;
  selectedLoanRepayments: any[] = [];
  eligibilityResult: any = null;
  actionMessage = '';
  isCheckingDefaults = false;
  isProcessingPayment = false;

  // Modals for Pawn Ticket and Repayment Receipt Printing
  showPawnTicketModal = false;
  selectedReceiptForPrint: any = null;

  readonly createLoanForm = this.fb.nonNullable.group({
    customerUserId: ['', Validators.required],
    loanDate: [new Date().toISOString().slice(0, 10), Validators.required],
    principalAmount: [0, [Validators.required, Validators.min(1)]],
    goldPurity: ['22K', Validators.required],
    notes: ['']
  });

  readonly loanItemForm = this.fb.group({
    itemDescription: ['Gold chain', Validators.required],
    weightGrams: [0, [Validators.required, Validators.min(0.1)]],
    purity: ['22K', Validators.required],
    estimatedValue: [0, [Validators.required, Validators.min(1)]],
    hallmarkNumber: [''],
    imageBase64: [null as string | null],
    imageExtension: ['jpg']
  });

  readonly eligibilityForm = this.fb.nonNullable.group({
    customerUserId: ['', Validators.required],
    itemDescription: ['Gold item', Validators.required],
    weightGrams: [0, [Validators.required, Validators.min(0.1)]],
    purity: ['22K', Validators.required],
    estimatedValue: [0]
  });

  readonly repaymentForm = this.fb.nonNullable.group({
    amountPaid: [0, [Validators.required, Validators.min(1)]],
    paymentMode: ['Cash', Validators.required],
    notes: ['']
  });

  readonly extendForm = this.fb.nonNullable.group({
    newMaturityDate: [new Date(new Date().setMonth(new Date().getMonth() + 3)).toISOString().slice(0, 10), Validators.required],
    reason: ['Loan extension requested by customer', Validators.required]
  });

  readonly closeForm = this.fb.nonNullable.group({
    goldReturned: [true],
    notes: ['Customer settled balance and retrieved pledged gold ornaments.']
  });

  readonly auctionForm = this.fb.nonNullable.group({
    auctionProceeds: [0, [Validators.required, Validators.min(1)]],
    auctionDate: [new Date().toISOString().slice(0, 10), Validators.required],
    notes: ['Auctioned pledged gold to recover outstanding balance.']
  });

  constructor() {
    this.load();
    this.loadCustomers();
  }

  load(): void {
    this.loanService.getActiveLoans(this.searchTerm).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.activeLoans = response ?? [],
      error: () => this.toast.error('Loans', 'Failed to load active loans.')
    });

    this.loanService.getDueThisWeek().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => this.dueThisWeek = response ?? [],
      error: () => this.toast.error('Loans', 'Failed to load loans due this week.')
    });

    this.loanService.getDefaulted().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response: any) => this.defaultedLoans = response ?? [],
      error: () => this.toast.error('Loans', 'Failed to load defaulted loans.')
    });
  }

  private loadCustomers(): void {
    this.adminService.getCustomers({ pageSize: 100 }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => this.customers = res?.items ?? res?.data ?? [],
      error: () => this.toast.error('Customers', 'Failed to load customers.')
    });
  }

  setTab(tab: LoanTab): void {
    this.activeTab = tab;
  }

  onSearch(): void {
    this.load();
  }

  selectLoan(loan: any): void {
    this.selectedLoan = loan;
    this.actionMessage = '';
    const loanId = loan.id;

    this.loanService.getLoan(loanId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => this.selectedLoanDetail = res,
      error: () => this.toast.error('Detail Error', 'Could not load loan details.')
    });

    this.loanService.getOutstanding(loanId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.selectedLoanOutstanding = res;
        this.repaymentForm.patchValue({ amountPaid: res.totalOutstanding });
      },
      error: () => this.toast.error('Balance Error', 'Could not fetch outstanding balance.')
    });

    this.loanService.getRepayments(loanId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => this.selectedLoanRepayments = res ?? [],
      error: () => this.toast.error('Repayment History', 'Could not load repayments.')
    });
  }

  clearSelectedLoan(): void {
    this.selectedLoan = null;
    this.selectedLoanDetail = null;
    this.selectedLoanOutstanding = null;
    this.selectedLoanRepayments = [];
    this.actionMessage = '';
  }

  createLoan(): void {
    if (this.createLoanForm.invalid || this.loanItemForm.invalid) {
      this.createLoanForm.markAllAsTouched();
      this.loanItemForm.markAllAsTouched();
      this.toast.error('Invalid Form', 'Please provide customer, disbursement amount, and pledged ornament details.');
      return;
    }

    const val = this.createLoanForm.getRawValue();
    const item = this.loanItemForm.getRawValue();

    const payload = {
      customerUserId: val.customerUserId,
      loanDate: val.loanDate,
      principalAmount: Number(val.principalAmount),
      goldPurity: val.goldPurity,
      notes: val.notes,
      goldItems: [
        {
          itemDescription: item.itemDescription ?? 'Pledged Ornament',
          weightGrams: Number(item.weightGrams),
          purity: item.purity ?? '22K',
          estimatedValue: Number(item.estimatedValue),
          hallmarkNumber: item.hallmarkNumber || null,
          imageBase64: item.imageBase64 || null,
          imageExtension: item.imageExtension || 'jpg'
        }
      ]
    };

    this.loanService.createLoan(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (loan) => {
        this.toast.success('Loan Created', `Gold Loan ${loan.loanNumber} sanctioned successfully.`);
        this.resetCreateForm();
        this.load();
        this.selectLoan(loan);
      },
      error: (err) => this.toast.error('Loan Failed', err.error?.message || 'Could not issue gold loan.')
    });
  }

  checkEligibility(): void {
    if (this.eligibilityForm.invalid) {
      this.eligibilityForm.markAllAsTouched();
      return;
    }

    const val = this.eligibilityForm.getRawValue();
    const payload = {
      customerUserId: val.customerUserId,
      items: [
        {
          itemDescription: val.itemDescription,
          weightGrams: Number(val.weightGrams),
          purity: val.purity,
          estimatedValue: Number(val.estimatedValue)
        }
      ]
    };

    this.loanService.checkEligibility(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => this.eligibilityResult = res,
      error: (err) => this.toast.error('Eligibility Error', err.error?.message || 'Failed to calculate eligibility.')
    });
  }

  recordRepayment(): void {
    if (!this.selectedLoan || this.repaymentForm.invalid) {
      this.repaymentForm.markAllAsTouched();
      return;
    }

    this.isProcessingPayment = true;
    const val = this.repaymentForm.getRawValue();
    const payload = {
      loanId: this.selectedLoan.id,
      amountPaid: Number(val.amountPaid),
      paymentMode: val.paymentMode,
      notes: val.notes
    };

    this.loanService.recordRepayment(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (receipt) => {
        this.isProcessingPayment = false;
        this.toast.success('Repayment Recorded', `Receipt #${receipt.receiptNumber} generated.`);
        this.actionMessage = `Receipt #${receipt.receiptNumber} processed. New balance: ₹${receipt.principalBalanceAfter.toLocaleString('en-IN')}`;
        this.load();
        this.selectLoan(this.selectedLoan);
      },
      error: (err) => {
        this.isProcessingPayment = false;
        this.toast.error('Repayment Failed', err.error?.message || 'Could not process repayment.');
      }
    });
  }

  runDefaultCheck(): void {
    this.isCheckingDefaults = true;
    this.loanService.checkDefaults().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isCheckingDefaults = false;
        this.toast.success('Default Check Complete', res?.message || 'Defaulted and overdue loans updated.');
        this.load();
      },
      error: (err) => {
        this.isCheckingDefaults = false;
        this.toast.error('Check Error', err.error?.message || 'Failed to run default checks.');
      }
    });
  }

  extendLoan(): void {
    if (!this.selectedLoan || this.extendForm.invalid) return;

    const payload = {
      loanId: this.selectedLoan.id,
      newMaturityDate: this.extendForm.controls.newMaturityDate.value,
      reason: this.extendForm.controls.reason.value
    };

    this.loanService.extendLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Loan Extended', 'Loan maturity date extended.');
        this.actionMessage = 'Loan maturity date extended.';
        this.load();
        this.selectLoan(this.selectedLoan);
      },
      error: (err) => this.toast.error('Extend Failed', err.error?.message || 'Extension failed.')
    });
  }

  closeLoan(): void {
    if (!this.selectedLoan) return;

    const payload = {
      loanId: this.selectedLoan.id,
      notes: this.closeForm.controls.notes.value,
      goldReturned: this.closeForm.controls.goldReturned.value
    };

    this.loanService.closeLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Loan Closed', 'Loan closed successfully.');
        this.actionMessage = 'Loan closed successfully and collateral released.';
        this.load();
        this.selectLoan(this.selectedLoan);
      },
      error: (err) => this.toast.error('Close Failed', err.error?.message || 'Failed to close loan.')
    });
  }

  auctionLoan(): void {
    if (!this.selectedLoan || this.auctionForm.invalid) return;

    const payload = {
      loanId: this.selectedLoan.id,
      auctionProceeds: Number(this.auctionForm.controls.auctionProceeds.value),
      auctionDate: this.auctionForm.controls.auctionDate.value,
      notes: this.auctionForm.controls.notes.value
    };

    this.loanService.auctionLoan(this.selectedLoan.id, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Auction Recorded', 'Auction recorded and loan settled.');
        this.actionMessage = 'Auction recorded and loan closed.';
        this.load();
        this.selectLoan(this.selectedLoan);
      },
      error: (err) => this.toast.error('Auction Failed', err.error?.message || 'Failed to record auction.')
    });
  }

  // ── Print Actions ──────────────────────────────────────────────────────────
  printPawnTicket(): void {
    if (!this.selectedLoan) return;
    this.showPawnTicketModal = true;
  }

  closePawnTicket(): void {
    this.showPawnTicketModal = false;
  }

  printReceipt(receipt: any): void {
    this.selectedReceiptForPrint = receipt;
  }

  closeReceipt(): void {
    this.selectedReceiptForPrint = null;
  }

  executePrint(): void {
    window.print();
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
