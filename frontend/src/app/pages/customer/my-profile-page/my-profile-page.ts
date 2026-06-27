import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/services/auth';
import { CustomerPortalService } from '../../../core/services/customer-portal';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-my-profile-page',
  imports: [ReactiveFormsModule],
  templateUrl: './my-profile-page.html',
  styleUrl: './my-profile-page.scss'
})
export class MyProfilePage {
  private readonly portal = inject(CustomerPortalService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toasts = inject(ToastService);
  profile: any;

  readonly profileForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phoneNumber: ['', Validators.required],
    alternatePhone: [''],
    dateOfBirth: [''],
    gender: [''],
    addressLine1: ['', Validators.required],
    addressLine2: [''],
    city: ['', Validators.required],
    state: ['', Validators.required],
    pinCode: ['', Validators.required]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  constructor() {
    this.loadProfile();
  }

  loadProfile(): void {
    this.portal.getProfile().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((profile) => {
      this.profile = profile;
      this.profileForm.patchValue({
        firstName: profile.firstName,
        lastName: profile.lastName,
        phoneNumber: profile.phoneNumber,
        alternatePhone: profile.alternatePhone ?? '',
        dateOfBirth: profile.dateOfBirth ?? '',
        gender: profile.gender ?? '',
        addressLine1: profile.addressLine1,
        addressLine2: profile.addressLine2 ?? '',
        city: profile.city,
        state: profile.state,
        pinCode: profile.pinCode
      });
    });
  }

  saveProfile(): void {
    this.portal.updateProfile(this.profileForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.toasts.success('Profile updated', 'Your profile details were saved.');
      this.loadProfile();
    });
  }

  changePassword(): void {
    this.auth.changePassword(this.passwordForm.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((response) => {
      this.toasts.success('Password updated', response.message);
      this.passwordForm.reset();
    });
  }

  uploadDocument(event: Event, controlName: 'profilePhoto' | 'idProof'): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append(controlName, file);
    this.portal.uploadDocuments(formData).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.toasts.success('Document uploaded', 'Your file was uploaded successfully.');
      this.loadProfile();
    });
  }
}
