import {
  Bell, Boxes, Building2, ChevronDown, ClipboardList, ExternalLink, FileBarChart2,
  History, LayoutDashboard, Menu, PackageSearch, ReceiptText, RotateCcw, Search,
  Settings, ShoppingCart, Store, Tags, Truck, UserRound, Users, WalletCards, X,
  ShieldAlert, Zap
} from 'lucide-react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useAuthStore } from '../features/auth/authStore';
import { useStoreContext } from '../features/shared/storeContext';
import { authApi } from '../features/auth/authApi';

const permissions = {
  pos: 'sales.pos.operate', sales: 'sales.view', shifts: 'operations.shifts.view',
  products: 'catalog.products.view', inventory: 'catalog.inventory.view', purchases: 'catalog.purchases.view',
  reorder: 'catalog.reorder.view', transfers: 'catalog.transfers.view', labels: 'catalog.labels.view',
  customers: 'relationships.customers.view', suppliers: 'relationships.suppliers.view', reports: 'insights.reports.view',
  alerts: 'insights.alerts.view', risk: 'insights.risk.view', audit: 'insights.audit.view', company: 'setup.company.view'
};

type Item = { to: string; label: string; icon: any; permission?: string; legacy?: string };
const groups: Array<{ title: string; items: Item[] }> = [
  { title: 'Command', items: [
    { to: '/dashboard', label: 'Overview', icon: LayoutDashboard },
    { to: '/alerts', label: 'Smart Alerts', icon: Bell, permission: permissions.alerts, legacy: '/alerts' },
    { to: '/risk', label: 'Risk Center', icon: ShieldAlert, permission: permissions.risk, legacy: '/risk' }
  ]},
  { title: 'Sell', items: [
    { to: '/pos', label: 'Point of Sale', icon: ShoppingCart, permission: permissions.pos },
    { to: '/sales', label: 'Sales & Returns', icon: RotateCcw, permission: permissions.sales },
    { to: '/shifts', label: 'Shifts & Cash', icon: WalletCards, permission: permissions.shifts }
  ]},
  { title: 'Stock', items: [
    { to: '/products', label: 'Products', icon: PackageSearch, permission: permissions.products },
    { to: '/inventory', label: 'Inventory', icon: Boxes, permission: permissions.inventory },
    { to: '/purchases', label: 'Purchases', icon: ReceiptText, permission: permissions.purchases, legacy: '/purchases' },
    { to: '/reorder', label: 'Smart Reorder', icon: ClipboardList, permission: permissions.reorder, legacy: '/reorder' },
    { to: '/transfers', label: 'Transfers', icon: Truck, permission: permissions.transfers, legacy: '/transfers' },
    { to: '/labels', label: 'Barcode & Labels', icon: Tags, permission: permissions.labels, legacy: '/labels' }
  ]},
  { title: 'People', items: [
    { to: '/customers', label: 'Customers', icon: Users, permission: permissions.customers, legacy: '/customers' },
    { to: '/suppliers', label: 'Suppliers', icon: Building2, permission: permissions.suppliers, legacy: '/suppliers' }
  ]},
  { title: 'Insights', items: [
    { to: '/reports', label: 'Reports', icon: FileBarChart2, permission: permissions.reports, legacy: '/reports' },
    { to: '/audit', label: 'Audit Trail', icon: History, permission: permissions.audit, legacy: '/audit' }
  ]},
  { title: 'System', items: [
    { to: '/setup', label: 'Administration', icon: Settings, permission: permissions.company, legacy: '/setup/company' }
  ]}
];

const pageNames: Record<string,string> = {
  '/dashboard': 'Business overview', '/pos': 'Point of sale', '/sales': 'Sales & returns',
  '/shifts': 'Shifts & cash', '/products': 'Products', '/inventory': 'Inventory',
  '/purchases': 'Purchases', '/reorder': 'Smart reorder', '/transfers': 'Transfers',
  '/labels': 'Barcode & labels', '/customers': 'Customers', '/suppliers': 'Suppliers',
  '/reports': 'Reports', '/alerts': 'Smart alerts', '/risk': 'Risk center', '/audit': 'Audit trail', '/setup': 'Administration'
};

export default function AppShell() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const user = useAuthStore((s) => s.user)!;
  const setUser = useAuthStore((s) => s.setUser);
  const storeId = useStoreContext((s) => s.storeId);
  const setStoreId = useStoreContext((s) => s.setStoreId);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();

  const can = (permission?: string) => !permission || user.permissions.includes(permission);
  const selectedStore = storeId ?? user.selectedStoreId ?? user.stores[0]?.id ?? null;
  const pageName = pageNames[location.pathname] ?? 'SmartPOS';

  const initials = useMemo(() => {
    const parts = user.displayName.trim().split(/\s+/);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || 'U';
  }, [user.displayName]);

  async function changeStore(next: string) {
    if (!next || next === selectedStore) return;
    await authApi.selectStore(next);
    setStoreId(next);
    await queryClient.invalidateQueries();
  }

  async function logout() {
    try { await authApi.logout(); }
    finally { setUser(null); navigate('/login', { replace: true }); }
  }

  return (
    <div className="app-shell modern-shell">
      {mobileOpen && <button className="drawer-backdrop" onClick={() => setMobileOpen(false)} aria-label="Close menu" />}

      <aside className={`sidebar modern-sidebar ${mobileOpen ? 'sidebar-open' : ''}`}>
        <div className="brand-row modern-brand-row">
          <div className="brand-mark modern-brand-mark"><Zap size={19} fill="currentColor" /></div>
          <div className="brand-copy"><div className="brand-name">SmartPOS</div><div className="brand-subtitle">Retail OS</div></div>
          <button className="sidebar-close" onClick={() => setMobileOpen(false)}><X size={20} /></button>
        </div>

        <div className="sidebar-search"><Search size={16}/><span>Navigation</span><kbd>⌘K</kbd></div>

        <nav className="nav-scroll modern-nav-scroll">
          {groups.map(group => {
            const visible = group.items.filter(x => can(x.permission));
            if (!visible.length) return null;
            return <div className="nav-group" key={group.title}>
              <div className="nav-group-title">{group.title}</div>
              {visible.map(item => {
                const Icon = item.icon;
                const active = location.pathname === item.to;
                if (item.legacy) {
                  return <a key={item.to} href={item.legacy} className={`nav-item modern-nav-item ${active ? 'active' : ''}`} title="This module is still using the existing Blazor screen until its React conversion phase">
                    <span className="nav-icon"><Icon size={18}/></span>
                    <span className="nav-label">{item.label}</span>
                    <ExternalLink className="legacy-arrow" size={13}/>
                  </a>;
                }
                return <NavLink key={item.to} to={item.to} onClick={() => setMobileOpen(false)} className={({isActive}) => `nav-item modern-nav-item ${isActive ? 'active' : ''}`}>
                  <span className="nav-icon"><Icon size={18}/></span><span className="nav-label">{item.label}</span>
                  {(item.to === '/pos' || item.to === '/sales' || item.to === '/shifts') && <span className="live-pill">NEW</span>}
                </NavLink>;
              })}
            </div>;
          })}
        </nav>

        <div className="sidebar-footer-card">
          <div className="footer-status-dot"/>
          <div><strong>React workspace</strong><span>Dashboard + POS + Sales + Shifts converted</span></div>
        </div>
      </aside>

      <div className="main-column modern-main-column">
        <header className="topbar modern-topbar">
          <div className="topbar-left modern-topbar-left">
            <button className="menu-button" onClick={() => setMobileOpen(true)}><Menu size={21}/></button>
            <div className="page-context"><span>Workspace</span><strong>{pageName}</strong></div>
          </div>

          <div className="topbar-actions modern-topbar-actions">
            <div className="store-switcher modern-store-switcher">
              <span className="store-icon"><Store size={16}/></span>
              <div className="store-select-copy"><small>Current store</small><select value={selectedStore ?? ''} onChange={e => changeStore(e.target.value)}>{user.stores.map(s => <option key={s.id} value={s.id}>{s.displayName}</option>)}</select></div>
              <ChevronDown size={15}/>
            </div>
            {location.pathname !== '/pos' && <NavLink className="quick-pos-button" to="/pos"><ShoppingCart size={17}/> <span>Open POS</span></NavLink>}
            <a className="icon-button notification-button modern-icon-button" href="/alerts" title="Open Smart Alerts"><Bell size={19}/><span className="notification-dot"/></a>
            <div className="profile-wrap">
              <button className="profile-button modern-profile-button" onClick={() => setProfileOpen(v => !v)}>
                <span className="avatar modern-avatar">{initials}</span>
                <span className="profile-copy"><strong>{user.displayName}</strong><small>{user.roles[0] ?? 'User'}</small></span>
                <ChevronDown size={15}/>
              </button>
              {profileOpen && <div className="profile-menu modern-profile-menu"><button><UserRound size={16}/> {user.email}</button><button onClick={logout}>Sign out</button></div>}
            </div>
          </div>
        </header>
        <main className="content modern-content"><Outlet/></main>
      </div>
    </div>
  );
}
