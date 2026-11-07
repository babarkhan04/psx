export interface Lookup {
  id: string;
  name: string;
}
export interface PosContext {
  storeId: string;
  storeName: string;
  registerId: string;
  registerName: string;
  requiresShift: boolean;
  hasOpenShift: boolean;
  shiftId: string | null;
  shiftNumber: string | null;
  shiftCashBalance: number | null;
}
export interface PosProduct {
  productId: string;
  variantId: string | null;
  sku: string;
  name: string;
  imageUrl: string | null;
  unitPrice: number;
  taxRate: number;
  stockOnHand: number;
  categoryId: string | null;
  brandId: string | null;
  unitAbbreviation: string | null;
  isFavorite: boolean;
  favoriteSortOrder: number;
  isOutOfStock: boolean;
}
export interface CustomerLookup {
  id: string;
  name: string;
  phone: string | null;
  creditLimit: number;
  balance: number;
  availableCredit: number;
  display: string;
}
export interface PosShortcut {
  action: string;
  keyCombination: string;
  isEnabled: boolean;
}
export interface PosBootstrap {
  context: PosContext | null;
  filters: {
    categories: Lookup[];
    brands: Lookup[];
  };
  products: PosProduct[];
  customers: CustomerLookup[];
  shortcuts: PosShortcut[];
}
export interface CartLine {
  productId: string;
  variantId: string | null;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  discountReason: string | null;
  taxRate: number;
}
export interface HeldSaleListItem {
  id: string;
  heldNumber: string;
  heldOn: string;
  expiresOn: string | null;
  minutesRemaining: number | null;
  expiryText: string;
  cashierName: string;
  customerName: string;
  itemCount: number;
  totalAmount: number;
}
export interface HeldSale {
  id: string;
  heldNumber: string;
  storeId: string;
  registerId: string;
  customerId: string | null;
  customerName: string;
  invoiceDiscountAmount: number;
  discountReason: string | null;
  notes: string | null;
  lines: CartLine[];
}
export interface Receipt {
  saleId: string;
  saleNumber: string;
  storeName: string;
  registerName: string;
  cashierName: string;
  customerName: string;
  paymentMethod: string;
  subtotal: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  amountPaid: number;
  changeDue: number;
  completedOn: string;
  lines: Array<{
    sku: string;
    name: string;
    quantity: number;
    unitPrice: number;
    discountAmount: number;
    lineTotal: number;
  }>;
  payments?: Array<{ method: string; amount: number; reference: string | null }>;
}

export interface PaymentLine {
  id: string;
  method: 'Cash' | 'Card' | 'Other' | 'Credit';
  amount: number;
  reference: string;
}

export interface ManagerApprovalResult {
  approvalId: string;
  approvedByUserId: string;
  approvedByName: string;
  approvedOn: string;
}
