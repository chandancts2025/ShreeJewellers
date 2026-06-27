import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicAboutData } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';

@Component({
  selector: 'app-about-page',
  imports: [],
  templateUrl: './about-page.html',
  styleUrl: './about-page.scss'
})
export class AboutPage {
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);
  data?: PublicAboutData;

  constructor() {
    this.publicData.getAbout().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }
}
