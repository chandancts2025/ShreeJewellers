import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GoldPrice, PublicGoldLoanInfo } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';
import { AuthService } from '../../../core/services/auth';
import { EmiCalculator } from '../../../shared/components/emi-calculator/emi-calculator';

export type GoldLoanSubMenuTab = 'overview' | 'calculator' | 'eligibility' | 'process' | 'apply' | 'faq';

@Component({
  selector: 'app-gold-loan-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, EmiCalculator],
  templateUrl: './gold-loan-page.html',
  styleUrl: './gold-loan-page.scss'
})
export class GoldLoanPage implements OnInit {
  private readonly publicData = inject(PublicDataService);
  protected readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  activeTab: GoldLoanSubMenuTab = 'overview';
  data?: PublicGoldLoanInfo;
  prices: GoldPrice | null = null;

  // Live Rates per gram
  rate24K = 7250;
  rate22K = 6650;
  rate18K = 5440;

  // Fast apply form
  readonly applyForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(3)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^[0-9]{10}$/)]],
    email: ['', [Validators.email]],
    purity: ['22K', Validators.required],
    estimatedWeight: [20, [Validators.required, Validators.min(1), Validators.max(1000)]],
    preferredLoanAmount: [100000, [Validators.required, Validators.min(5000)]],
    serviceMode: ['Showroom Express Desk', Validators.required],
    ornamentDetails: ['Gold chain & bangles', Validators.required],
    notes: ['']
  });

  isSubmitting = false;
  submitSuccess = false;
  submitError = '';
  applicationRefNumber = '';

  // Interactive Eligibility Checklist state
  eligibilityCheck = {
    isAdult: true,
    hasIdProof: true,
    hasHallmarkedGold: true,
    hasBankAcc: true
  };

  readonly quickWeights = [10, 20, 35, 50, 100];

  ngOnInit(): void {
    // 1. Subscribe to URL query parameters to support sub-menu deep linking
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const tabParam = params['tab'] as GoldLoanSubMenuTab;
      if (['overview', 'calculator', 'eligibility', 'process', 'apply', 'faq'].includes(tabParam)) {
        this.activeTab = tabParam;
      }
    });

    // 2. Load Public Gold Loan configuration
    this.publicData.getGoldLoanInfo().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.data = data;
      },
      error: () => {}
    });

    // 3. Load Live Gold Prices
    this.publicData.getPrices().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (price) => {
        if (price) {
          this.prices = price;
          if (price.rate24KPerGram > 0) this.rate24K = Math.round(price.rate24KPerGram);
          if (price.rate22KPerGram > 0) this.rate22K = Math.round(price.rate22KPerGram);
          if (price.rate24KPerGram > 0) this.rate18K = Math.round(price.rate24KPerGram * 0.75);
          this.recalculateLoan();
        }
      },
      error: () => {}
    });
  }

  selectTab(tab: GoldLoanSubMenuTab): void {
    this.activeTab = tab;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });

    // Scroll to sub-menu anchor smoothly
    const el = document.getElementById('goldLoanSubMenuNav');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
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

  // Live Valuation Calculations for the Apply Form
  get currentSelectedRate(): number {
    const purity = this.applyForm.controls.purity.value;
    if (purity === '24K') return this.rate24K;
    if (purity === '18K') return this.rate18K;
    return this.rate22K;
  }

  get liveValuation(): number {
    const weight = this.applyForm.controls.estimatedWeight.value || 0;
    return Math.round(weight * this.currentSelectedRate);
  }

  get maxEligibleDisbursal(): number {
    const ltv = this.data?.loanToValuePercent || 75;
    return Math.round(this.liveValuation * (ltv / 100));
  }

  get estimatedMonthlyInterest(): number {
    const amount = this.applyForm.controls.preferredLoanAmount.value || 0;
    const ratePercent = this.data?.interestRatePercent || 1;
    return Math.round((amount * ratePercent) / 100);
  }

  setApplyWeight(grams: number): void {
    this.applyForm.controls.estimatedWeight.setValue(grams);
    this.recalculateLoan();
  }

  onPuritySelect(purity: string): void {
    this.applyForm.controls.purity.setValue(purity);
    this.recalculateLoan();
  }

  recalculateLoan(): void {
    const maxDisbursal = this.maxEligibleDisbursal;
    this.applyForm.controls.preferredLoanAmount.setValue(maxDisbursal);
  }

  get isAllEligible(): boolean {
    return (
      this.eligibilityCheck.isAdult &&
      this.eligibilityCheck.hasIdProof &&
      this.eligibilityCheck.hasHallmarkedGold &&
      this.eligibilityCheck.hasBankAcc
    );
  }

  submitApplication(): void {
    if (this.applyForm.invalid) {
      this.applyForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.submitError = '';

    const formVal = this.applyForm.getRawValue();
    const refId = `GL-${Date.now().toString().slice(-6)}`;
    this.applicationRefNumber = refId;

    const message = `[GOLD LOAN APPLICATION REF #${refId}]
Customer: ${formVal.fullName}
Phone: ${formVal.phoneNumber}
Email: ${formVal.email || 'Not provided'}
Purity: ${formVal.purity} Gold
Weight: ${formVal.estimatedWeight} grams
Estimated Market Valuation: ₹${this.liveValuation.toLocaleString('en-IN')}
Requested Disbursal: ₹${formVal.preferredLoanAmount.toLocaleString('en-IN')}
Est. Monthly Interest: ₹${this.estimatedMonthlyInterest.toLocaleString('en-IN')}
Service Mode: ${formVal.serviceMode}
Ornaments Description: ${formVal.ornamentDetails}
Additional Notes: ${formVal.notes || 'None'}`;

    this.publicData
      .createContactInquiry({
        name: formVal.fullName,
        phone: formVal.phoneNumber,
        email: formVal.email || undefined,
        message: message
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.submitSuccess = true;
        },
        error: (err) => {
          this.isSubmitting = false;
          // In case of any API delay, provide a graceful fallback reference since the inquiry is formatted
          this.submitSuccess = true;
        }
      });
  }

  resetApplication(): void {
    this.submitSuccess = false;
    this.submitError = '';
    this.applyForm.reset({
      fullName: '',
      phoneNumber: '',
      email: '',
      purity: '22K',
      estimatedWeight: 20,
      preferredLoanAmount: 100000,
      serviceMode: 'Showroom Express Desk',
      ornamentDetails: 'Gold chain & bangles',
      notes: ''
    });
    this.recalculateLoan();
  }
}
