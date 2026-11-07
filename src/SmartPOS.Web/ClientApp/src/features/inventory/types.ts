import type { ProductListItem } from '../catalog/types';

export interface StockItem {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  variantName?: string | null;
  storeId: string;
  storeName: string;
  quantityOnHand: number;
  reorderLevel: number;
  isLowStock: boolean;
}

export interface StockLedgerItem {
  id: string;
  createdOn: string;
  createdBy?: string | null;
  movementType: string;
  productName: string;
  variantName?: string | null;
  storeName: string;
  quantityChange: number;
  quantityAfter: number;
  reference?: string | null;
  notes?: string | null;
}

export interface InventoryBootstrap {
  stock: StockItem[];
  ledger: StockLedgerItem[];
  products: ProductListItem[];
}

export interface StockAdjustmentLine {
  productId: string;
  productVariantId?: string | null;
  productName: string;
  quantityChange: number;
}

export interface StockAdjustmentRequest {
  storeId: string;
  movementType: 'Opening' | 'Adjustment' | 'Damage' | 'Loss';
  reference?: string | null;
  notes?: string | null;
  lines: StockAdjustmentLine[];
}
