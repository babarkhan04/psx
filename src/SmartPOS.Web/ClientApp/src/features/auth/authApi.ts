import { api } from '../../lib/api';
import type { CurrentUser } from './types';

export const authApi = {
  login: (email: string, password: string) =>
    api.post<{ signedIn: boolean; displayName: string; email: string }>('/auth/login', { email, password }),
  me: () => api.get<CurrentUser>('/auth/me'),
  logout: () => api.post<void>('/auth/logout'),
  selectStore: (storeId: string) => api.post<void>('/stores/select', { storeId })
};
