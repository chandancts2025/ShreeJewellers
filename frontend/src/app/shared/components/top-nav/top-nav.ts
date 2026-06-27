import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth';
import { PriceTicker } from '../price-ticker/price-ticker';

@Component({
  selector: 'app-top-nav',
  imports: [RouterLink, RouterLinkActive, PriceTicker],
  templateUrl: './top-nav.html',
  styleUrl: './top-nav.scss'
})
export class TopNav {
  protected readonly auth = inject(AuthService);
  protected readonly router = inject(Router);
  protected readonly displayName = computed(() => this.auth.session()?.fullName ?? 'Guest');

  goToDashboard(): void {
    this.auth.isAuthenticated() ? this.auth.navigateAfterLogin() : this.router.navigate(['/login']);
  }
}
