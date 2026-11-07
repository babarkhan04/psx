export interface StoreOption {
  id: string;
  code: string;
  name: string;
  displayName: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  stores: StoreOption[];
  selectedStoreId: string | null;
}
