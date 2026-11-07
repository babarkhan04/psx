import { api } from '../../lib/api';
import type { ManagerApprovalResult } from '../pos/types';
import type {
  ProcessReturnRequest, ReturnReceipt, SaleForReturn, SalesPage, SalesReceipt
} from './types';

export const salesApi = {
  list: (args: { storeId: string; fromUtc?: string; toUtc?: string; search?: string; take?: number }) => {
    const q = new URLSearchParams({ storeId: args.storeId });
    if (args.fromUtc) q.set('fromUtc', args.fromUtc);
    if (args.toUtc) q.set('toUtc', args.toUtc);
    if (args.search?.trim()) q.set('search', args.search.trim());
    q.set('take', String(args.take ?? 250));
    return api.get<SalesPage>(`/sales?${q}`);
  },
  receipt: (saleId: string) => api.get<SalesReceipt>(`/sales/${saleId}/receipt`),
  logReprint: (saleId: string, reason: string) => api.post<void>(`/sales/${saleId}/reprint`, { reason }),
  returnDetail: (saleId: string) => api.get<SaleForReturn>(`/sales/${saleId}/return`),
  processReturn: (payload: ProcessReturnRequest) => api.post<ReturnReceipt>('/sales/return', payload),
  voidSale: (saleId: string, reason: string) => api.post<void>('/sales/void', { saleId, reason }),
  managerApproval: (payload: {
    storeId: string | null;
    managerEmail: string;
    managerPassword: string;
    reference: string;
    amount: number;
    reason: string;
  }) => api.post<ManagerApprovalResult>('/sales/manager-approval', {
    ...payload,
    approvalType: 'VoidSale'
  })
};
