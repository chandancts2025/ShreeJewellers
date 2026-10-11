import { Component, computed, inject, signal, HostListener, ElementRef } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
  private readonly elementRef = inject(ElementRef);

  protected readonly displayName = computed(() => this.auth.session()?.fullName ?? 'Guest');
  protected readonly isNavOpen = signal(false);
  protected readonly activeDropdown = signal<string | null>(null);

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        this.closeNav();
      });
  }

  toggleNav(): void {
    this.isNavOpen.update((open) => !open);
    if (!this.isNavOpen()) {
      this.activeDropdown.set(null);
    }
  }

  closeNav(): void {
    this.isNavOpen.set(false);
    this.activeDropdown.set(null);
  }

  toggleDropdown(name: string, event: Event): void {
    event.stopPropagation();
    this.activeDropdown.update((current) => (current === name ? null : name));
  }

  closeDropdown(): void {
    this.activeDropdown.set(null);
  }

  goToDashboard(): void {
    this.closeNav();
    this.auth.isAuthenticated() ? this.auth.navigateAfterLogin() : this.router.navigate(['/login']);
  }

  logout(): void {
    this.closeNav();
    this.auth.logout();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.closeNav();
    }
  }
}
