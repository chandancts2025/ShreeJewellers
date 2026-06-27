import { Component, signal } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AdminSidebar } from '../../components/admin-sidebar/admin-sidebar';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, AdminSidebar],
  template: `
    <div class="admin-wrapper d-flex">
      <!-- Mobile Sidebar Toggle Button -->
      <button
        class="sidebar-toggle-btn d-lg-none"
        type="button"
        (click)="toggleSidebar()"
        [class.active]="sidebarVisible()">
        <i class="bi bi-list"></i>
      </button>

      <!-- Mobile Overlay -->
      @if (sidebarVisible()) {
        <div class="sidebar-overlay d-lg-none" (click)="closeSidebar()"></div>
      }

      <app-admin-sidebar
        [isVisible]="sidebarVisible()"
        (closeSidebar)="closeSidebar()">
      </app-admin-sidebar>

      <main class="admin-main flex-grow-1">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .admin-wrapper {
      min-height: calc(100vh - 100px);
      position: relative;
    }

    .admin-main {
      overflow-y: auto;
    }

    .sidebar-toggle-btn {
      position: fixed;
      top: 120px;
      left: 15px;
      z-index: 1020;
      background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
      border: 2px solid rgba(255, 215, 0, 0.3);
      color: #ffd700;
      width: 50px;
      height: 50px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.2rem;
      cursor: pointer;
      transition: all 0.3s ease;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);

      &:hover {
        background: linear-gradient(135deg, #16213e 0%, #1a1a2e 100%);
        border-color: #ffd700;
        transform: scale(1.05);
      }

      &.active {
        background: linear-gradient(135deg, #ffd700 0%, #ffb347 100%);
        color: #1a1a2e;
        border-color: #ffd700;
      }

      i {
        font-size: 1.4rem;
      }
    }

    .sidebar-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.5);
      z-index: 1010;
      backdrop-filter: blur(2px);
    }

    @media (max-width: 992px) {
      .admin-wrapper {
        flex-direction: column;
      }
    }

    @media (min-width: 993px) {
      .sidebar-toggle-btn {
        display: none !important;
      }

      .sidebar-overlay {
        display: none !important;
      }
    }
  `]
})
export class AdminLayout {
  sidebarVisible = signal(false);

  toggleSidebar(): void {
    this.sidebarVisible.set(!this.sidebarVisible());
  }

  closeSidebar(): void {
    this.sidebarVisible.set(false);
  }
}
