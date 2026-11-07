import { api } from '../../lib/api';
import type {
  CartLine, HeldSale, HeldSaleListItem, ManagerApprovalResult, PosBootstrap, PosProduct, Receipt
} from './types';

export const posApi = {
  bootstrap: (storeId: string | null) =>
    api.get<PosBootstrap>(`/pos/bootstrap${storeId ? `?storeId=${encodeURIComponent(storeId)}` : ''}`),

  products: (storeId: string, categoryId: string | null, brandId: string | null, search: string) => {
    const q = new URLSearchParams({ storeId });
    if (categoryId) q.set('categoryId', categoryId);
    if (brandId) q.set('brandId', brandId);
    if (search.trim()) q.set('search', search.trim());
    return api.get<PosProduct[]>(`/pos/products?${q}`);
  },

  barcode: (storeId: string, code: string) =>
    api.get<PosProduct>(`/pos/barcode?storeId=${encodeURIComponent(storeId)}&code=${encodeURIComponent(code)}`),

  favorite: (productId: string, isFavorite: boolean) =>
    api.post<void>(`/pos/favorites/${productId}?isFavorite=${isFavorite}`),

  checkout: (payload: {
    storeId: string;
    registerId: string;
    customerId: string | null;
    customerName: string;
    paymentMethod: string;
    amountTendered: number;
    invoiceDiscountAmount: number;
    discountReason: string | null;
    heldSaleId: string | null;
    lines: CartLine[];
    payments: Array<{ method: string; amount: number; reference?: string | null }>;
  }) => api.post<Receipt>('/pos/checkout', payload),

  hold: (payload: {
    storeId: string;
    registerId: string;
    customerId: string | null;
    customerName: string;
    invoiceDiscountAmount: number;
    discountReason: string | null;
    lines: CartLine[];
  }) => api.post<string>('/pos/hold', payload),

  held: (storeId: string, registerId: string) =>
    api.get<HeldSaleListItem[]>(`/pos/held?storeId=${encodeURIComponent(storeId)}&registerId=${encodeURIComponent(registerId)}`),

  heldSale: (heldSaleId: string) => api.get<HeldSale>(`/pos/held/${heldSaleId}`),
  cancelHeld: (heldSaleId: string) => api.delete<void>(`/pos/held/${heldSaleId}`),

  managerApproval: (payload: {
    storeId: string | null;
    approvalType: string;
    managerEmail: string;
    managerPassword: string;
    reference?: string | null;
    amount?: number | null;
    reason: string;
  }) => api.post<ManagerApprovalResult>('/pos/manager-approval', payload),
};
