export interface PagedResponse<T> {
  total: number;
  page?: number;
  pageSize?: number;
  data: T[];
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  userId: string;
  fullName: string;
  email: string;
  customerCode: string;
  roles: string[];
  kycStatus: string;
}

export interface GoldPrice {
  rate22KPer10g: number;
  rate24KPer10g: number;
  silverRatePerKg: number;
  rate22KPerGram: number;
  rate24KPerGram: number;
  silverRatePerGram: number;
  recordedAt: string;
  isManualOverride: boolean;
  source: string;
}

export interface PublicHomeData {
  shopName: string;
  trustedSinceYear: number;
  heroHeadline: string;
  heroSubheadline: string;
  aboutSummary: string;
  highlights: Array<{ title: string; description: string; icon: string }>;
  featuredCategories: Array<{ categoryId: number; name: string; metalType: string; productCount: number; description: string }>;
  testimonials: Array<{ customerName: string; quote: string; location: string }>;
  quickLinks: Array<{ title: string; route: string }>;
}

export interface PublicAboutData {
  shopName: string;
  trustedSinceYear: number;
  story: string;
  ownerName: string;
  ownerTitle: string;
  ownerPhotoUrl?: string | null;
  certifications: string[];
  hallmarkNote: string;
}

export interface PublicContactData {
  shopName: string;
  address: string;
  phone: string;
  email: string;
  whatsAppNumber: string;
  mapEmbedUrl: string;
  businessHours: string;
}

export interface PublicGoldLoanInfo {
  interestRatePercent: number;
  interestCalculationType: string;
  defaultTenureMonths: number;
  loanToValuePercent: number;
  steps: string[];
  requiredDocuments: string[];
}

export interface ProductSummary {
  id: number;
  skuCode: string;
  name: string;
  categoryName: string;
  purity: string;
  netWeightGrams: number;
  stockQuantity: number;
  isLowStock: boolean;
  imageUrl?: string | null;
  currentTotalValue?: number | null;
}

export interface Product {
  id: number;
  categoryId: number;
  categoryName: string;
  name: string;
  description?: string | null;
  skuCode: string;
  hsnCode: string;
  purity: string;
  netWeightGrams: number;
  grossWeightGrams: number;
  stoneWeightGrams: number;
  hallmarkNumber?: string | null;
  makingChargesType: string;
  makingChargesValue: number;
  wastagePercent: number;
  hallmarkCharges: number;
  gstRatePercent: number;
  stoneValue: number;
  stockQuantity: number;
  reorderLevel: number;
  isLowStock: boolean;
  imageUrl?: string | null;
  barcodeData?: string | null;
  isActive: boolean;
  createdAt: string;
  currentMetalValue?: number | null;
  currentMakingCharges?: number | null;
  currentTotalValue?: number | null;
  weight?: number;
}

export interface Category {
  id: number;
  name: string;
  description?: string | null;
  metalType: string;
  productCount: number;
  isActive: boolean;
}

export interface InventoryTransaction {
  id: number;
  productId: number;
  productName: string;
  skuCode: string;
  transactionType: string;
  quantity: number;
  weightGrams: number;
  ratePerGram: number;
  totalValue: number;
  referenceNo?: string | null;
  notes?: string | null;
  createdByName: string;
  createdAt: string;
}

export interface StockLevel {
  productId: number;
  skuCode: string;
  productName: string;
  categoryName: string;
  purity: string;
  currentStock: number;
  reorderLevel: number;
  totalWeightGrams: number;
  currentMarketValue: number;
  isLowStock: boolean;
}

export interface InventoryValuation {
  asOf: string;
  totalGoldWeightGrams: number;
  totalSilverWeightGrams: number;
  totalGoldValueINR: number;
  totalSilverValueINR: number;
  totalInventoryValue: number;
  goldRate22K: number;
  goldRate24K: number;
  silverRatePerKg: number;
  byCategory: Array<{
    categoryName: string;
    productCount: number;
    totalPieces: number;
    totalWeightGrams: number;
    totalValue: number;
  }>;
}

export interface CustomerListItem {
  userId: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  customerCode?: string | null;
  kycStatus: string;
  isActive: boolean;
  createdAt: string;
  role: string;
}

export interface CustomerDetail extends CustomerListItem {
  firstName: string;
  lastName: string;
  alternatePhone?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  pinCode: string;
}

export interface UpdateCustomerPayload {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  alternatePhone?: string | null;
  email: string;
  dateOfBirth?: string | null;
  gender?: string | null;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  pinCode: string;
  isActive: boolean;
}

export interface AdminCreateUserPayload {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  alternatePhone?: string | null;
  email: string;
  dateOfBirth: string;
  gender: string;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  pinCode: string;
  role: 'Customer' | 'Staff' | 'Admin';
  aadhaarNumber?: string | null;
  panNumber?: string | null;
}

export interface CustomerDashboard {
  userId: string;
  fullName: string;
  customerCode: string;
  kycStatus: string;
  activeLoanCount: number;
  activeLoanOutstanding: number;
  interestAccrued: number;
  recentOrderCount: number;
  recentOrderValue: number;
  activeLoans: Array<{
    loanId: number;
    loanNumber: string;
    principalAmount: number;
    daysRemaining: number;
    interestAccrued: number;
    outstandingBalance: number;
    status: string;
  }>;
  recentOrders: Array<{
    orderId: number;
    orderNumber: string;
    orderDate: string;
    netAmount: number;
    status: string;
    paymentStatus: string;
  }>;
}

export interface Profile {
  userId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phoneNumber: string;
  alternatePhone?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  addressLine1: string;
  addressLine2?: string | null;
  city: string;
  state: string;
  pinCode: string;
  customerCode?: string | null;
  kycStatus: string;
  profilePhotoUrl?: string | null;
  idProofUrl?: string | null;
  kycRejectionReason?: string | null;
}

export interface GoldLoanSummary {
  id: number;
  loanNumber: string;
  customerName: string;
  customerCode: string;
  loanDate: string;
  maturityDate: string;
  principalAmount: number;
  totalRepaid: number;
  outstandingBalance: number;
  accruedInterest: number;
  loanStatus: string;
  isOverdue: boolean;
  daysOverdue: number;
}

export interface GoldLoanResponse {
  id: number;
  loanNumber: string;
  customerUserId: string;
  customerName: string;
  customerCode: string;
  loanDate: string;
  maturityDate: string;
  closedDate?: string | null;
  principalAmount: number;
  totalRepaid: number;
  goldDepositedWeightGrams: number;
  goldPurity: string;
  goldValueAtDeposit: number;
  loanToValuePercent: number;
  loanStatus: string;
  extensionCount: number;
  notes?: string | null;
  interestCalculationType: string;
  interestRatePercent: number;
  compoundingEnabled: boolean;
  goldItems: Array<{
    id: number;
    itemDescription: string;
    weightGrams: number;
    purity: string;
    estimatedValue: number;
    hallmarkNumber?: string | null;
    imageUrl?: string | null;
  }>;
  repayments: Array<{
    id: number;
    receiptNumber: string;
    repaymentDate: string;
    amountPaid: number;
    principalComponent: number;
    interestComponent: number;
    penaltyAmount: number;
    paymentMode: string;
    principalBalanceAfter: number;
    notes?: string | null;
    createdAt: string;
  }>;
  createdAt: string;
}

export interface OutstandingBalance {
  loanId: number;
  loanNumber: string;
  asOfDate: string;
  principalAtOrigin: number;
  totalRepaid: number;
  principalRemaining: number;
  accruedInterest: number;
  penaltyInterest: number;
  totalOutstanding: number;
  payoffAmount: number;
  isOverdue: boolean;
  daysOverdue: number;
  loanStatus: string;
  maturityDate: string;
  interestRatePercent: number;
  calculationType: string;
  compoundingEnabled: boolean;
}

export interface RepaymentSchedule {
  loanId: number;
  loanNumber: string;
  principalAmount: number;
  totalInterestIfPaidOnSchedule: number;
  totalPayableIfPaidOnSchedule: number;
  loanDate: string;
  maturityDate: string;
  schedule: Array<{
    installmentNumber: number;
    dueDate: string;
    openingBalance: number;
    interestDue: number;
    totalDue: number;
    closingBalance: number;
    isPaid: boolean;
    amountActuallyPaid?: number | null;
    actualPaymentDate?: string | null;
  }>;
}

export interface LoanEligibility {
  isEligible: boolean;
  ineligibilityReason?: string | null;
  totalGoldWeightGrams: number;
  totalGoldMarketValue: number;
  loanToValuePercent: number;
  maxEligibleLoanAmount: number;
  currentGoldRatePer10g: number;
  goldPurityAssumed: string;
  interestRatePercent: number;
  interestCalculationType: string;
  proposedMaturityDate: string;
}

export interface SalesOrderItem {
  id: number;
  productId: number;
  productName: string;
  skuCode: string;
  purity: string;
  quantity: number;
  weightGrams: number;
  ratePerGram: number;
  makingCharges: number;
  hallmarkCharges: number;
  stoneValue: number;
  taxPercent: number;
  taxAmount: number;
  discountAmount: number;
  lineTotal: number;
}

export interface SalesOrderSummary {
  id: number;
  orderNumber: string;
  customerName?: string | null;
  customerCode?: string | null;
  walkInCustomerName?: string | null;
  orderDate: string;
  netAmount: number;
  balanceDue: number;
  paymentStatus: string;
  status: string;
  hasInvoice: boolean;
}

export interface SalesOrderResponse {
  id: number;
  orderNumber: string;
  customerUserId: string | null;
  customerName?: string | null;
  customerCode?: string | null;
  walkInCustomerName?: string | null;
  walkInCustomerPhone?: string | null;
  orderDate: string;
  grossAmount: number;
  discountAmount: number;
  oldGoldExchangeValue: number;
  taxAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  netAmount: number;
  amountPaid: number;
  advanceAmount: number;
  balanceDue: number;
  paymentMode: 'Cash' | 'Card' | 'UPI' | 'NEFT' | 'Cheque' | 'Split';
  paymentStatus: 'Pending' | 'PartiallyPaid' | 'Paid' | 'Refunded';
  status: 'Draft' | 'Confirmed' | 'Delivered' | 'Cancelled' | 'Returned';
  invoiceUrl?: string | null;
  notes?: string | null;
  items: SalesOrderItem[];
  createdAt: string;
}

export interface CreateSalesOrderDto {
  customerUserId?: string | null;
  walkInCustomerName?: string;
  walkInCustomerPhone?: string;
  orderDate: string;
  items: CreateSalesOrderItemDto[];
  paymentMode: 'Cash' | 'Card' | 'UPI' | 'NEFT' | 'Cheque' | 'Split';
  amountPaid: number;
  advanceAmount?: number;
  discountAmount?: number;
  oldGoldExchangeValue?: number;
  oldGoldWeightGrams?: number;
  oldGoldPurity?: string;
  isInterState?: boolean;
  notes?: string;
}

export interface CreateSalesOrderItemDto {
  productId: number;
  quantity: number;
  weightGrams: number;
  ratePerGram: number;
  makingCharges: number;
  hallmarkCharges: number;
  stoneValue: number;
  discountAmount?: number;
}

export interface ReturnOrderDto {
  originalOrderId: number;
  itemIdsToReturn: number[];
  returnReason: string;
  refundMode: 'Cash' | 'Card' | 'UPI' | 'NEFT' | 'StoreCredit';
  notes?: string;
}

export interface SalesFilterDto {
  fromDate?: string;
  toDate?: string;
  customerSearch?: string;
  status?: 'Draft' | 'Confirmed' | 'Delivered' | 'Cancelled' | 'Returned';
  paymentStatus?: 'Pending' | 'PartiallyPaid' | 'Paid' | 'Refunded';
  page: number;
  pageSize: number;
}

export interface DailySalesSummaryDto {
  date: string;
  orderCount: number;
  totalGrossAmount: number;
  totalDiscounts: number;
  totalTax: number;
  totalNetAmount: number;
  totalAmountReceived: number;
  byPaymentMode: Record<string, number>;
  byCategoryName: Record<string, number>;
}
