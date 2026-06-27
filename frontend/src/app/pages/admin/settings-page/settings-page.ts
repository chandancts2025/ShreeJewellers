import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SettingsService } from '../../../core/services/settings';

@Component({
  selector: 'app-settings-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './settings-page.html',
  styleUrl: './settings-page.scss'
})
export class SettingsPage {
  private readonly settings = inject(SettingsService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  users: any[] = [];
  interestRates: any[] = [];

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
    interestRatePercent: [12],
    calculationType: [1],
    compoundingEnabled: [false],
    penaltyRatePercent: [1],
    defaultTenureMonths: [12],
    loanToValuePercent: [75],
    effectiveFrom: [new Date().toISOString().slice(0, 10)]
  });

  readonly priceSettingsForm = this.fb.nonNullable.group({
    apiKey: [''],
    refreshIntervalMinutes: ['15']
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.settings.getShopSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => this.shopForm.patchValue(data));
    this.settings.getInterestRates().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => this.interestRates = data ?? []);
    this.settings.getUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => this.users = data ?? []);
    this.settings.getGoldPriceSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => this.priceSettingsForm.patchValue(data));
  }

  saveShop(): void {
    this.settings.updateShopSettings(this.shopForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  saveInterest(): void {
    this.settings.createInterestRate(this.interestForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load());
  }

  savePriceSettings(): void {
    this.settings.updateGoldPriceSettings(this.priceSettingsForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }
}
