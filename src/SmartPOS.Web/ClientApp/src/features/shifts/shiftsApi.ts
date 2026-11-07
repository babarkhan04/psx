import { api } from '../../lib/api';
import type { CashDenominationLine, RegisterOption, ShiftListItem, ShiftReport } from './types';

export const shiftsApi = {
  list: (storeId: string, openOnly = false) =>
    api.get<ShiftListItem[]>(`/shifts?storeId=${encodeURIComponent(storeId)}&openOnly=${openOnly}`),

  registers: (storeId: string) =>
    api.get<RegisterOption[]>(`/shifts/registers?storeId=${encodeURIComponent(storeId)}`),

  open: (storeId: string, payload: { registerId: string; openingFloat: number; notes?: string | null }) =>
    api.post<string>(`/shifts/open?storeId=${encodeURIComponent(storeId)}`, payload),

  cashMovement: (storeId: string, payload: { shiftId: string; movementType: 'CashIn' | 'CashOut'; amount: number; notes?: string | null }) =>
    api.post<void>(`/shifts/cash-movement?storeId=${encodeURIComponent(storeId)}`, payload),

  noSale: (storeId: string, payload: { shiftId: string; reason: string }) =>
    api.post<void>(`/shifts/no-sale?storeId=${encodeURIComponent(storeId)}`, payload),

  close: (storeId: string, payload: { shiftId: string; countedCash: number; denominations: CashDenominationLine[]; notes?: string | null }) =>
    api.post<void>(`/shifts/close?storeId=${encodeURIComponent(storeId)}`, payload),

  report: (storeId: string, shiftId: string) =>
    api.get<ShiftReport>(`/shifts/${encodeURIComponent(shiftId)}/report?storeId=${encodeURIComponent(storeId)}`)
};
