import { Component, DestroyRef, inject, signal, computed } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
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
  private readonly sanitizer = inject(DomSanitizer);

  readonly contactData = signal<PublicContactData>({
    shopName: 'Shree Jewellers',
    address: '3B8-2D, Sanhita Township, Newtown, 700135, WB',
    phone: '+91 98516 32391',
    email: 'chandankr.mandal87@gmail.com',
    whatsAppNumber: '9851632391',
    mapEmbedUrl:
      'https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3683.473539876251!2d88.46876127595568!3d22.580126882226842!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x3a02753066345e69%3A0x6b5d9852dc362545!2sNewtown%2C%20Kolkata%2C%20West%20Bengal%20700135!5e0!3m2!1sen!2sin!4v1700000000000!5m2!1sen!2sin',
    businessHours: 'Mon - Sat: 10:30 AM - 8:30 PM | Sun: 11:00 AM - 6:00 PM'
  });

  readonly safeMapUrl = computed<SafeResourceUrl>(() =>
    this.sanitizer.bypassSecurityTrustResourceUrl(this.contactData().mapEmbedUrl)
  );

  readonly whatsAppLink = computed<string>(() => {
    const raw = this.contactData().whatsAppNumber.replace(/\D/g, '');
    const phone = raw.length === 10 ? '91' + raw : raw;
    const text = encodeURIComponent(
      'Hello Shree Jewellers, I would like to inquire regarding jewellery collections / gold loan services.'
    );
    return `https://wa.me/${phone}?text=${text}`;
  });

  isSubmitting = false;

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
    phone: ['', [Validators.required, Validators.pattern(/^[0-9+ -]{10,15}$/)]],
    email: ['', [Validators.email]],
    subject: ['Bridal Consultation', [Validators.required]],
    message: ['', [Validators.required, Validators.minLength(10)]]
  });

  constructor() {
    this.publicData
      .getContact()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (apiData) => {
          if (apiData) {
            this.contactData.set(apiData);
          }
        },
        error: (err) => {
          console.warn('Public contact API failed, using verified fallback data.', err);
        }
      });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const raw = this.form.getRawValue();
    const payload = {
      name: raw.name,
      phone: raw.phone,
      email: raw.email || undefined,
      message: `[Subject: ${raw.subject}] ${raw.message}`
    };

    this.publicData
      .createContactInquiry(payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.toasts.success('Inquiry Received', response.message);
          this.form.reset({
            name: '',
            phone: '',
            email: '',
            subject: 'Bridal Consultation',
            message: ''
          });
          this.isSubmitting = false;
        },
        error: () => {
          this.isSubmitting = false;
        }
      });
  }
}
