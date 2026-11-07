import { api, ApiError } from '../../lib/api';
import type {
  CatalogBootstrap,
  ProductDetail,
  ProductImportResult
} from './types';

function qs(params: Record<string, string | boolean | number | null | undefined>) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== '') {
      search.set(key, String(value));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

async function parseRawError(response: Response) {
  let body: any = null;
  try { body = await response.json(); } catch { /* ignore */ }
  throw new ApiError(response.status, body?.message ?? `Request failed (${response.status}).`, body?.errorCode);
}

export const catalogApi = {
  bootstrap: (storeId: string | null, search: string, assignedOnly: boolean) =>
    api.get<CatalogBootstrap>(`/catalog/bootstrap${qs({ storeId, search, assignedOnly })}`),

  product: (id: string) =>
    api.get<ProductDetail>(`/catalog/products/${id}`),

  create: (model: ProductDetail) =>
    api.post<{ id: string }>(`/catalog/products`, model),

  update: (id: string, model: ProductDetail) =>
    api.post<void>(`/catalog/products/${id}`, model),

  setActive: (id: string, isActive: boolean) =>
    api.post<void>(`/catalog/products/${id}/active`, { isActive }),

  delete: (id: string) =>
    api.delete<void>(`/catalog/products/${id}`),

  assign: (storeId: string, productIds: string[], isVisible: boolean) =>
    api.post<void>('/catalog/assign', { storeId, productIds, isVisible }),

  importProducts: async (file: File, storeId: string | null) => {
    const data = new FormData();
    data.append('file', file);
    const suffix = storeId ? `?storeId=${encodeURIComponent(storeId)}` : '';
    const response = await fetch(`/api/react/catalog/import${suffix}`, {
      method: 'POST',
      credentials: 'include',
      body: data
    });
    if (!response.ok) await parseRawError(response);
    return response.json() as Promise<ProductImportResult>;
  },

  uploadImage: async (file: File) => {
    const data = new FormData();
    data.append('file', file);
    const response = await fetch('/api/products/upload-image', {
      method: 'POST',
      credentials: 'include',
      body: data
    });
    if (!response.ok) await parseRawError(response);
    return response.json() as Promise<{ url: string }>;
  }
};
