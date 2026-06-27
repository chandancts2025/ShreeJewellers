import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../../core/services/auth';

interface SidebarItem {
  label: string;
  icon: string;
  route?: string;
  badge?: number;
  children?: SidebarItem[];
  roles?: string[];
}

@Component({
  selector: 'app-admin-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="admin-sidebar" [class.show]="isVisible()">
      <nav class="sidebar-nav">
        <!-- Close button for mobile -->
        <button
          class="sidebar-close-btn d-lg-none"
          type="button"
          (click)="closeSidebar.emit()"
          aria-label="Close sidebar">
          <i class="bi bi-x-lg"></i>
        </button>

        <!-- Dashboard -->
        <a class="sidebar-item" routerLink="/admin/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="closeOnMobile()">
          <i class="bi bi-speedometer2"></i>
          <span>Dashboard</span>
        </a>

        <!-- Operations Section -->
        <div class="sidebar-section">
          <div class="sidebar-section-title">Operations</div>

          <!-- Customers -->
          <a class="sidebar-item" routerLink="/admin/customers" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-people-fill"></i>
            <span>Customers</span>
          </a>

          <!-- Inventory & Products -->
          <a class="sidebar-item" routerLink="/admin/inventory" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-box2-heart"></i>
            <span>Inventory & Products</span>
          </a>

          <!-- Sales & Billing -->
          <a class="sidebar-item" routerLink="/admin/sales" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" (click)="closeOnMobile()">
            <i class="bi bi-receipt-cutoff"></i>
            <span>Sales & Billing</span>
          </a>

          <!-- Sales Submenu -->
          <div class="sidebar-submenu" style="padding-left: 1.5rem;">
            <a class="sidebar-item small" routerLink="/admin/sales/create" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-plus-circle"></i>
              <span>New Order</span>
            </a>
            <a class="sidebar-item small" routerLink="/admin/sales/list" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-list-ul"></i>
              <span>View Orders</span>
            </a>
            <a class="sidebar-item small" routerLink="/admin/sales/summary" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-graph-up"></i>
              <span>Daily Summary</span>
            </a>
          </div>

          <!-- Gold Loans -->
          <a class="sidebar-item" routerLink="/admin/loans" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-cash-coin"></i>
            <span>Gold Loans</span>
          </a>
        </div>

        <!-- Reporting Section -->
        <div class="sidebar-section">
          <div class="sidebar-section-title">Reporting</div>

          <!-- Reports & Analytics -->
          <a class="sidebar-item" routerLink="/admin/reports" routerLinkActive="active" (click)="closeOnMobile()">
            <i class="bi bi-graph-up"></i>
            <span>Reports & Analytics</span>
          </a>
        </div>

        <!-- Configuration Section (SuperAdmin Only) -->
        @if (auth.hasAnyRole(['SuperAdmin'])) {
          <div class="sidebar-section">
            <div class="sidebar-section-title">Configuration</div>

            <!-- Settings -->
            <a class="sidebar-item" routerLink="/admin/settings" routerLinkActive="active" (click)="closeOnMobile()">
              <i class="bi bi-gear-fill"></i>
              <span>Settings</span>
            </a>
          </div>
        }
      </nav>
    </aside>
  `,
  styles: [`
    .admin-sidebar {
      width: 250px;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
      border-right: 1px solid rgba(255, 215, 0, 0.1);
      padding: 1rem 0;
      position: sticky;
      top: 100px;
      height: calc(100vh - 100px);
      overflow-y: auto;
      transition: left 0.3s ease;
    }

    .sidebar-close-btn {
      position: absolute;
      top: 15px;
      right: 15px;
      background: none;
      border: none;
      color: rgba(255, 215, 0, 0.7);
      font-size: 1.2rem;
      cursor: pointer;
      padding: 0.5rem;
      border-radius: 50%;
      transition: all 0.2s ease;
      z-index: 1020;

      &:hover {
        background: rgba(255, 215, 0, 0.1);
        color: #ffd700;
      }

      i {
        font-size: 1.1rem;
      }
    }

    .sidebar-nav {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding-top: 2rem; /* Space for close button */
    }

    .sidebar-item {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 0.75rem 1rem;
      color: rgba(255, 215, 0, 0.7);
      text-decoration: none;
      transition: all 0.2s ease;
      border-left: 3px solid transparent;

      i {
        font-size: 1.1rem;
        width: 24px;
        display: flex;
        align-items: center;
        justify-content: center;
      }

      span {
        font-size: 0.95rem;
      }

      &:hover {
        background: rgba(255, 215, 0, 0.05);
        color: #ffd700;
        padding-left: 1rem;
      }

      &.active {
        background: rgba(255, 215, 0, 0.1);
        color: #ffd700;
        border-left-color: #ffd700;
      }
    }

    .sidebar-section {
      margin-top: 1.5rem;
      padding-top: 1rem;
      border-top: 1px solid rgba(255, 215, 0, 0.1);
    }

    .sidebar-section-title {
      padding: 0 1rem;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: rgba(255, 215, 0, 0.5);
      margin-bottom: 0.5rem;
    }

    .sidebar-submenu {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      margin-top: 0.25rem;

      .sidebar-item {
        font-size: 0.85rem;
        padding: 0.5rem 1rem;
        color: rgba(255, 215, 0, 0.5);

        i {
          font-size: 0.9rem;
        }

        &:hover {
          background: rgba(255, 215, 0, 0.05);
          color: rgba(255, 215, 0, 0.8);
        }

        &.active {
          background: rgba(255, 215, 0, 0.15);
          color: #ffd700;
          border-left-color: #ffd700;
        }
      }
    }

    /* Mobile Styles */
    @media (max-width: 992px) {
      .admin-sidebar {
        position: fixed;
        left: -250px;
        top: 100px;
        bottom: 0;
        z-index: 1020;
        box-shadow: 4px 0 12px rgba(0, 0, 0, 0.3);

        &.show {
          left: 0;
        }
      }

      .sidebar-nav {
        padding-top: 3rem; /* More space for close button on mobile */
      }
    }

    /* Desktop Styles */
    @media (min-width: 993px) {
      .sidebar-close-btn {
        display: none !important;
      }

      .sidebar-nav {
        padding-top: 1rem; /* Normal padding on desktop */
      }
    }
  `]
})
export class AdminSidebar {
  protected readonly auth = inject(AuthService);

  // Input to control visibility
  isVisible = input<boolean>(false);

  // Output to notify parent when sidebar should close
  closeSidebar = output<void>();

  closeOnMobile(): void {
    // Only close on mobile devices
    if (window.innerWidth < 993) {
      this.closeSidebar.emit();
    }
  }
}
