import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicContactData } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';
import { ToastService } from '../../../core/services/toast';

@Component({
  selector: 'app-contact-page',
  imports: [ReactiveFormsModule],
  templateUrl: './contact-page.html',
  styleUrl: './contact-page.scss'
})
export class ContactPage {
  private readonly publicData = inject(PublicDataService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly toasts = inject(ToastService);

  data?: PublicContactData;
  isSubmitting = false;

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    phone: ['', [Validators.required, Validators.minLength(10)]],
    email: ['', [Validators.email]],
    message: ['', [Validators.required, Validators.minLength(10)]]
  });

  constructor() {
    this.publicData.getContact().pipe(takeUntilDestroyed(this.destroyRef)).subscribe((data) => {
      this.data = data;
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.publicData.createContactInquiry(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.toasts.success('Message sent', response.message);
          this.form.reset();
          this.isSubmitting = false;
        },
        error: () => {
          this.isSubmitting = false;
        }
      });
  }
}
