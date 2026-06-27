import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/services/auth';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss'
})
export class LoginPage {
  private readonly fb = inject(FormBuilder);
  protected readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  readonly showPassword = signal(false);
  isSubmitting = false;

  readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
    rememberMe: [true]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.auth.login(this.form.getRawValue()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.isSubmitting = false; },
      error: () => { this.isSubmitting = false; }
    });
  }
}
