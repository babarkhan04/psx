import type { StoreOption } from '../auth/types';

export interface LookupOption {
  id: string;
  name: string;
}

export interface ProductListItem {
  id: string;
  sku: string;
  name: string;
  barcode?: string | null;
  categoryName?: string | null;
  brandName?: string | null;
  unitAbbreviation?: string | null;
  costPrice: number;
  sellPrice: number;
  reorderLevel: number;
  imageUrl?: string | null;
  taxRate?: number | null;
  isActive: boolean;
  variantCount: number;
  isAssignedToSelectedStore: boolean;
}

export interface ProductVariant {
  id: string;
  sku: string;
  name: string;
  barcode?: string | null;
  costPrice?: number | null;
  sellPrice?: number | null;
  isActive: boolean;
}

export interface ProductStorePrice {
  storeId: string;
  storeName: string;
  price?: number | null;
}

export interface ProductStoreAssignment {
  storeId: string;
  storeName: string;
  isVisibleOnPos: boolean;
}

export interface ProductDetail {
  id: string;
  sku: string;
  name: string;
  description?: string | null;
  barcode?: string | null;
  categoryId?: string | null;
  brandId?: string | null;
  unitId?: string | null;
  taxCategoryId?: string | null;
  costPrice: number;
  sellPrice: number;
  reorderLevel: number;
  imageUrl?: string | null;
  isActive: boolean;
  additionalBarcodes: string[];
  storePrices: ProductStorePrice[];
  storeAssignments: ProductStoreAssignment[];
  variants: ProductVariant[];
}

export interface CatalogBootstrap {
  products: ProductListItem[];
  categories: LookupOption[];
  brands: LookupOption[];
  units: LookupOption[];
  taxes: LookupOption[];
  stores: StoreOption[];
}

export interface ProductImportResult {
  created: number;
  updated: number;
  skipped: number;
  errors: string[];
}
