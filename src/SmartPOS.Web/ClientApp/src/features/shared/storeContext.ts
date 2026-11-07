import { create } from 'zustand';

interface StoreState {
  storeId: string | null;
  setStoreId: (storeId: string | null) => void;
}

export const useStoreContext = create<StoreState>((set) => ({
  storeId: localStorage.getItem('SmartPOS.React.SelectedStoreId'),
  setStoreId: (storeId) => {
    if (storeId) localStorage.setItem('SmartPOS.React.SelectedStoreId', storeId);
    else localStorage.removeItem('SmartPOS.React.SelectedStoreId');
    set({ storeId });
  }
}));
