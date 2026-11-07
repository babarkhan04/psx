import { api } from '../../lib/api';
import type { DashboardData } from './types';

export const dashboardApi = {
  get: (storeId: string | null) =>
    api.get<DashboardData>(`/dashboard${storeId ? `?storeId=${encodeURIComponent(storeId)}` : ''}`)
};
