import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicAboutData } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';

interface TimelineMilestone {
  year: string;
  title: string;
  description: string;
}

interface HeritagePillar {
  icon: string;
  title: string;
  description: string;
}

@Component({
  selector: 'app-about-page',
  imports: [RouterLink],
  templateUrl: './about-page.html',
  styleUrl: './about-page.scss'
})
export class AboutPage {
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);

  readonly aboutData = signal<PublicAboutData>({
    shopName: 'Shree Jewellers',
    trustedSinceYear: 1998,
    story:
      'Founded in 1998 by Chandan Kr Mandal, Shree Jewellers was established on an unshakeable promise: absolute purity, honest weight, and generational customer relationships.\n\nFrom our headquarters at 3B8-2D, Sanhita Township, Newtown, 700135, WB, we have adorned brides across generations, crafted bespoke bridal necklaces, and preserved the heritage of handcrafted Indian temple jewellery alongside contemporary lightweight diamond fashion. Every piece of gold in our showroom is 100% BIS Hallmarked with a 6-digit HUID code, certified for purity, and backed by a transparent lifetime exchange guarantee.\n\nBeyond fine jewellery, our registered Gold Loan service provides secure, compassionate liquidity with bank-grade vault custody and fast disbursement.',
    ownerName: 'Chandan Kr Mandal',
    ownerTitle: 'Founder & Managing Director',
    ownerPhotoUrl: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&auto=format&fit=crop&q=80',
    certifications: [
      'BIS 916 & 999 Hallmark Licensed',
      'GST Registered & Audited Tax Compliance',
      'Certified Diamond Assaying & Appraisals',
      'Licensed & Regulated Gold Loan Provider',
      'Lifetime Polish, Cleaning & Exchange Promise',
      'Insured Safe Vault Storage & CCTV Surveillance'
    ],
    hallmarkNote:
      'Every ornament is certified before display, accompanied by tamper-proof documentation and an authentic GST invoice.'
  });

  readonly milestones: TimelineMilestone[] = [
    {
      year: '1998',
      title: 'Humble Beginnings',
      description: 'Chandan Kr Mandal established an artisanal jewellery house dedicated to 100% hallmark purity.'
    },
    {
      year: '2005',
      title: 'BIS Hallmark Pioneer',
      description: 'Among the earliest jewelers in the region to adopt the 100% BIS Hallmarking standard.'
    },
    {
      year: '2014',
      title: 'Gold Loan Facility',
      description: 'Launched regulated gold lending operations to support local entrepreneurs with instant liquidity.'
    },
    {
      year: '2026',
      title: 'Digital Showroom Era',
      description: 'Digital portal, itemized computerized GST billing, and live gold rate tracking.'
    }
  ];

  readonly pillars: HeritagePillar[] = [
    {
      icon: 'bi-patch-check',
      title: 'Uncompromised Purity',
      description: 'Every gold piece is certified 22K (916) or 24K (999) with laser-inscribed 6-digit HUID.'
    },
    {
      icon: 'bi-gem',
      title: 'Ethical Natural Diamonds',
      description: 'Only conflict-free, natural diamonds tested for cut, color, clarity, and carat weight.'
    },
    {
      icon: 'bi-receipt-cutoff',
      title: 'Transparent Pricing',
      description: 'Exact digital breakdown of live gold rate, net weight, stone value, making charge, and 3% GST.'
    },
    {
      icon: 'bi-shield-lock',
      title: 'Insured Vault Security',
      description: 'Gold loan ornaments stored in multi-lock bank-grade security vaults under 24/7 CCTV vigilance.'
    }
  ];

  constructor() {
    this.publicData
      .getAbout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (apiData) => {
          if (apiData) {
            this.aboutData.set(apiData);
          }
        },
        error: (err) => {
          console.warn('Public about API failed, using verified fallback data.', err);
        }
      });
  }
}
