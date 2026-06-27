import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicHomeData } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss'
})
export class HomePage {
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);
  data?: PublicHomeData;

  constructor() {
    this.publicData.getHome().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }
}
