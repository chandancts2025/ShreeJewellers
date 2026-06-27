import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, startWith, switchMap } from 'rxjs';
import { GoldPrice } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';

@Component({
  selector: 'app-price-ticker',
  imports: [CommonModule],
  templateUrl: './price-ticker.html',
  styleUrl: './price-ticker.scss'
})
export class PriceTicker {
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);

  price: GoldPrice | null = null;

  constructor() {
    interval(300000)
      .pipe(
        startWith(0),
        switchMap(() => this.publicData.getPrices()),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((price) => {
        this.price = price;
      });
  }
}
