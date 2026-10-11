import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AdminSidebar } from '../../components/admin-sidebar/admin-sidebar';
import { ToastHost } from '../../components/toast-host/toast-host';
import { AuthService } from '../../../core/services/auth';
import { PublicDataService } from '../../../core/services/public-data';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, AdminSidebar, ToastHost, RouterLink, RouterLinkActive, DecimalPipe],
  template: `
    <div class="admin-wrapper d-flex">
      <!-- Mobile Sidebar Toggle Button -->
      <button
        class="sidebar-toggle-btn d-lg-none"
        type="button"
        (click)="toggleSidebar()"
        [class.active]="sidebarVisible()"
        aria-label="Toggle navigation menu">
        <i class="bi bi-list"></i>
      </button>

      <!-- Mobile Backdrop Overlay -->
      @if (sidebarVisible()) {
        <div class="sidebar-overlay d-lg-none" (click)="closeSidebar()"></div>
      }

      <!-- Flush Left Sidebar (Starts at Top: 0) -->
      <app-admin-sidebar
        [isVisible]="sidebarVisible()"
        (closeSidebar)="closeSidebar()">
      </app-admin-sidebar>

      <!-- Main Column: Topbar + Dynamic Page Content -->
      <div class="admin-main-container flex-grow-1 d-flex flex-column min-vh-100">
        <!-- Luxury High-Contrast Admin Topbar -->
        <header class="admin-topbar">
          <div class="topbar-left d-flex align-items-center gap-2 gap-lg-3">
            <!-- Mobile Brand Title -->
            <a routerLink="/" class="d-lg-none text-decoration-none d-flex align-items-center gap-2">
              <span class="mobile-gem"><i class="bi bi-gem"></i></span>
              <span class="mobile-brand-name font-display">Shree Jewellers</span>
            </a>

            <!-- Live Gold Rate Ticker -->
            @if (rate22k() > 0) {
              <div class="live-rate-badge d-none d-md-flex align-items-center gap-2" title="Real-time Gold Board Rate">
                <span class="rate-pulse-dot"></span>
                <span class="rate-label">Live Gold:</span>
                <span class="rate-text">22K <strong class="rate-gold">₹{{ rate22k() | number:'1.0-0' }}</strong>/g</span>
                <span class="rate-divider">|</span>
                <span class="rate-text">24K <strong class="rate-gold">₹{{ rate24k() | number:'1.0-0' }}</strong>/g</span>
              </div>
            }
          </div>

          <!-- Center Navigation Links (High-Contrast, Zero Color Overlap) -->
          <nav class="topbar-nav d-none d-xl-flex align-items-center gap-1">
            <a routerLink="/admin/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" class="topbar-link">
              <i class="bi bi-speedometer2"></i> Dashboard
            </a>
            <a routerLink="/admin/sales" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" class="topbar-link">
              <i class="bi bi-receipt-cutoff"></i> POS Billing
            </a>
            <a routerLink="/admin/loans" routerLinkActive="active" class="topbar-link">
              <i class="bi bi-cash-coin"></i> Gold Loans
            </a>
            <a routerLink="/admin/inventory" routerLinkActive="active" class="topbar-link">
              <i class="bi bi-box2-heart-fill"></i> Inventory
            </a>
            <a routerLink="/admin/sales/list" routerLinkActive="active" class="topbar-link">
              <i class="bi bi-card-list"></i> Orders
            </a>
            <a routerLink="/admin/reports" routerLinkActive="active" class="topbar-link">
              <i class="bi bi-graph-up-arrow"></i> Reports
            </a>
          </nav>

          <!-- Right Side Actions & Profile -->
          <div class="topbar-right d-flex align-items-center gap-2 gap-sm-3">
            <!-- Standout Radiant Gold "Visit Home" Button -->
            <a routerLink="/" class="btn-home-storefront" title="Return to Public Customer Storefront">
              <i class="bi bi-house-door-fill"></i>
              <span class="btn-home-text">Storefront</span>
            </a>

            <!-- Admin Profile Chip -->
            <div class="user-chip d-none d-sm-flex align-items-center gap-2">
              <div class="user-chip-avatar">
                <i class="bi bi-person-fill"></i>
              </div>
              <div class="user-chip-meta d-none d-lg-block text-start">
                <div class="user-chip-name">{{ userName() }}</div>
                <div class="user-chip-role">{{ userRole() }}</div>
              </div>
            </div>

            <!-- High-Contrast Logout Button -->
            <button class="btn-topbar-logout" (click)="logout()" title="Sign Out of Admin Console">
              <i class="bi bi-box-arrow-right"></i>
              <span class="d-none d-sm-inline">Logout</span>
            </button>
          </div>
        </header>

        <!-- Dynamic Admin Content Viewport -->
        <main class="admin-viewport flex-grow-1">
          <app-toast-host></app-toast-host>
          <router-outlet></router-outlet>
        </main>
      </div>
    </div>
  `,
  styles: [`
    .admin-wrapper {
      min-height: 100vh;
      position: relative;
      background: #faf8f2;
    }

    .admin-main-container {
      min-width: 0;
      background: #faf8f2;
    }

    /* ── Topbar Styling ── */
    .admin-topbar {
      height: 64px;
      min-height: 64px;
      background: linear-gradient(90deg, #131326 0%, #1a1a38 50%, #14142b 100%);
      border-bottom: 1px solid rgba(201, 168, 76, 0.25);
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.28);
      padding: 0 1.25rem;
      display: flex;
      align-items: center;
      justify-content: space-between;
      position: sticky;
      top: 0;
      z-index: 1000;
    }

    .mobile-gem {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: linear-gradient(135deg, #c9a84c 0%, #ffd700 100%);
      color: #121224;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 1rem;
    }

    .mobile-brand-name {
      color: #ffd700;
      font-size: 1.2rem;
      font-weight: 700;
    }

    /* ── Live Rate Badge ── */
    .live-rate-badge {
      background: rgba(201, 168, 76, 0.12);
      border: 1px solid rgba(255, 215, 0, 0.3);
      padding: 0.35rem 0.85rem;
      border-radius: 999px;
      font-size: 0.82rem;
      color: #e2e8f0;
    }

    .rate-pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10b981;
      box-shadow: 0 0 8px #10b981;
      animation: pulse-glow 2s infinite ease-in-out;
    }

    @keyframes pulse-glow {
      0%, 100% { opacity: 1; transform: scale(1); }
      50% { opacity: 0.5; transform: scale(0.85); }
    }

    .rate-label {
      color: #94a3b8;
      font-weight: 600;
      text-transform: uppercase;
      font-size: 0.72rem;
      letter-spacing: 0.05em;
    }

    .rate-text {
      color: #cbd5e1;
    }

    .rate-gold {
      color: #ffd700;
      font-weight: 700;
    }

    .rate-divider {
      color: rgba(255, 215, 0, 0.4);
    }

    /* ── Topbar Navigation Links (High Contrast, Zero Overlap) ── */
    .topbar-nav {
      gap: 0.35rem;
    }

    .topbar-link {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      padding: 0.45rem 0.85rem;
      border-radius: 8px;
      color: #e2e8f0;
      font-size: 0.88rem;
      font-weight: 600;
      text-decoration: none;
      transition: all 0.2s ease;
      border: 1px solid transparent;

      i {
        font-size: 1rem;
        color: #f6d87c;
        transition: color 0.2s ease;
      }

      &:hover {
        color: #ffd700;
        background: rgba(255, 215, 0, 0.12);
        border-color: rgba(255, 215, 0, 0.25);

        i {
          color: #ffd700;
        }
      }

      &.active {
        color: #ffd700;
        background: rgba(255, 215, 0, 0.18);
        border-color: rgba(255, 215, 0, 0.4);
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.25);

        i {
          color: #ffd700;
        }
      }
    }

    /* ── Standout "Public Home" Storefront Button ── */
    .btn-home-storefront {
      background: linear-gradient(135deg, #ffd700 0%, #c9a84c 100%);
      color: #121224 !important;
      font-weight: 700;
      font-size: 0.85rem;
      padding: 0.45rem 1rem;
      border-radius: 999px;
      text-decoration: none;
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      box-shadow: 0 2px 10px rgba(201, 168, 76, 0.4);
      border: 1px solid #ffd700;
      transition: all 0.2s ease;

      i {
        font-size: 1rem;
      }

      &:hover {
        background: linear-gradient(135deg, #fff176 0%, #ffd700 100%);
        color: #000000 !important;
        transform: translateY(-1px);
        box-shadow: 0 4px 16px rgba(201, 168, 76, 0.6);
      }
    }

    /* ── User Chip ── */
    .user-chip {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.16);
      border-radius: 999px;
      padding: 0.25rem 0.85rem 0.25rem 0.35rem;
    }

    .user-chip-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: linear-gradient(135deg, #c9a84c 0%, #b8912d 100%);
      color: #121224;
      font-size: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid #ffd700;
      flex-shrink: 0;
    }

    .user-chip-name {
      font-size: 0.82rem;
      font-weight: 700;
      color: #f8fafc;
      line-height: 1.2;
    }

    .user-chip-role {
      font-size: 0.68rem;
      color: #ffd700;
      text-transform: uppercase;
      font-weight: 600;
      letter-spacing: 0.05em;
      line-height: 1;
    }

    /* ── Logout Button ── */
    .btn-topbar-logout {
      background: rgba(239, 68, 68, 0.15);
      color: #fca5a5 !important;
      border: 1px solid rgba(239, 68, 68, 0.4);
      border-radius: 999px;
      padding: 0.4rem 0.85rem;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s ease;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;

      &:hover {
        background: #ef4444;
        color: #ffffff !important;
        border-color: #ef4444;
        box-shadow: 0 0 12px rgba(239, 68, 68, 0.4);
        transform: translateY(-1px);
      }
    }

    /* ── Mobile Sidebar Toggle Button ── */
    .sidebar-toggle-btn {
      position: fixed;
      bottom: 20px;
      right: 20px;
      z-index: 1050;
      background: linear-gradient(135deg, #c9a84c 0%, #ffd700 100%);
      border: none;
      color: #121224;
      width: 50px;
      height: 50px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
      cursor: pointer;
      transition: all 0.3s ease;
      box-shadow: 0 6px 20px rgba(0, 0, 0, 0.4);

      &:hover {
        transform: scale(1.08);
      }

      &.active {
        background: #ef4444;
        color: #ffffff;
      }
    }

    .sidebar-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.65);
      z-index: 1030;
      backdrop-filter: blur(3px);
    }

    .admin-viewport {
      padding: 0;
      min-height: calc(100vh - 64px);
    }

    @media (min-width: 993px) {
      .sidebar-toggle-btn,
      .sidebar-overlay {
        display: none !important;
      }
    }
  `]
})
export class AdminLayout implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);

  sidebarVisible = signal(false);
  rate22k = signal(6646);
  rate24k = signal(7250);

  userName = signal('Chandan Kr Mandal');
  userRole = signal('SuperAdmin');

  ngOnInit(): void {
    const session = this.auth.session();
    if (session) {
      if (session.fullName) this.userName.set(session.fullName);
      if (session.roles && session.roles.length > 0) this.userRole.set(session.roles[0]);
    }

    this.publicData.getPrices().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (price) => {
        if (price) {
          if (price.rate22KPerGram > 0) this.rate22k.set(Math.round(price.rate22KPerGram));
          if (price.rate24KPerGram > 0) this.rate24k.set(Math.round(price.rate24KPerGram));
        }
      },
      error: () => undefined
    });
  }

  toggleSidebar(): void {
    this.sidebarVisible.set(!this.sidebarVisible());
  }

  closeSidebar(): void {
    this.sidebarVisible.set(false);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
