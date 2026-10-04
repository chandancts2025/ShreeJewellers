import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PublicDataService } from '../../../core/services/public-data';

export interface AmortizationRow {
  period: number;
  openingBalance: number;
  principalComponent: number;
  interestComponent: number;
  totalPayment: number;
  closingBalance: number;
}

@Component({
  selector: 'app-emi-calculator',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './emi-calculator.html',
  styleUrl: './emi-calculator.scss'
})
export class EmiCalculator implements OnInit {
  private readonly publicData = inject(PublicDataService);

  // Active view: 'eligibility' (Gold to Cash) or 'repayment' (Loan EMI)
  activeTab: 'eligibility' | 'repayment' = 'eligibility';

  // ── Gold Eligibility State ──
  purity: '22K' | '24K' | '18K' = '22K';
  goldWeightGrams: number = 25;
  goldRatePerGram: number = 6650;
  ltvPercent: number = 75; // RBI maximum is 75%

  // ── Repayment / EMI State ──
  principal: number = 100000;
  annualRate: number = 12; // 12% p.a. = 1% per month
  tenureMonths: number = 12;
  loanScheme: 'bullet' | 'emi' = 'bullet'; // 'bullet' is standard interest-first pawn loan
  showAmortization: boolean = false;

  readonly quickWeights = [5, 10, 20, 50, 100];
  readonly quickPrincipals = [25000, 50000, 100000, 250000, 500000];
  readonly quickTenures = [3, 6, 12, 18, 24];

  ngOnInit(): void {
    this.fetchLivePrices();
  }

  fetchLivePrices(): void {
    this.publicData.getPrices().subscribe({
      next: (price) => {
        if (price) {
          if (this.purity === '22K' && price.rate22KPerGram > 0) {
            this.goldRatePerGram = Math.round(price.rate22KPerGram);
          } else if (this.purity === '24K' && price.rate24KPerGram > 0) {
            this.goldRatePerGram = Math.round(price.rate24KPerGram);
          } else if (this.purity === '18K' && price.rate24KPerGram > 0) {
            this.goldRatePerGram = Math.round(price.rate24KPerGram * 0.75);
          }
        }
      },
      error: () => {
        // Fallback to default market rate
        this.goldRatePerGram = 6650;
      }
    });
  }

  onPurityChange(purity: '22K' | '24K' | '18K'): void {
    this.purity = purity;
    this.fetchLivePrices();
  }

  addWeight(grams: number): void {
    this.goldWeightGrams = Math.min(500, (this.goldWeightGrams || 0) + grams);
  }

  setWeight(grams: number): void {
    this.goldWeightGrams = grams;
  }

  setPrincipal(amount: number): void {
    this.principal = amount;
  }

  setTenure(months: number): void {
    this.tenureMonths = months;
  }

  // ── Computations: Gold Eligibility ──
  get totalGoldValuation(): number {
    return Math.max(0, (this.goldWeightGrams || 0) * (this.goldRatePerGram || 0));
  }

  get maxEligibleLoan(): number {
    return Math.round(this.totalGoldValuation * ((this.ltvPercent || 75) / 100));
  }

  transferToLoanCalculator(): void {
    this.principal = this.maxEligibleLoan;
    this.activeTab = 'repayment';
  }

  // ── Computations: Loan Repayment ──
  get monthlyInterestRate(): number {
    return (this.annualRate || 0) / 12 / 100;
  }

  /**
   * Monthly payable:
   * If 'emi': Standard reducing balance EMI
   * If 'bullet': Only interest payment per month
   */
  get monthlyPayment(): number {
    const P = this.principal || 0;
    const r = this.monthlyInterestRate;
    const n = Math.max(1, this.tenureMonths || 1);

    if (this.loanScheme === 'bullet') {
      return Math.round(P * r);
    } else {
      if (r === 0) return Math.round(P / n);
      const factor = Math.pow(1 + r, n);
      return Math.round((P * r * factor) / (factor - 1));
    }
  }

  get totalInterest(): number {
    const P = this.principal || 0;
    const n = Math.max(1, this.tenureMonths || 1);

    if (this.loanScheme === 'bullet') {
      return Math.round(P * this.monthlyInterestRate * n);
    } else {
      return Math.round(this.monthlyPayment * n - P);
    }
  }

  get totalRepayment(): number {
    return (this.principal || 0) + this.totalInterest;
  }

  get principalPercentage(): number {
    const total = this.totalRepayment;
    if (!total) return 100;
    return Math.min(100, Math.round(((this.principal || 0) / total) * 100));
  }

  get interestPercentage(): number {
    return 100 - this.principalPercentage;
  }

  get amortizationSchedule(): AmortizationRow[] {
    const rows: AmortizationRow[] = [];
    const n = Math.max(1, this.tenureMonths || 1);
    let balance = this.principal || 0;
    const r = this.monthlyInterestRate;
    const emi = this.monthlyPayment;

    for (let month = 1; month <= n; month++) {
      const interest = Math.round(balance * r);
      let principalComp = 0;
      let payment = 0;

      if (this.loanScheme === 'bullet') {
        if (month === n) {
          principalComp = balance;
          payment = interest + principalComp;
          balance = 0;
        } else {
          principalComp = 0;
          payment = interest;
        }
      } else {
        principalComp = Math.min(balance, emi - interest);
        payment = principalComp + interest;
        balance = Math.max(0, balance - principalComp);
      }

      rows.push({
        period: month,
        openingBalance: balance + principalComp,
        principalComponent: principalComp,
        interestComponent: interest,
        totalPayment: payment,
        closingBalance: balance
      });
    }

    return rows;
  }
}
