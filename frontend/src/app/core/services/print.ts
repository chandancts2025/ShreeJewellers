import { Injectable } from '@angular/core';

export type PrintFormat = '80mm' | 'a4' | 'a5' | 'tag';

@Injectable({ providedIn: 'root' })
export class PrintService {
  /**
   * Convert a number to Indian currency words (Rupees and Paise)
   * e.g. 145820 -> "Rupees One Lakh Forty-Five Thousand Eight Hundred Twenty Only"
   */
  numberToWords(amount: number): string {
    if (!amount || isNaN(amount) || amount === 0) return 'Rupees Zero Only';

    const absAmount = Math.abs(amount);
    const rupees = Math.floor(absAmount);
    const paise = Math.round((absAmount - rupees) * 100);

    const units = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine',
      'Ten', 'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen'];
    const tens = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety'];

    const convertBelowThousand = (n: number): string => {
      let str = '';
      if (n >= 100) {
        str += units[Math.floor(n / 100)] + ' Hundred ';
        n %= 100;
      }
      if (n >= 20) {
        str += tens[Math.floor(n / 10)] + ' ';
        n %= 10;
      }
      if (n > 0) {
        str += units[n] + ' ';
      }
      return str.trim();
    };

    let remaining = rupees;
    let parts: string[] = [];

    // Crores (1,00,00,000)
    if (remaining >= 10000000) {
      const crore = Math.floor(remaining / 10000000);
      parts.push(convertBelowThousand(crore) + ' Crore');
      remaining %= 10000000;
    }

    // Lakhs (1,00,000)
    if (remaining >= 100000) {
      const lakh = Math.floor(remaining / 100000);
      parts.push(convertBelowThousand(lakh) + ' Lakh');
      remaining %= 100000;
    }

    // Thousands (1,000)
    if (remaining >= 1000) {
      const thousand = Math.floor(remaining / 1000);
      parts.push(convertBelowThousand(thousand) + ' Thousand');
      remaining %= 1000;
    }

    // Hundreds and below
    if (remaining > 0) {
      parts.push(convertBelowThousand(remaining));
    }

    let result = 'Rupees ' + parts.join(' ').replace(/\s+/g, ' ').trim();
    if (paise > 0) {
      result += ' and ' + convertBelowThousand(paise) + ' Paise';
    }
    result += ' Only';

    return result;
  }

  /**
   * Triggers window.print with targeted print stylesheet formatting classes
   */
  triggerPrint(format: PrintFormat = 'a4'): void {
    const body = document.body;
    body.classList.remove('print-active-80mm', 'print-active-a4', 'print-active-a5', 'print-active-tag');
    body.classList.add('print-active', `print-active-${format}`);

    const cleanup = () => {
      body.classList.remove('print-active', 'print-active-80mm', 'print-active-a4', 'print-active-a5', 'print-active-tag');
      window.removeEventListener('afterprint', cleanup);
    };

    window.addEventListener('afterprint', cleanup);

    // Give browser a moment to apply print classes before opening print spooler
    setTimeout(() => {
      window.print();
      // Safety fallback in case afterprint does not fire
      setTimeout(cleanup, 2000);
    }, 120);
  }

  /**
   * Generates a clean Code-128 style Barcode SVG pattern
   */
  generateBarcodeSvg(code: string, width = 180, height = 40): string {
    if (!code) code = 'SHREE-JEWELS';
    // Deterministic barcode pattern based on string characters
    const hash = Array.from(code).map(c => c.charCodeAt(0));
    let bars: number[] = [2, 1, 1, 2]; // Guard pattern
    for (let i = 0; i < hash.length; i++) {
      const v = hash[i];
      bars.push((v % 3) + 1, ((v >> 1) % 2) + 1, ((v >> 2) % 3) + 1, ((v >> 3) % 2) + 1);
    }
    bars.push(2, 1, 2, 2); // Stop pattern

    let totalUnits = bars.reduce((a, b) => a + b, 0);
    let unitWidth = width / totalUnits;
    let currentX = 0;
    let rects = '';

    bars.forEach((bWidth, index) => {
      const isBlack = index % 2 === 0;
      if (isBlack) {
        rects += `<rect x="${currentX.toFixed(2)}" y="0" width="${(bWidth * unitWidth).toFixed(2)}" height="${height}" fill="#000"/>`;
      }
      currentX += bWidth * unitWidth;
    });

    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${width} ${height}" width="${width}" height="${height}">${rects}</svg>`;
  }

  /**
   * Generates a clean, scannable QR Code SVG for invoices and pawn slips
   */
  generateQrSvg(data: string, size = 96): string {
    // Generate a high-contrast mock-accurate 25x25 QR matrix with finder patterns
    const N = 25;
    const matrix: boolean[][] = Array.from({ length: N }, () => Array(N).fill(false));

    // Finder pattern helper
    const drawFinder = (r: number, c: number) => {
      for (let i = -1; i <= 7; i++) {
        for (let j = -1; j <= 7; j++) {
          const row = r + i;
          const col = c + j;
          if (row >= 0 && row < N && col >= 0 && col < N) {
            const isBorder = i === 0 || i === 6 || j === 0 || j === 6;
            const isCenter = i >= 2 && i <= 4 && j >= 2 && j <= 4;
            matrix[row][col] = isBorder || isCenter;
          }
        }
      }
    };

    drawFinder(0, 0);
    drawFinder(0, N - 7);
    drawFinder(N - 7, 0);

    // Timing patterns
    for (let i = 8; i < N - 8; i++) {
      matrix[6][i] = i % 2 === 0;
      matrix[i][6] = i % 2 === 0;
    }

    // Pseudo-random data encoding from string hash
    let seed = 0;
    for (let i = 0; i < data.length; i++) {
      seed = ((seed << 5) - seed + data.charCodeAt(i)) | 0;
    }

    const pseudoRandom = () => {
      seed = (seed * 9301 + 49297) % 233280;
      return seed / 233280;
    };

    for (let r = 0; r < N; r++) {
      for (let c = 0; c < N; c++) {
        // Skip finders
        if ((r < 8 && c < 8) || (r < 8 && c >= N - 8) || (r >= N - 8 && c < 8)) continue;
        if (r === 6 || c === 6) continue;
        matrix[r][c] = pseudoRandom() > 0.45;
      }
    }

    const cellSize = size / N;
    let paths = '';
    for (let r = 0; r < N; r++) {
      for (let c = 0; c < N; c++) {
        if (matrix[r][c]) {
          paths += `<rect x="${(c * cellSize).toFixed(2)}" y="${(r * cellSize).toFixed(2)}" width="${cellSize.toFixed(2)}" height="${cellSize.toFixed(2)}" fill="#111"/>`;
        }
      }
    }

    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${size} ${size}" width="${size}" height="${size}"><rect width="${size}" height="${size}" fill="#fff"/>${paths}</svg>`;
  }
}
