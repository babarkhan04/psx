import { useEffect } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import AppShell from '../components/AppShell';
import ProtectedRoute from '../components/ProtectedRoute';
import DashboardPage from '../pages/DashboardPage';
import LoginPage from '../pages/LoginPage';
import PosPage from '../pages/PosPage';
import ShiftsPage from '../pages/ShiftsPage';
import SalesPage from '../pages/SalesPage';
import ProductsPage from '../pages/ProductsPage';
import InventoryPage from '../pages/InventoryPage';
import LegacyPage from '../pages/LegacyPage';
import { authApi } from '../features/auth/authApi';
import { useAuthStore } from '../features/auth/authStore';
import { useStoreContext } from '../features/shared/storeContext';

export default function App() {
  const setUser = useAuthStore(s => s.setUser);
  const setBootstrapped = useAuthStore(s => s.setBootstrapped);
  const setStoreId = useStoreContext(s => s.setStoreId);

  useEffect(() => {
    let active = true;
    authApi.me().then(user => {
      if (!active) return;
      setUser(user);
      const persisted = localStorage.getItem('SmartPOS.React.SelectedStoreId');
      const valid = persisted && user.stores.some(s => s.id === persisted) ? persisted : null;
      setStoreId(valid ?? user.selectedStoreId ?? user.stores[0]?.id ?? null);
    }).catch(() => active && setUser(null))
      .finally(() => active && setBootstrapped(true));

    const unauthorized = () => setUser(null);
    window.addEventListener('smartpos:unauthorized', unauthorized);
    return () => { active = false; window.removeEventListener('smartpos:unauthorized', unauthorized); };
  }, [setBootstrapped,setStoreId,setUser]);

  return <Routes>
    <Route path="/login" element={<LoginPage/>}/>
    <Route element={<ProtectedRoute/>}>
      <Route element={<AppShell/>}>
        <Route path="/dashboard" element={<DashboardPage/>}/>
        <Route path="/pos" element={<PosPage/>}/>
        <Route path="/sales" element={<SalesPage/>}/>
        <Route path="/shifts" element={<ShiftsPage/>}/>
        <Route path="/products" element={<ProductsPage/>}/>
        <Route path="/inventory" element={<InventoryPage/>}/>
        <Route path="/purchases" element={<LegacyPage title="Purchases" href="/purchases"/>}/>
        <Route path="/reorder" element={<LegacyPage title="Smart Reorder" href="/reorder"/>}/>
        <Route path="/transfers" element={<LegacyPage title="Transfers" href="/transfers"/>}/>
        <Route path="/labels" element={<LegacyPage title="Barcode & Labels" href="/labels"/>}/>
        <Route path="/customers" element={<LegacyPage title="Customers" href="/customers"/>}/>
        <Route path="/suppliers" element={<LegacyPage title="Suppliers" href="/suppliers"/>}/>
        <Route path="/reports" element={<LegacyPage title="Reports" href="/reports"/>}/>
        <Route path="/alerts" element={<LegacyPage title="Smart Alerts" href="/alerts"/>}/>
        <Route path="/risk" element={<LegacyPage title="Risk Center" href="/risk"/>}/>
        <Route path="/audit" element={<LegacyPage title="Audit Trail" href="/audit"/>}/>
        <Route path="/setup" element={<LegacyPage title="Administration Setup" href="/setup/company"/>}/>
      </Route>
    </Route>
    <Route path="/" element={<Navigate to="/dashboard" replace/>}/>
    <Route path="*" element={<Navigate to="/dashboard" replace/>}/>
  </Routes>;
}
