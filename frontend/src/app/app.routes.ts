import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth-guard';
import { roleGuard } from './core/guards/role-guard';
import { AboutPage } from './pages/public/about-page/about-page';
import { ContactPage } from './pages/public/contact-page/contact-page';
import { GoldLoanPage } from './pages/public/gold-loan-page/gold-loan-page';
import { HomePage } from './pages/public/home-page/home-page';
import { LoginPage } from './pages/auth/login-page/login-page';
import { RegisterPage } from './pages/auth/register-page/register-page';
import { AppShell } from './shared/layout/app-shell/app-shell';
import { AdminLayout } from './shared/layout/admin-layout/admin-layout';
import { CustomerDashboardPage } from './pages/customer/customer-dashboard-page/customer-dashboard-page';
import { MyLoansPage } from './pages/customer/my-loans-page/my-loans-page';
import { LoanDetailPage } from './pages/customer/loan-detail-page/loan-detail-page';
import { MyOrdersPage } from './pages/customer/my-orders-page/my-orders-page';
import { MyProfilePage } from './pages/customer/my-profile-page/my-profile-page';
import { AdminDashboardPage } from './pages/admin/admin-dashboard-page/admin-dashboard-page';
import { CustomerManagementPage } from './pages/admin/customer-management-page/customer-management-page';
import { InventoryPage } from './pages/admin/inventory-page/inventory-page';
import { SalesBillingPage } from './pages/admin/sales-billing-page/sales-billing-page';
import { CreateSalesOrderPage } from './pages/admin/create-sales-order-page/create-sales-order-page';
import { SalesOrdersListPage } from './pages/admin/sales-orders-list-page/sales-orders-list-page';
import { OrderDetailsPage } from './pages/admin/order-details-page/order-details-page';
import { ProcessReturnPage } from './pages/admin/process-return-page/process-return-page';
import { DailySalesSummaryPage } from './pages/admin/daily-sales-summary-page/daily-sales-summary-page';
import { GoldLoanManagementPage } from './pages/admin/gold-loan-management-page/gold-loan-management-page';
import { ReportsPage } from './pages/admin/reports-page/reports-page';
import { SettingsPage } from './pages/admin/settings-page/settings-page';

export const routes: Routes = [
  {
    path: '',
    component: AppShell,
    children: [
      { path: '', pathMatch: 'full', component: HomePage },
      { path: 'about', component: AboutPage },
      { path: 'contact', component: ContactPage },
      { path: 'gold-loan', component: GoldLoanPage },
      { path: 'login', component: LoginPage },
      { path: 'register', component: RegisterPage },
      {
        path: 'customer/dashboard',
        component: CustomerDashboardPage,
        canActivate: [authGuard, roleGuard],
        data: { roles: ['Customer'] }
      },
      {
        path: 'customer/loans',
        component: MyLoansPage,
        canActivate: [authGuard, roleGuard],
        data: { roles: ['Customer'] }
      },
      {
        path: 'customer/loans/:id',
        component: LoanDetailPage,
        canActivate: [authGuard, roleGuard],
        data: { roles: ['Customer', 'Staff', 'Admin', 'SuperAdmin'] }
      },
      {
        path: 'customer/orders',
        component: MyOrdersPage,
        canActivate: [authGuard, roleGuard],
        data: { roles: ['Customer'] }
      },
      {
        path: 'customer/profile',
        component: MyProfilePage,
        canActivate: [authGuard]
      }
    ]
  },
  {
    path: 'admin',
    component: AdminLayout,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Staff', 'Admin', 'SuperAdmin'] },
    children: [
      {
        path: 'dashboard',
        component: AdminDashboardPage
      },
      {
        path: 'customers',
        component: CustomerManagementPage
      },
      {
        path: 'inventory',
        component: InventoryPage
      },
      {
        path: 'sales',
        component: SalesBillingPage
      },
      {
        path: 'sales/create',
        component: CreateSalesOrderPage
      },
      {
        path: 'sales/list',
        component: SalesOrdersListPage
      },
      {
        path: 'sales/view/:id',
        component: OrderDetailsPage
      },
      {
        path: 'sales/edit/:id',
        component: CreateSalesOrderPage
      },
      {
        path: 'sales/return/:id',
        component: ProcessReturnPage
      },
      {
        path: 'sales/summary',
        component: DailySalesSummaryPage
      },
      {
        path: 'loans',
        component: GoldLoanManagementPage
      },
      {
        path: 'reports',
        component: ReportsPage,
        data: { roles: ['Admin', 'SuperAdmin'] }
      },
      {
        path: 'settings',
        component: SettingsPage,
        data: { roles: ['SuperAdmin'] }
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
