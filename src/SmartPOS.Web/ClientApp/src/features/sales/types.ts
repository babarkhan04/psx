export interface SaleListItem {
  id: string;
  saleNumber: string;
  completedOn: string;
  storeName: string;
  registerName: string;
  cashierName: string;
  customerName: string;
  paymentMethod: string;
  status: string;
  itemCount: number;
  totalAmount: number;
}

export interface SalesSummary {
  saleCount: number;
  grossSales: number;
  taxCollected: number;
  itemsSold: number;
}

export interface SalesPage {
  sales: SaleListItem[];
  summary: SalesSummary;
}

export interface ReceiptLine {
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  discountReason?: string | null;
  lineTotal: number;
}

export interface ReceiptPayment {
  method: string;
  amount: number;
  reference: string | null;
}

export interface ReceiptReprintLog {
  id: string;
  reprintedByName: string;
  reason: string;
  reprintedOn: string;
}

export interface SalesReceipt {
  saleId: string;
  saleNumber: string;
  storeId: string;
  registerId: string | null;
  storeName: string;
  registerName: string;
  cashierName: string;
  customerName: string;
  paymentMethod: string;
  subtotal: number;
  discountAmount: number;
  discountReason: string | null;
  taxAmount: number;
  totalAmount: number;
  amountPaid: number;
  changeDue: number;
  completedOn: string;
  lines: ReceiptLine[];
  payments: ReceiptPayment[];
  reprintCount: number;
  reprintLogs: ReceiptReprintLog[];
}

export interface SaleForReturnLine {
  saleLineId: string;
  sku: string;
  productName: string;
  quantitySold: number;
  quantityAlreadyReturned: number;
  quantityReturnable: number;
  unitPrice: number;
  discountedUnitPrice: number;
  taxRate: number;
  lineTotal: number;
}

export interface SaleForReturn {
  saleId: string;
  saleNumber: string;
  customerName: string;
  customerId: string | null;
  originalPaymentMethod: string;
  totalAmount: number;
  completedOn: string;
  lines: SaleForReturnLine[];
}

export interface ReturnLineInput {
  saleLineId: string;
  quantityReturned: number;
  disposition: 'Restock' | 'Damaged' | 'Discard';
  lineReason: string | null;
}

export interface ProcessReturnRequest {
  saleId: string;
  returnType: 'Refund' | 'Exchange';
  refundMethod: 'Cash' | 'Card' | 'Other' | 'Credit';
  returnReason: string;
  refundReference: string | null;
  exchangeReference: string | null;
  notes: string | null;
  lines: ReturnLineInput[];
}

export interface ReturnReceiptLine {
  sku: string;
  productName: string;
  quantityReturned: number;
  lineTotal: number;
  disposition: string;
  lineReason: string | null;
}

export interface ReturnReceipt {
  returnId: string;
  returnNumber: string;
  originalSaleNumber: string;
  returnType: string;
  refundMethod: string;
  customerName: string;
  returnReason: string;
  refundReference: string | null;
  exchangeReference: string | null;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  processedOn: string;
  lines: ReturnReceiptLine[];
}
