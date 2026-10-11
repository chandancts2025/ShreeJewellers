import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PublicHomeData } from '../../../core/models';
import { PublicDataService } from '../../../core/services/public-data';

interface ShowcaseItem {
  name: string;
  category: string;
  purity: string;
  weight: string;
  description: string;
  imageUrl: string;
  badge: string;
}

@Component({
  selector: 'app-home-page',
  imports: [RouterLink],
  templateUrl: './home-page.html',
  styleUrl: './home-page.scss'
})
export class HomePage {
  private readonly publicData = inject(PublicDataService);
  private readonly destroyRef = inject(DestroyRef);

  readonly homeData = signal<PublicHomeData>({
    shopName: 'Shree Jewellers',
    trustedSinceYear: 1998,
    heroHeadline: 'Trusted Since 1998 — Fine Gold, Diamonds & Instant Loans',
    heroSubheadline:
      'Artisanal 22K/24K hallmark purity, certified solitaires, transparent digital billing, and confidential gold loans under one revered roof.',
    aboutSummary:
      'Led by Chandan Kr Mandal, Shree Jewellers delivers certified 22K/24K hallmarked jewellery, bridal heirlooms, fine diamond solitaires, and instant transparent gold loans with unmatched family trust.',
    highlights: [
      {
        title: '100% BIS 916 Hallmark',
        description: 'Every ornament carries laser-etched HUID certification verifiable on the BIS Care portal.',
        icon: 'bi-patch-check'
      },
      {
        title: 'Instant Secure Gold Loans',
        description: 'Disbursement within 20 minutes against gold valuation with bank-grade vault custody.',
        icon: 'bi-shield-lock'
      },
      {
        title: 'Transparent Itemized Billing',
        description: 'Computerized GST invoices with clearly separated metal rates, making charges, and net weights.',
        icon: 'bi-receipt'
      },
      {
        title: 'IGI & GIA Certified Solitaires',
        description: 'Handpicked natural diamonds set in hallmarked 18K gold and 950 platinum with lifetime buyback.',
        icon: 'bi-gem'
      }
    ],
    featuredCategories: [
      {
        categoryId: 1,
        name: 'Gold Jewellery',
        metalType: 'Gold',
        productCount: 18,
        description: 'Handcrafted 22K bridal necklaces, bangles, mangalsutras, and Italian chains.'
      },
      {
        categoryId: 3,
        name: 'Diamond Solitaires',
        metalType: 'Diamond',
        productCount: 12,
        description: 'IGI/GIA certified natural diamonds set in 18K white and rose gold.'
      },
      {
        categoryId: 4,
        name: 'Platinum 950',
        metalType: 'Platinum',
        productCount: 8,
        description: 'Pure 950 platinum wedding bands and couple rings for everlasting commitment.'
      },
      {
        categoryId: 2,
        name: 'Silver Articles & Coins',
        metalType: 'Silver',
        productCount: 15,
        description: '999 pure silver coins and handcrafted 925 sterling silver pooja sets.'
      }
    ],
    testimonials: [
      {
        customerName: 'Sunita & Rajesh Aggarwal',
        quote:
          'We purchased our daughter’s complete bridal wedding set from Shree Jewellers. The purity transparency, 6-digit HUID hallmark explanation, and warm personal care made our wedding shopping stress-free.',
        location: 'Newtown, Kolkata'
      },
      {
        customerName: 'Ramesh Kothari',
        quote:
          'Needed urgent working capital for my wholesale business. The gold loan verification took under 20 minutes, interest rates are crystal clear, and ornaments are in bank-grade safe custody.',
        location: 'Salt Lake, Kolkata'
      },
      {
        customerName: 'Dr. Ananya Sen',
        quote:
          'The 18K diamond solitaire ring and Italian Figaro chain are breathtaking. Complete GST bill, hallmarking certificate, and prompt digital billing. Unmatched trust!',
        location: 'Rajarhat, Kolkata'
      }
    ],
    quickLinks: [
      { title: 'Explore Gold Loans', route: '/gold-loan' },
      { title: 'Our Heritage Story', route: '/about' },
      { title: 'Showroom & Contact', route: '/contact' },
      { title: 'Customer Portal', route: '/login' }
    ]
  });

  readonly showcaseItems: ShowcaseItem[] = [
    {
      name: '22K Gold Temple Choker Necklace',
      category: 'Bridal Collection',
      purity: '22K BIS 916',
      weight: '35.50 g',
      description: 'Intricately crafted antique finish temple jewellery choker with auspicious Lakshmi motif and pearls.',
      imageUrl: 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=800&auto=format&fit=crop&q=80',
      badge: 'Heritage Best Seller'
    },
    {
      name: '18K Solitaire Diamond Ring (0.60 ct)',
      category: 'Diamond Atelier',
      purity: '18K Gold · VVS-EF',
      weight: '4.80 g',
      description: 'IGI certified brilliant-cut solitaire diamond set in an elegant hallmarked two-tone band.',
      imageUrl: 'https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=800&auto=format&fit=crop&q=80',
      badge: 'Certified Solitaire'
    },
    {
      name: '22K Royal Bridal Kada Bangles',
      category: 'Wedding Heirlooms',
      purity: '22K BIS 916',
      weight: '48.20 g',
      description: 'Handcrafted floral filigree heavy bridal kadas with secure screw lock mechanism (Pair).',
      imageUrl: 'https://images.unsplash.com/photo-1611591475152-4c09ac33c497?w=800&auto=format&fit=crop&q=80',
      badge: 'Handcrafted Pair'
    },
    {
      name: '18K Diamond Tennis Bracelet (3.5 ct)',
      category: 'Diamond Atelier',
      purity: '18K White Gold',
      weight: '12.40 g',
      description: 'Continuous strand of prong-set sparkling brilliant round diamonds with safety clasp.',
      imageUrl: 'https://images.unsplash.com/photo-1599643477877-530eb83abc8e?w=800&auto=format&fit=crop&q=80',
      badge: 'Luxury Diamond'
    },
    {
      name: 'Platinum 950 Couple Love Bands',
      category: 'Platinum Love',
      purity: 'Pt 950 Pure',
      weight: '11.80 g',
      description: 'Matched pair of high-polish platinum bands featuring star-set natural diamond accents.',
      imageUrl: 'https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=800&auto=format&fit=crop&q=80',
      badge: 'Couple Set'
    },
    {
      name: '925 Silver Royal Pooja Thali Set',
      category: 'Silver Artistry',
      purity: '925 Sterling',
      weight: '320.00 g',
      description: 'Auspicious embossed prayer thali set complete with silver bell, diya, and sacred kalash.',
      imageUrl: 'https://images.unsplash.com/photo-1534447677768-be436bb09401?w=800&auto=format&fit=crop&q=80',
      badge: '925 Sterling'
    }
  ];

  constructor() {
    this.publicData
      .getHome()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (apiData) => {
          if (apiData) {
            this.homeData.set(apiData);
          }
        },
        error: (err) => {
          console.warn('Public home API failed, using verified fallback data.', err);
        }
      });
  }
}
