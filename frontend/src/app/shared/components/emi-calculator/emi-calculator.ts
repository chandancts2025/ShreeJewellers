import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-emi-calculator',
  imports: [CommonModule, FormsModule],
  templateUrl: './emi-calculator.html',
  styleUrl: './emi-calculator.scss'
})
export class EmiCalculator {
  principal = 100000;
  annualRate = 12;
  tenureMonths = 12;
  paymentFrequency: 'weekly' | 'monthly' | 'quarterly' | 'yearly' = 'monthly';

  readonly frequencyOptions = [
    { value: 'weekly', label: 'Weekly', periodsPerYear: 52, display: 'week' },
    { value: 'monthly', label: 'Monthly', periodsPerYear: 12, display: 'month' },
    { value: 'quarterly', label: 'Quarterly', periodsPerYear: 4, display: 'quarter' },
    { value: 'yearly', label: 'Yearly', periodsPerYear: 1, display: 'year' }
  ] as const;

  get selectedFrequency() {
    return this.frequencyOptions.find(option => option.value === this.paymentFrequency) ?? this.frequencyOptions[1];
  }

  get periodicRate(): number {
    return this.annualRate / this.selectedFrequency.periodsPerYear / 100;
  }

  get totalPayments(): number {
    return Math.max(1, Math.round(this.tenureMonths * this.selectedFrequency.periodsPerYear / 12));
  }

  get emi(): number {
    const rate = this.periodicRate;
    const periods = this.totalPayments;
    if (!rate) {
      return this.principal / periods;
    }
    const factor = Math.pow(1 + rate, periods);
    return (this.principal * rate * factor) / (factor - 1);
  }

  get totalRepayment(): number {
    return this.emi * this.totalPayments;
  }

  get totalInterest(): number {
    return this.totalRepayment - this.principal;
  }

  get rateLabel(): string {
    return `${(this.periodicRate * 100).toFixed(3)}% per ${this.selectedFrequency.display}`;
  }
}
