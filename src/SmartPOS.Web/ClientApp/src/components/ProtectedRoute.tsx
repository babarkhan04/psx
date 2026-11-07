import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../features/auth/authStore';

export default function ProtectedRoute() {
  const user = useAuthStore((s) => s.user);
  const bootstrapped = useAuthStore((s) => s.bootstrapped);
  if (!bootstrapped) return <div className="page-loader">Loading SmartPOS…</div>;
  if (!user) return <Navigate to="/login" replace />;
  return <Outlet />;
}
