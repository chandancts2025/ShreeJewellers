import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth';

@Component({
  selector: 'app-admin-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="admin-sidebar" [class.show]="isVisible()">
      <!-- Brand Header (Flush at Top, 0px Gap) -->
      <div class="sidebar-brand-header">
        <a routerLink="/admin/dashboard" (click)="closeOnMobile()" class="brand-link">
          <div class="brand-gem-icon">
            <i class="bi bi-gem"></i>
          </div>
          <div class="brand-info">
            <div class="brand-name">Shree Jewellers</div>
            <div class="brand-tagline">Management Console</div>
          </div>
        </a>

        <!-- Close button for mobile -->
        <button
          class="sidebar-close-btn d-lg-none"
          type="button"
          (click)="closeSidebar.emit()"
          aria-label="Close sidebar">
          <i class="bi bi-x-lg"></i>
        </button>
      </div>

      <!-- Quick Storefront Link -->
      <div class="px-3 pt-3 pb-1">
        <a class="sidebar-storefront-banner" routerLink="/" (click)="closeOnMobile()" title="Visit Customer Facing Storefront">
          <div class="d-flex align-items-center gap-2">
            <i class="bi bi-house-door-fill text-gold-glow fs-5"></i>
            <span class="banner-text">Public Storefront</span>
          </div>
          <span class="live-pill">Live Site</span>
        </a>
      </div>

      <!-- Scrollable Navigation Area -->
      <nav class="sidebar-scroll-body">
        <!-- Dashboard -->
        <a class="sidebar-item" routerLink="/admin/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="closeOnMobile()">
          <i class="bi bi-speedometer2"></i>
          <span>Dashboard</span>
        </a>

        <!-- Operations Section -->
        <div class="sidebar-section">
          <div class="sidebar-section-title">Core Operations</div>

          <!-- Customers -->
          <a class="sidebar-item" routerLink="/admin/customers" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-people-fill"></i>
            <span>Customers & KYC</span>
          </a>

          <!-- Inventory & Products -->
          <a class="sidebar-item" routerLink="/admin/inventory" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-box2-heart-fill"></i>
            <span>Jewellery Inventory</span>
          </a>

          <!-- Sales & Billing -->
          <a class="sidebar-item" routerLink="/admin/sales" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="closeOnMobile()">
            <i class="bi bi-receipt-cutoff"></i>
            <span>POS Billing</span>
          </a>

          <!-- Sales Submenu -->
          <div class="sidebar-submenu">
            <a class="sidebar-item sub-item" routerLink="/admin/sales/create" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-plus-circle-fill"></i>
              <span>New Bill / Invoice</span>
            </a>
            <a class="sidebar-item sub-item" routerLink="/admin/sales/list" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-card-list"></i>
              <span>Sales Orders</span>
            </a>
            <a class="sidebar-item sub-item" routerLink="/admin/sales/summary" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-bar-chart-fill"></i>
              <span>Daily Sales Summary</span>
            </a>
          </div>

          <!-- Gold Loans -->
          <a class="sidebar-item" routerLink="/admin/loans" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-cash-coin"></i>
            <span>Gold Loans & Pawns</span>
          </a>
        </div>

        <!-- Reporting Section -->
        <div class="sidebar-section">
          <div class="sidebar-section-title">Intelligence</div>

          <!-- Reports & Analytics -->
          <a class="sidebar-item" routerLink="/admin/reports" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-graph-up-arrow"></i>
            <span>Financial Reports</span>
          </a>
        </div>

        <!-- Configuration Section (Admin & SuperAdmin) -->
        @if (auth.hasAnyRole(['SuperAdmin', 'Admin'])) {
          <div class="sidebar-section">
            <div class="sidebar-section-title">Administration</div>

            <!-- Settings -->
            <a class="sidebar-item" routerLink="/admin/settings" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-gear-wide-connected"></i>
              <span>Store Settings & Rates</span>
            </a>
          </div>
        }
      </nav>

      <!-- Sidebar Footer (Owner Card) -->
      <div class="sidebar-footer">
        <div class="d-flex align-items-center gap-2">
          <div class="footer-avatar">CM</div>
          <div class="footer-info">
            <div class="footer-name">Chandan Kr Mandal</div>
            <div class="footer-role">Owner & SuperAdmin</div>
          </div>
        </div>
      </div>
    </aside>
  `,
  styles: [`
    .admin-sidebar {
      width: 260px;
      min-width: 260px;
      background: linear-gradient(180deg, #121224 0%, #17172f 50%, #131327 100%);
      border-right: 1px solid rgba(201, 168, 76, 0.22);
      display: flex;
      flex-direction: column;
      position: sticky;
      top: 0;
      height: 100vh;
      z-index: 1020;
      box-shadow: 4px 0 24px rgba(0, 0, 0, 0.35);
      padding: 0;
      margin: 0;
      transition: left 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }

    /* ── Brand Header (Flush Top) ── */
    .sidebar-brand-header {
      padding: 1.15rem 1.25rem;
      background: rgba(10, 10, 22, 0.65);
      border-bottom: 1px solid rgba(201, 168, 76, 0.2);
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-height: 64px;
    }

    .brand-link {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      text-decoration: none;
      color: inherit;
    }

    .brand-gem-icon {
      width: 38px;
      height: 38px;
      border-radius: 10px;
      background: linear-gradient(135deg, #c9a84c 0%, #ffd700 100%);
      color: #121224;
      font-size: 1.2rem;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 12px rgba(201, 168, 76, 0.35);
      flex-shrink: 0;
    }

    .brand-name {
      font-family: 'Cormorant Garamond', Georgia, serif;
      font-size: 1.35rem;
      font-weight: 700;
      color: #ffd700;
      line-height: 1.1;
      letter-spacing: -0.01em;
      text-shadow: 0 0 12px rgba(255, 215, 0, 0.3);
    }

    .brand-tagline {
      font-size: 0.68rem;
      color: #94a3b8;
      text-transform: uppercase;
      letter-spacing: 0.1em;
      font-weight: 600;
    }

    .sidebar-close-btn {
      background: transparent;
      border: 1px solid rgba(255, 215, 0, 0.3);
      color: #ffd700;
      font-size: 1rem;
      cursor: pointer;
      width: 32px;
      height: 32px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s ease;

      &:hover {
        background: rgba(255, 215, 0, 0.2);
        color: #ffffff;
      }
    }

    /* ── Storefront Banner Link ── */
    .sidebar-storefront-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.65rem 0.85rem;
      border-radius: 10px;
      background: linear-gradient(135deg, rgba(201, 168, 76, 0.16) 0%, rgba(255, 215, 0, 0.08) 100%);
      border: 1px solid rgba(255, 215, 0, 0.35);
      color: #ffd700;
      text-decoration: none;
      font-size: 0.88rem;
      font-weight: 600;
      transition: all 0.2s ease;

      &:hover {
        background: linear-gradient(135deg, rgba(201, 168, 76, 0.28) 0%, rgba(255, 215, 0, 0.18) 100%);
        border-color: #ffd700;
        color: #ffffff;
        transform: translateY(-1px);
        box-shadow: 0 4px 14px rgba(201, 168, 76, 0.3);
      }
    }

    .live-pill {
      font-size: 0.68rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      padding: 0.2rem 0.5rem;
      border-radius: 999px;
      background: #ffd700;
      color: #121224;
    }

    /* ── Scrollable Body ── */
    .sidebar-scroll-body {
      flex: 1;
      overflow-y: auto;
      padding: 0.75rem 0.75rem 1rem 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;

      &::-webkit-scrollbar {
        width: 4px;
      }
      &::-webkit-scrollbar-thumb {
        background: rgba(201, 168, 76, 0.25);
        border-radius: 4px;
      }
    }

    .sidebar-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.65rem 0.85rem;
      color: #e2e8f0;
      text-decoration: none;
      font-size: 0.92rem;
      font-weight: 500;
      border-radius: 8px;
      transition: all 0.2s ease;

      i {
        font-size: 1.15rem;
        width: 22px;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #f6d87c;
        transition: color 0.2s ease;
      }

      span {
        white-space: nowrap;
      }

      &:hover {
        background: rgba(255, 215, 0, 0.1);
        color: #ffd700;
        transform: translateX(3px);

        i {
          color: #ffd700;
        }
      }

      &.active {
        background: linear-gradient(90deg, rgba(201, 168, 76, 0.25) 0%, rgba(201, 168, 76, 0.08) 100%);
        color: #ffd700;
        font-weight: 700;
        border-left: 3px solid #ffd700;

        i {
          color: #ffd700;
        }
      }

      &.sub-item {
        font-size: 0.85rem;
        padding: 0.5rem 0.85rem;
        color: #cbd5e1;

        i {
          font-size: 0.95rem;
          color: #d4af37;
        }
      }
    }

    .sidebar-section {
      margin-top: 1rem;
      padding-top: 0.75rem;
      border-top: 1px solid rgba(255, 215, 0, 0.12);
    }

    .sidebar-section-title {
      padding: 0 0.85rem 0.4rem 0.85rem;
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: #94a3b8;
    }

    .sidebar-submenu {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      margin-left: 0.75rem;
      padding-left: 0.5rem;
      border-left: 1px dashed rgba(201, 168, 76, 0.25);
      margin-top: 0.25rem;
      margin-bottom: 0.4rem;
    }

    /* ── Footer / Owner Info ── */
    .sidebar-footer {
      padding: 0.85rem 1rem;
      background: rgba(10, 10, 22, 0.7);
      border-top: 1px solid rgba(201, 168, 76, 0.2);
    }

    .footer-avatar {
      width: 34px;
      height: 34px;
      border-radius: 50%;
      background: linear-gradient(135deg, #c9a84c 0%, #b8912d 100%);
      color: #121224;
      font-size: 0.85rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid #ffd700;
      flex-shrink: 0;
    }

    .footer-name {
      font-size: 0.82rem;
      font-weight: 700;
      color: #f8fafc;
      line-height: 1.2;
    }

    .footer-role {
      font-size: 0.68rem;
      color: #ffd700;
      text-transform: uppercase;
      font-weight: 600;
      letter-spacing: 0.05em;
    }

    /* ── Mobile Drawer ── */
    @media (max-width: 992px) {
      .admin-sidebar {
        position: fixed;
        top: 0;
        left: -280px;
        bottom: 0;
        width: 280px;
        height: 100vh;
        z-index: 1040;
        box-shadow: 6px 0 25px rgba(0, 0, 0, 0.6);

        &.show {
          left: 0;
        }
      }
    }

    @media (min-width: 993px) {
      .sidebar-close-btn {
        display: none !important;
      }
    }
  `]
})
export class AdminSidebar {
  protected readonly auth = inject(AuthService);

  isVisible = input<boolean>(false);
  closeSidebar = output<void>();

  closeOnMobile(): void {
    if (window.innerWidth < 993) {
      this.closeSidebar.emit();
    }
  }
}
