import { api } from '../../lib/api';
import type {
  InventoryBootstrap,
  StockAdjustmentRequest,
  StockLedgerItem
} from './types';

export const inventoryApi = {
  bootstrap: (storeId: string, lowStockOnly = false, ledgerTake = 180) =>
    api.get<InventoryBootstrap>(
      `/inventory/bootstrap?storeId=${encodeURIComponent(storeId)}&lowStockOnly=${lowStockOnly}&ledgerTake=${ledgerTake}`
    ),

  ledger: (storeId: string, productId?: string | null, take = 180) => {
    const product = productId ? `&productId=${encodeURIComponent(productId)}` : '';
    return api.get<StockLedgerItem[]>(
      `/inventory/ledger?storeId=${encodeURIComponent(storeId)}${product}&take=${take}`
    );
  },

  adjust: (request: StockAdjustmentRequest) =>
    api.post<void>('/inventory/adjustment', request),

  opening: (storeId: string, productId: string, quantity: number, variantId?: string | null) =>
    api.post<void>('/inventory/opening', { storeId, productId, quantity, variantId })
};
