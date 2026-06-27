import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../../../core/services/api';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-page.html',
  styleUrl: './register-page.scss'
})
export class RegisterPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toasts = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  step = 1;
  customerId = '';
  isSubmitting = false;

  readonly basicForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, Validators.minLength(10)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  readonly kycForm = this.fb.nonNullable.group({
    dateOfBirth: [''],
    gender: ['Female'],
    aadhaarNumber: ['', Validators.required],
    panNumber: ['', Validators.required],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: ['', Validators.required],
    state: ['', Validators.required],
    pinCode: ['', Validators.required],
    alternatePhone: [''],
    referralCode: ['']
  });

  readonly uploadForm = this.fb.group({
    profilePhoto: [null as File | null],
    idProof: [null as File | null]
  });

  submitStep1(): void {
    if (this.basicForm.invalid || this.basicForm.value.password !== this.basicForm.value.confirmPassword) {
      this.basicForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.api.post<{ userId: string; message: string }>('/api/auth/register/step1', this.basicForm.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.customerId = response.userId;
          this.step = 2;
          this.isSubmitting = false;
          this.toasts.success('Step 1 complete', response.message);
        },
        error: () => { this.isSubmitting = false; }
      });
  }

  submitStep2(): void {
    if (this.kycForm.invalid) {
      this.kycForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.api.post('/api/auth/register/step2', { customerId: this.customerId, ...this.kycForm.getRawValue() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.step = 3;
          this.isSubmitting = false;
          this.toasts.success('Step 2 complete', 'KYC details saved successfully.');
        },
        error: () => { this.isSubmitting = false; }
      });
  }

  onFileChange(event: Event, controlName: 'profilePhoto' | 'idProof'): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.uploadForm.patchValue({ [controlName]: file });
  }

  submitStep3(): void {
    const formData = new FormData();
    formData.append('userId', this.customerId);
    if (this.uploadForm.value.profilePhoto) formData.append('profilePhoto', this.uploadForm.value.profilePhoto);
    if (this.uploadForm.value.idProof) formData.append('idProof', this.uploadForm.value.idProof);

    this.isSubmitting = true;
    this.api.postForm('/api/auth/register/step3', formData)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isSubmitting = false;
          this.toasts.success('Registration complete', 'Your account was created successfully.');
          this.step = 1;
          this.customerId = '';
          this.basicForm.reset();
          this.kycForm.reset();
          this.uploadForm.reset();
        },
        error: () => { this.isSubmitting = false; }
      });
  }
}
