import { Component, EventEmitter, Input, Output, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { SalesOrderResponse } from '../../../core/models';
import { PrintFormat, PrintService } from '../../../core/services/print';

@Component({
  selector: 'app-receipt-preview-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './receipt-preview-modal.html',
  styleUrls: ['./receipt-preview-modal.scss']
})
export class ReceiptPreviewModal {
  protected readonly printService = inject(PrintService);
  private readonly sanitizer = inject(DomSanitizer);

  @Input() order: SalesOrderResponse | null = null;
  @Input() isOpen = false;
  @Output() close = new EventEmitter<void>();

  activeFormat: PrintFormat = 'a4';

  readonly shopInfo = {
    name: 'SHREE JEWELLERS',
    owner: 'Chandan Kr Mandal',
    address: '3B8-2D, Sanhita Township, Newtown, 700135, WB',
    phone: '+91 98516 32391',
    email: 'chandankr.mandal87@gmail.com',
    gstin: '19AAACS1234F1Z5',
    state: 'West Bengal',
    stateCode: '19',
    hsn: '7113',
    hallmarkLicense: 'HM-916-WB-2026'
  };

  get amountInWords(): string {
    return this.order ? this.printService.numberToWords(this.order.netAmount) : '';
  }

  get totalGrossWeight(): number {
    return this.order?.items?.reduce((sum, item) => sum + (item.weightGrams || 0), 0) || 0;
  }

  get totalMakingCharges(): number {
    return this.order?.items?.reduce((sum, item) => sum + (item.makingCharges || 0), 0) || 0;
  }

  get totalHallmarkCharges(): number {
    return this.order?.items?.reduce((sum, item) => sum + (item.hallmarkCharges || 0), 0) || 0;
  }

  get totalStoneValue(): number {
    return this.order?.items?.reduce((sum, item) => sum + (item.stoneValue || 0), 0) || 0;
  }

  get barcodeSvg(): SafeHtml {
    const code = this.order?.orderNumber || 'SHREE-INV';
    return this.sanitizer.bypassSecurityTrustHtml(this.printService.generateBarcodeSvg(code, 200, 36));
  }

  get qrSvg(): SafeHtml {
    const data = `SHREE-JEWELLERS|INV:${this.order?.orderNumber}|AMT:${this.order?.netAmount}|DATE:${this.order?.orderDate}|GSTIN:${this.shopInfo.gstin}`;
    return this.sanitizer.bypassSecurityTrustHtml(this.printService.generateQrSvg(data, 100));
  }

  setFormat(format: PrintFormat): void {
    this.activeFormat = format;
  }

  printCurrent(): void {
    this.printService.triggerPrint(this.activeFormat);
  }

  onClose(): void {
    this.close.emit();
  }
}
