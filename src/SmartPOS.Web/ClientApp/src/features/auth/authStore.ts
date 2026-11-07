import { create } from 'zustand';
import type { CurrentUser } from './types';

interface AuthState {
  user: CurrentUser | null;
  bootstrapped: boolean;
  setUser: (user: CurrentUser | null) => void;
  setBootstrapped: (value: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  bootstrapped: false,
  setUser: (user) => set({ user }),
  setBootstrapped: (bootstrapped) => set({ bootstrapped })
}));
