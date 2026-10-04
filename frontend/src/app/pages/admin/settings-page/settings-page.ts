import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SettingsService } from '../../../core/services/settings';
import { CatalogService } from '../../../core/services/catalog';
import { ToastService } from '../../../core/services/toast';
import { PublicDataService } from '../../../core/services/public-data';

@Component({
  selector: 'app-settings-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss'
})
export class SettingsPage {
  private readonly settings = inject(SettingsService);
  private readonly catalog = inject(CatalogService);
  private readonly publicData = inject(PublicDataService);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  users: any[] = [];
  interestRates: any[] = [];
  priceHistory: any[] = [];
  currentRates: any = null;
  isLoading = false;
  isSavingPrice = false;

  readonly shopForm = this.fb.nonNullable.group({
    shopName: [''],
    shopAddress: [''],
    gstNumber: [''],
    shopPhone: [''],
    shopEmail: [''],
    logoUrl: [''],
    whatsAppNumber: [''],
    mapEmbedUrl: [''],
    trustedSinceYear: ['1998'],
    businessHours: [''],
    aboutSummary: [''],
    aboutStory: [''],
    ownerName: [''],
    ownerTitle: [''],
    ownerPhotoUrl: ['']
  });

  readonly interestForm = this.fb.nonNullable.group({
    interestRatePercent: [12, [Validators.required, Validators.min(0)]],
    calculationType: [1, Validators.required],
    compoundingEnabled: [false],
    penaltyRatePercent: [1, [Validators.required, Validators.min(0)]],
    defaultTenureMonths: [12, [Validators.required, Validators.min(1)]],
    loanToValuePercent: [75, [Validators.required, Validators.min(1), Validators.max(100)]],
    effectiveFrom: [new Date().toISOString().slice(0, 10), Validators.required]
  });

  readonly priceSettingsForm = this.fb.nonNullable.group({
    apiKey: [''],
    refreshIntervalMinutes: ['15']
  });

  readonly manualPriceForm = this.fb.nonNullable.group({
    goldRate22KPer10g: [0, [Validators.required, Validators.min(1)]],
    goldRate24KPer10g: [0, [Validators.required, Validators.min(1)]],
    silverRatePerKg: [0, [Validators.required, Validators.min(1)]],
    reason: ['Daily opening rate override', Validators.required]
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.settings.getShopSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => this.shopForm.patchValue(data),
      error: () => this.toast.error('Settings', 'Could not load shop settings.')
    });

    this.settings.getInterestRates().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => this.interestRates = data ?? []
    });

    this.settings.getUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => this.users = data ?? []
    });

    this.settings.getGoldPriceSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => this.priceSettingsForm.patchValue(data)
    });

    this.publicData.getPrices().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (prices) => {
        this.currentRates = prices;
        this.manualPriceForm.patchValue({
          goldRate22KPer10g: prices.rate22KPer10g,
          goldRate24KPer10g: prices.rate24KPer10g,
          silverRatePerKg: prices.silverRatePerKg
        });
      }
    });

    this.loadPriceHistory();
  }

  loadPriceHistory(): void {
    const to = new Date().toISOString();
    const from = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
    this.catalog.getPriceHistory(from, to).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (history) => this.priceHistory = history ?? [],
      error: () => {}
    });
  }

  saveShop(): void {
    this.settings.updateShopSettings(this.shopForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.toast.success('Shop Settings', 'Shop settings saved successfully!'),
      error: (err) => this.toast.error('Shop Settings', err.error?.message || 'Failed to update shop settings.')
    });
  }

  saveInterest(): void {
    if (this.interestForm.invalid) {
      this.toast.error('Validation', 'Please enter valid interest rate details.');
      return;
    }
    this.settings.createInterestRate(this.interestForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Interest Rate', 'New interest rate tier activated!');
        this.load();
      },
      error: (err) => this.toast.error('Interest Rate', err.error?.message || 'Failed to save interest rate.')
    });
  }

  savePriceSettings(): void {
    this.settings.updateGoldPriceSettings(this.priceSettingsForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.toast.success('Price API', 'Price API configurations updated!'),
      error: (err) => this.toast.error('Price API', err.error?.message || 'Failed to update price settings.')
    });
  }

  setManualPriceOverride(): void {
    if (this.manualPriceForm.invalid) {
      this.toast.error('Validation', 'Please provide valid rates and a reason.');
      return;
    }
    this.isSavingPrice = true;
    const raw = this.manualPriceForm.getRawValue();
    const payload = {
      goldRate22KPer10g: Number(raw.goldRate22KPer10g),
      goldRate24KPer10g: Number(raw.goldRate24KPer10g),
      silverRatePerKg: Number(raw.silverRatePerKg),
      reason: raw.reason.trim()
    };

    this.catalog.setManualPriceOverride(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.isSavingPrice = false;
        this.toast.success('Price Override', 'Manual price override set for today!');
        this.load();
      },
      error: (err) => {
        this.isSavingPrice = false;
        this.toast.error('Price Override', err.error?.message || 'Failed to set price override.');
      }
    });
  }
}
