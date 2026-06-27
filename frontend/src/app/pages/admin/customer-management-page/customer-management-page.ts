import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { AdminCreateUserPayload, CustomerDetail, CustomerListItem, UpdateCustomerPayload } from '../../../core/models';
import { AdminService } from '../../../core/services/admin';
import { AuthService } from '../../../core/services/auth';
import { ToastService } from '../../../core/services/toast';

type CustomerPanel = 'details' | 'create';

@Component({
  selector: 'app-customer-management-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './customer-management-page.html',
  styleUrl: './customer-management-page.scss'
})
export class CustomerManagementPage {
  private readonly admin = inject(AdminService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toast = inject(ToastService);

  customers: CustomerListItem[] = [];
  selectedCustomer: CustomerDetail | null = null;
  activePanel: CustomerPanel = 'details';
  currentPage = 1;
  pageSize = 20;
  totalCustomers = 0;

  isLoading = false;
  isLoadingDetail = false;
  isSaving = false;
  isCreating = false;

  readonly filters = this.fb.nonNullable.group({
    search: [''],
    kycStatus: [''],
    isActive: ['']
  });

  readonly detailForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
    alternatePhone: [''],
    dateOfBirth: [''],
    gender: [''],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: ['', Validators.required],
    state: ['', Validators.required],
    pinCode: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    isActive: [true]
  });

  readonly createForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.pattern(/^[6-9]\d{9}$/)]],
    alternatePhone: [''],
    role: ['Customer', Validators.required],
    dateOfBirth: ['', Validators.required],
    gender: ['Male', Validators.required],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: ['', Validators.required],
    state: ['', Validators.required],
    pinCode: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    aadhaarNumber: [''],
    panNumber: ['']
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    const raw = this.filters.getRawValue();
    const params = {
      search: raw.search.trim() || undefined,
      kycStatus: raw.kycStatus || undefined,
      isActive: raw.isActive === '' ? undefined : raw.isActive === 'active',
      page: this.currentPage,
      pageSize: this.pageSize
    };

    this.admin.getCustomers(params).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoading = false)
    ).subscribe({
      next: (response) => {
        this.customers = response.data ?? [];
        this.totalCustomers = response.total ?? 0;
        this.pageSize = response.pageSize ?? this.pageSize;
        this.currentPage = response.page ?? this.currentPage;

        if (this.selectedCustomer && !this.customers.some(c => c.userId === this.selectedCustomer?.userId)) {
          this.selectedCustomer = null;
          this.detailForm.reset();
        }
      },
      error: () => this.toast.error('Customers', 'Failed to load customers.')
    });
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.load();
  }

  selectCustomer(customer: CustomerListItem): void {
    this.activePanel = 'details';
    this.isLoadingDetail = true;
    this.admin.getCustomer(customer.userId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isLoadingDetail = false)
    ).subscribe({
      next: (detail) => {
        this.selectedCustomer = detail;
        this.detailForm.reset({
          firstName: detail.firstName,
          lastName: detail.lastName,
          email: detail.email,
          phoneNumber: detail.phoneNumber,
          alternatePhone: detail.alternatePhone ?? '',
          dateOfBirth: detail.dateOfBirth ?? '',
          gender: detail.gender ?? '',
          addressLine1: detail.addressLine1,
          addressLine2: detail.addressLine2 ?? '',
          city: detail.city,
          state: detail.state,
          pinCode: detail.pinCode,
          isActive: detail.isActive
        });
      },
      error: () => this.toast.error('Customer Details', 'Failed to load customer details.')
    });
  }

  saveCustomer(): void {
    if (!this.selectedCustomer) {
      return;
    }

    if (this.detailForm.invalid) {
      this.detailForm.markAllAsTouched();
      this.toast.error('Validation Error', 'Please fix the highlighted customer fields.');
      return;
    }

    this.isSaving = true;
    const value = this.detailForm.getRawValue();
    const payload: UpdateCustomerPayload = {
      ...value,
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email.trim().toLowerCase(),
      phoneNumber: value.phoneNumber.trim(),
      alternatePhone: value.alternatePhone.trim() || null,
      dateOfBirth: value.dateOfBirth || null,
      gender: value.gender || null,
      addressLine1: value.addressLine1.trim(),
      addressLine2: value.addressLine2.trim() || null,
      city: value.city.trim(),
      state: value.state.trim(),
      pinCode: value.pinCode.trim()
    };

    this.admin.updateCustomer(this.selectedCustomer.userId, payload).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isSaving = false)
    ).subscribe({
      next: (updated) => {
        this.toast.success('Customer Updated', 'Customer profile saved successfully.');
        this.selectedCustomer = updated;
        this.load();
      },
      error: () => this.toast.error('Customer Update', 'Failed to save customer.')
    });
  }

  createCustomer(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toast.error('Validation Error', 'Please fix the highlighted account fields.');
      return;
    }

    this.isCreating = true;
    const value = this.createForm.getRawValue();
    const payload: AdminCreateUserPayload = {
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email.trim().toLowerCase(),
      phoneNumber: value.phoneNumber.trim(),
      alternatePhone: value.alternatePhone.trim() || null,
      role: value.role as 'Customer' | 'Staff' | 'Admin',
      dateOfBirth: value.dateOfBirth,
      gender: value.gender,
      addressLine1: value.addressLine1.trim(),
      addressLine2: value.addressLine2.trim() || null,
      city: value.city.trim(),
      state: value.state.trim(),
      pinCode: value.pinCode.trim(),
      aadhaarNumber: value.aadhaarNumber.trim() || null,
      panNumber: value.panNumber.trim().toUpperCase() || null
    };

    this.admin.createUser(payload).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.isCreating = false)
    ).subscribe({
      next: () => {
        this.toast.success('Account Created', 'Temporary password has been sent to the user by email.');
        this.resetCreateForm();
        this.activePanel = 'details';
        this.load();
      },
      error: () => this.toast.error('Create Account', 'Failed to create user account.')
    });
  }

  verify(customer: CustomerListItem | CustomerDetail): void {
    if (!this.canManageCustomers || customer.kycStatus === 'Verified') {
      return;
    }

    if (!confirm(`Verify KYC for ${customer.fullName}?`)) {
      return;
    }

    this.admin.verifyKyc({ customerId: customer.userId, notes: 'Verified from admin customer management.' })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success('KYC Verified', 'Customer KYC marked as verified.');
          this.load();
          if (this.selectedCustomer?.userId === customer.userId) {
            this.selectCustomer(customer as CustomerListItem);
          }
        },
        error: () => this.toast.error('KYC Verification', 'Failed to verify KYC.')
      });
  }

  reject(customer: CustomerListItem | CustomerDetail): void {
    if (!this.canManageCustomers || customer.kycStatus === 'Rejected') {
      return;
    }

    const reason = prompt('Rejection reason', 'Details need review');
    if (!reason?.trim()) {
      return;
    }

    this.admin.rejectKyc({ customerId: customer.userId, rejectionReason: reason.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.toast.success('KYC Rejected', 'Customer KYC marked as rejected.');
          this.load();
          if (this.selectedCustomer?.userId === customer.userId) {
            this.selectCustomer(customer as CustomerListItem);
          }
        },
        error: () => this.toast.error('KYC Rejection', 'Failed to reject KYC.')
      });
  }

  toggleActive(customer: CustomerListItem | CustomerDetail): void {
    if (!this.canManageCustomers) {
      return;
    }

    const action = customer.isActive ? this.admin.deactivateCustomer(customer.userId) : this.admin.activateCustomer(customer.userId);
    action.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toast.success('Customer Status', customer.isActive ? 'Customer deactivated.' : 'Customer activated.');
        this.load();
        if (this.selectedCustomer?.userId === customer.userId) {
          this.selectCustomer(customer as CustomerListItem);
        }
      },
      error: () => this.toast.error('Customer Status', 'Failed to update customer status.')
    });
  }

  openCreatePanel(): void {
    this.activePanel = 'create';
    this.selectedCustomer = null;
  }

  resetCreateForm(): void {
    this.createForm.reset({
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      alternatePhone: '',
      role: 'Customer',
      dateOfBirth: '',
      gender: 'Male',
      addressLine1: '',
      addressLine2: '',
      city: '',
      state: '',
      pinCode: '',
      aadhaarNumber: '',
      panNumber: ''
    });
  }

  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }
    this.currentPage = page;
    this.load();
  }

  isInvalid(form: FormGroup, controlName: string): boolean {
    const control = form.get(controlName);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  kycClass(status: string): string {
    const normalized = status.toLowerCase();
    if (normalized === 'verified') {
      return 'status-verified';
    }
    if (normalized === 'rejected') {
      return 'status-rejected';
    }
    return 'status-pending';
  }

  get canManageCustomers(): boolean {
    return this.auth.hasAnyRole(['SuperAdmin', 'Admin']);
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCustomers / this.pageSize));
  }

  get pages(): number[] {
    const result = [];
    for (let page = 1; page <= this.totalPages; page++) {
      result.push(page);
    }
    return result;
  }

  get pendingKycOnPage(): number {
    return this.customers.filter(c => c.kycStatus === 'Pending').length;
  }

  get activeOnPage(): number {
    return this.customers.filter(c => c.isActive).length;
  }
}
