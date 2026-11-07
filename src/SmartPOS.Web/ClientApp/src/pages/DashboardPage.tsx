import { AlertTriangle, ArrowUpRight, Boxes, CreditCard, PackageCheck, ShoppingBag, TrendingUp, WalletCards, Zap } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '../features/dashboard/dashboardApi';
import { useStoreContext } from '../features/shared/storeContext';

const money = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', maximumFractionDigits: 0 });

export default function DashboardPage() {
  const storeId = useStoreContext(s => s.storeId);
  const dashboard = useQuery({ queryKey: ['dashboard', storeId], queryFn: () => dashboardApi.get(storeId) });

  if (dashboard.isLoading) return <div className="dashboard-loading"><div className="loading-orb"/><strong>Building your retail snapshot…</strong></div>;
  if (dashboard.isError) return <div className="empty-state modern-empty-state"><AlertTriangle size={34}/><h2>Dashboard could not load</h2><p>{dashboard.error instanceof Error ? dashboard.error.message : 'Unknown error'}</p></div>;

  const data = dashboard.data!;
  const s = data.summary;
  const maxTrend = Math.max(...s.activityTrend.map(x => x.amount), 1);
  const lowStockPercent = Math.min(100, s.activeProductCount ? (s.lowStockCount / s.activeProductCount) * 100 : 0);

  return <section className="modern-dashboard">
    <div className="dashboard-hero">
      <div className="hero-glow hero-glow-one"/><div className="hero-glow hero-glow-two"/>
      <div className="hero-copy">
        <span className="hero-kicker"><Zap size={14} fill="currentColor"/> LIVE BUSINESS PULSE</span>
        <h1>Your store, at a glance.</h1>
        <p>Sales, margin, inventory and operational attention points — refreshed from the same SmartPOS backend.</p>
        <div className="hero-actions"><a href="/react/pos" className="hero-primary"><ShoppingBag size={17}/> Start a sale</a><a href="/reports" className="hero-secondary">View reports <ArrowUpRight size={15}/></a></div>
      </div>
      <div className="hero-highlight">
        <span>Today’s sales</span><strong>{money.format(s.todaySales)}</strong>
        <div className="hero-highlight-row"><span>{s.todayTransactions} transactions</span><span>{money.format(s.averageBasket)} avg. basket</span></div>
      </div>
    </div>

    <div className="modern-kpi-grid">
      <article className="modern-kpi-card sales-card"><div className="kpi-top"><span className="modern-kpi-icon"><TrendingUp size={19}/></span><span className="kpi-badge positive">Today</span></div><span className="modern-kpi-label">Net sales</span><strong className="modern-kpi-value">{money.format(s.todaySales)}</strong><span className="modern-kpi-caption">{s.todayTransactions} completed transactions</span></article>
      <article className="modern-kpi-card profit-card"><div className="kpi-top"><span className="modern-kpi-icon"><WalletCards size={19}/></span><span className="kpi-badge">{s.todayMarginPercent.toFixed(1)}%</span></div><span className="modern-kpi-label">Gross profit</span><strong className="modern-kpi-value">{money.format(s.todayProfit)}</strong><span className="modern-kpi-caption">Gross margin today</span></article>
      <article className="modern-kpi-card stock-card"><div className="kpi-top"><span className="modern-kpi-icon"><Boxes size={19}/></span><span className={`kpi-badge ${s.lowStockCount ? 'warning':''}`}>{s.lowStockCount} low</span></div><span className="modern-kpi-label">Inventory value</span><strong className="modern-kpi-value">{money.format(s.inventoryRetailValue)}</strong><span className="modern-kpi-caption">{s.activeProductCount} active products</span></article>
      <article className="modern-kpi-card alert-card"><div className="kpi-top"><span className="modern-kpi-icon"><AlertTriangle size={19}/></span><span className={`kpi-badge ${data.alertSummary.critical ? 'danger':''}`}>{data.alertSummary.critical} critical</span></div><span className="modern-kpi-label">Smart alerts</span><strong className="modern-kpi-value">{data.alertSummary.total}</strong><span className="modern-kpi-caption">{data.alertSummary.warning} warnings need review</span></article>
    </div>

    <div className="dashboard-main-grid">
      <article className="modern-panel trend-panel">
        <div className="modern-panel-heading"><div><span className="panel-kicker">Performance</span><h2>7-day sales rhythm</h2><p>Daily completed sales value</p></div><span className="panel-icon"><TrendingUp size={18}/></span></div>
        <div className="modern-bar-chart">{s.activityTrend.map(item => <div className="modern-bar-item" key={item.label}><div className="modern-bar-value">{item.amount > 0 ? money.format(item.amount) : '—'}</div><div className="modern-bar-track"><div className="modern-bar-fill" style={{height:`${Math.max(item.amount / maxTrend * 100, 4)}%`}}/></div><span>{item.label}</span></div>)}</div>
      </article>

      <article className="modern-panel health-panel">
        <div className="modern-panel-heading"><div><span className="panel-kicker">Operations</span><h2>Store health</h2><p>What needs attention now</p></div><span className="panel-icon"><PackageCheck size={18}/></span></div>
        <div className="health-score"><div className="health-ring"><span>{Math.max(0,100-Math.round(lowStockPercent))}</span><small>health</small></div><div className="health-copy"><strong>Inventory readiness</strong><span>{s.lowStockCount ? `${s.lowStockCount} products are at or below reorder level.` : 'Stock levels look healthy.'}</span></div></div>
        <div className="health-stat-grid"><div><span>Active products</span><strong>{s.activeProductCount}</strong></div><div><span>Low stock</span><strong>{s.lowStockCount}</strong></div><div><span>Critical alerts</span><strong>{data.alertSummary.critical}</strong></div><div><span>Warnings</span><strong>{data.alertSummary.warning}</strong></div></div>
      </article>
    </div>

    <div className="dashboard-secondary-grid">
      <article className="modern-panel payment-panel"><div className="modern-panel-heading"><div><span className="panel-kicker">Payments</span><h2>Payment mix</h2></div><span className="panel-icon"><CreditCard size={18}/></span></div><div className="payment-mix-list">{s.paymentMix.length ? s.paymentMix.map((x,index) => <div className="payment-mix-item" key={x.method}><div className="payment-name"><span className={`payment-dot dot-${index%4}`}/><strong>{x.method}</strong></div><div className="payment-progress"><span style={{width:`${Math.max(4,x.sharePercent)}%`}}/></div><div className="payment-amount"><strong>{money.format(x.amount)}</strong><span>{x.sharePercent.toFixed(0)}%</span></div></div>) : <p className="muted-copy">No payment activity yet.</p>}</div></article>

      <article className="modern-panel alerts-panel"><div className="modern-panel-heading"><div><span className="panel-kicker">Attention</span><h2>Priority alerts</h2></div><a href="/alerts" className="panel-link">View all <ArrowUpRight size={14}/></a></div><div className="modern-alert-list">{data.topAlerts.length ? data.topAlerts.slice(0,4).map(a => <a href={a.actionUrl ?? '/alerts'} className="modern-alert-item" key={a.alertKey}><span className={`alert-severity-icon ${a.severity.toLowerCase()}`}><AlertTriangle size={15}/></span><div><strong>{a.title}</strong><p>{a.message}</p><small>{a.storeName}</small></div><ArrowUpRight size={14}/></a>) : <div className="all-clear modern-all-clear"><PackageCheck size={24}/><strong>All clear</strong><span>No active alerts right now.</span></div>}</div></article>
    </div>

    <article className="modern-panel store-performance-panel"><div className="modern-panel-heading"><div><span className="panel-kicker">Branches</span><h2>Store performance</h2><p>Compare sales and profitability across outlets</p></div></div><div className="modern-store-table"><div className="modern-store-row table-header"><span>Store</span><span>Net sales</span><span>Gross profit</span><span>Transactions</span></div>{s.storePerformance.map(x => <div className="modern-store-row" key={x.storeId}><span className="store-name-cell"><span className="store-bullet"/><strong>{x.storeName}</strong></span><span>{money.format(x.netSales)}</span><span>{money.format(x.grossProfit)}</span><span>{x.transactions}</span></div>)}</div></article>
  </section>;
}
