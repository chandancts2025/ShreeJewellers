# Quick Navigation Reference

## 🏠 Public Navigation (No Login Required)

```
Home (/)
├── About (/about)
├── Contact (/contact)
├── Gold Loan (/gold-loan) — Public info about loans
├── Login (/login)
└── Register (/register)
```

---

## 👤 Customer Dashboard (`/customer/dashboard`)

After login as Customer, access via top-nav "Account" dropdown:

```
Account Dropdown
├── Dashboard (/customer/dashboard)
├── Profile (/customer/profile)
├── My Loans (/customer/loans)
│   └── Loan Details (/customer/loans/:id)
└── My Orders (/customer/orders)
```

**Accessible from**: Account dropdown menu or sidebar (when logged in)

---

## 👨‍💼 Admin Dashboard (`/admin/dashboard`)

After login as Staff/Admin/SuperAdmin:

### Via Top-Nav "Admin" Dropdown:
```
Admin Dropdown
└── Dashboard (/admin/dashboard)
```

### Via Admin Sidebar (All pages):
```
Admin Sidebar Navigation
├── Dashboard (/admin/dashboard)
├── ─────────────────────
├── OPERATIONS
│   ├── Customers (/admin/customers)
│   ├── Inventory & Products (/admin/inventory)
│   ├── Sales & Billing (/admin/sales)
│   └── Gold Loans (/admin/loans)
├── ─────────────────────
├── REPORTING
│   └── Reports & Analytics (/admin/reports)
├── ─────────────────────
├── CONFIGURATION (SuperAdmin Only)
│   └── Settings (/admin/settings)
```

---

## 📊 Admin Module Access

### 1. **Customers** (`/admin/customers`) — Staff/Admin/SuperAdmin
   - View all customers
   - Manage customer details
   - Activate/Deactivate accounts
   - Verify KYC documents

### 2. **Inventory & Products** (`/admin/inventory`) — Staff/Admin/SuperAdmin
   - Manage products catalog
   - Track stock levels
   - Record stock movements
   - Adjust inventory
   - View stock valuation

### 3. **Sales & Billing** (`/admin/sales`) — Staff/Admin/SuperAdmin
   - Create sales invoices
   - Process returns
   - View sales history
   - Send invoices via email/SMS
   - Daily sales summary

### 4. **Gold Loans** (`/admin/loans`) — Staff/Admin/SuperAdmin
   - Create new loans
   - Check eligibility
   - Record repayments
   - Extend loans
   - Process loan closures
   - Auction pledged gold

### 5. **Reports & Analytics** (`/admin/reports`) — Admin/SuperAdmin
   - 18 different report types
   - Sales, Inventory, Loans, Financial reports
   - Customizable date ranges
   - Export capabilities

### 6. **Settings** (`/admin/settings`) — SuperAdmin Only
   - Shop configuration
   - Interest rates management
   - User management
   - Price API settings
   - Business hours & contact info

---

## 🔐 Role-Based Access Control

| Feature | Customer | Staff | Admin | SuperAdmin |
|---------|----------|-------|-------|-----------|
| View own loans | ✅ | — | ✅ | ✅ |
| View own orders | ✅ | — | ✅ | ✅ |
| Customers page | — | ✅ | ✅ | ✅ |
| Inventory page | — | ✅ | ✅ | ✅ |
| Sales/Billing | — | ✅ | ✅ | ✅ |
| Gold Loans | — | ✅ | ✅ | ✅ |
| Reports | — | — | ✅ | ✅ |
| Settings | — | — | — | ✅ |

---

## 🎯 Direct URLs for Quick Access

```
CUSTOMER SECTION:
- Dashboard:  http://localhost:4200/customer/dashboard
- My Loans:   http://localhost:4200/customer/loans
- My Orders:  http://localhost:4200/customer/orders
- Profile:    http://localhost:4200/customer/profile

ADMIN SECTION:
- Dashboard:  http://localhost:4200/admin/dashboard
- Customers:  http://localhost:4200/admin/customers
- Inventory:  http://localhost:4200/admin/inventory
- Sales:      http://localhost:4200/admin/sales
- Loans:      http://localhost:4200/admin/loans
- Reports:    http://localhost:4200/admin/reports
- Settings:   http://localhost:4200/admin/settings

PUBLIC:
- Home:       http://localhost:4200/
- About:      http://localhost:4200/about
- Contact:    http://localhost:4200/contact
- Gold Loan:  http://localhost:4200/gold-loan
```

---

## 🛠️ Navigation Components

### Top Navigation Bar
- Located in: `frontend/src/app/shared/components/top-nav/`
- Updated with organized dropdowns for Admin and Account
- Shows different menus based on user role
- Icons included for visual clarity

### Admin Sidebar
- Located in: `frontend/src/app/shared/components/admin-sidebar/`
- Available on all admin pages
- Only visible when accessing /admin/* routes
- Sticky positioning for easy access while scrolling
- Responsive (collapses on mobile)

---

## 📱 Mobile Navigation

On mobile devices (< 992px):
- Top navigation toggles with hamburger menu
- Admin sidebar transforms to mobile-friendly drawer
- All functionality remains the same
- Touch-friendly button sizes

---

## 🚀 How to Navigate

### For End Users:
1. **Customer**: Login → Click "Account" dropdown → Access loans/orders
2. **Admin**: Login → Click "Admin" dropdown → Access all admin modules

### For Developers:
1. Check role-based access in `authGuard` and `roleGuard`
2. Admin routes use `AdminLayout` for consistent sidebar
3. All endpoints covered by services in `frontend/src/app/core/services/`
4. Update navigation by modifying `top-nav.html` or `admin-sidebar.ts`

---

## 📝 API Endpoints Covered

All endpoints from these controllers are now accessible in UI:
- ✅ ProductController (`/api/products`)
- ✅ InventoryController (`/api/inventory`)
- ✅ SalesOrderController (`/api/sales`)
- ✅ GoldLoanController (`/api/goldloan`)
- ✅ SettingsController (`/api/settings`)
- ✅ PublicController (`/api/public`)
- ✅ CustomersController (`/api/customers`)
- ✅ ReportsDashboardController (`/api/reports`)

---

## ✅ Verification Checklist

- [x] All endpoints accessible via UI
- [x] Role-based access control working
- [x] Navigation menus organized by function
- [x] Admin sidebar functional on all admin pages
- [x] Mobile responsive design
- [x] Authentication guards protecting routes
- [x] Production build successful
- [x] All 18 report types accessible
- [x] Public pages accessible without login
