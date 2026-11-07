import { useDeferredValue, useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle, Ban, Banknote, CalendarDays, CheckCircle2, ChevronLeft, ChevronRight,
  CircleDollarSign, Clock3, CreditCard, ExternalLink, FileText, PackageCheck, Printer,
  ReceiptText, RefreshCw, RotateCcw, Search, ShieldCheck, ShoppingBasket, Sparkles,
  Store, Undo2, UserRound, WalletCards, X
} from 'lucide-react';
import { salesApi } from '../features/sales/salesApi';
import type {
  ProcessReturnRequest, ReturnReceipt, SaleForReturn, SaleListItem, SalesReceipt
} from '../features/sales/types';
import { useStoreContext } from '../features/shared/storeContext';
import { useAuthStore } from '../features/auth/authStore';

const money = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', maximumFractionDigits: 0 });
const money2 = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', minimumFractionDigits: 2, maximumFractionDigits: 2 });

function inputDate(date: Date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function startUtc(value: string) {
  return value ? new Date(`${value}T00:00:00`).toISOString() : undefined;
}

function endUtc(value: string) {
  if (!value) return undefined;
  const date = new Date(`${value}T00:00:00`);
  date.setDate(date.getDate() + 1);
  return date.toISOString();
}

function localDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' });
}

function localTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
}

function statusClass(status: string) {
  const s = status.toLowerCase();
  if (s === 'completed') return 'completed';
  if (s.includes('returned')) return 'returned';
  if (s.includes('void')) return 'voided';
  return 'neutral';
}

function paymentIcon(method: string) {
  const lower = method.toLowerCase();
  if (lower.includes('cash')) return Banknote;
  if (lower.includes('card')) return CreditCard;
  if (lower.includes('credit')) return WalletCards;
  return CircleDollarSign;
}

type StatusFilter = 'All' | 'Completed' | 'Returned' | 'Voided';
type Toast = { message: string; kind: 'success' | 'error' } | null;
type ReturnLineState = {
  saleLineId: string;
  sku: string;
  productName: string;
  returnable: number;
  quantity: number;
  unitPrice: number;
  discountedUnitPrice: number;
  taxRate: number;
  disposition: 'Restock' | 'Damaged' | 'Discard';
  lineReason: string;
};

export default function SalesPage() {
  const storeId = useStoreContext(s => s.storeId);
  const user = useAuthStore(s => s.user)!;
  const canRefund = user.permissions.includes('sales.refund');
  const canVoid = user.permissions.includes('sales.void');

  const today = useMemo(() => new Date(), []);
  const defaultFrom = useMemo(() => { const d = new Date(); d.setDate(d.getDate() - 29); return d; }, []);
  const [from, setFrom] = useState(inputDate(defaultFrom));
  const [to, setTo] = useState(inputDate(today));
  const [search, setSearch] = useState('');
  const deferredSearch = useDeferredValue(search);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All');
  const [page, setPage] = useState(1);
  const pageSize = 12;

  const [receiptId, setReceiptId] = useState<string | null>(null);
  const [reprintOpen, setReprintOpen] = useState(false);
  const [reprintReason, setReprintReason] = useState('');
  const [reprintBusy, setReprintBusy] = useState(false);

  const [returnId, setReturnId] = useState<string | null>(null);
  const [returnType, setReturnType] = useState<'Refund' | 'Exchange'>('Refund');
  const [refundMethod, setRefundMethod] = useState<'Cash' | 'Card' | 'Other' | 'Credit'>('Cash');
  const [returnReason, setReturnReason] = useState('');
  const [returnReference, setReturnReference] = useState('');
  const [returnNotes, setReturnNotes] = useState('');
  const [returnLines, setReturnLines] = useState<ReturnLineState[]>([]);
  const [returnBusy, setReturnBusy] = useState(false);
  const [returnReceipt, setReturnReceipt] = useState<ReturnReceipt | null>(null);

  const [voidSale, setVoidSale] = useState<SaleListItem | null>(null);
  const [voidReason, setVoidReason] = useState('');
  const [managerEmail, setManagerEmail] = useState('');
  const [managerPassword, setManagerPassword] = useState('');
  const [voidBusy, setVoidBusy] = useState(false);

  const [toast, setToast] = useState<Toast>(null);

  const salesQuery = useQuery({
    queryKey: ['sales', storeId, from, to, deferredSearch],
    queryFn: () => salesApi.list({
      storeId: storeId!,
      fromUtc: startUtc(from),
      toUtc: endUtc(to),
      search: deferredSearch,
      take: 300
    }),
    enabled: !!storeId
  });

  const receiptQuery = useQuery({
    queryKey: ['sales-receipt', receiptId],
    queryFn: () => salesApi.receipt(receiptId!),
    enabled: !!receiptId
  });

  const returnQuery = useQuery({
    queryKey: ['sales-return', returnId],
    queryFn: () => salesApi.returnDetail(returnId!),
    enabled: !!returnId
  });

  useEffect(() => {
    const sale = returnQuery.data;
    if (!sale) return;
    setRefundMethod((['Cash', 'Card', 'Other', 'Credit'].includes(sale.originalPaymentMethod) ? sale.originalPaymentMethod : 'Cash') as typeof refundMethod);
    setReturnLines(sale.lines.map(line => ({
      saleLineId: line.saleLineId,
      sku: line.sku,
      productName: line.productName,
      returnable: line.quantityReturnable,
      quantity: line.quantityReturnable,
      unitPrice: line.unitPrice,
      discountedUnitPrice: line.discountedUnitPrice <= 0 ? line.unitPrice : line.discountedUnitPrice,
      taxRate: line.taxRate,
      disposition: 'Restock',
      lineReason: ''
    })));
    setReturnType('Refund');
    setReturnReason('');
    setReturnReference('');
    setReturnNotes('');
  }, [returnQuery.data]);

  useEffect(() => { setPage(1); }, [statusFilter, deferredSearch, from, to, storeId]);
  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 3600);
    return () => window.clearTimeout(t);
  }, [toast]);

  const rawSales = salesQuery.data?.sales ?? [];
  const filtered = useMemo(() => rawSales.filter(sale => {
    if (statusFilter === 'All') return true;
    if (statusFilter === 'Returned') return sale.status.includes('Returned');
    if (statusFilter === 'Voided') return sale.status === 'Voided';
    return sale.status === statusFilter;
  }), [rawSales, statusFilter]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const pageRows = filtered.slice((page - 1) * pageSize, page * pageSize);

  const trend = useMemo(() => {
    const points: Array<{ label: string; amount: number }> = [];
    for (let i = 6; i >= 0; i--) {
      const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() - i);
      const next = new Date(d); next.setDate(next.getDate() + 1);
      const amount = rawSales.filter(x => {
        const ts = new Date(x.completedOn).getTime();
        return ts >= d.getTime() && ts < next.getTime() && x.status !== 'Voided';
      }).reduce((sum, x) => sum + x.totalAmount, 0);
      points.push({ label: d.toLocaleDateString(undefined, { weekday: 'short' }), amount });
    }
    return points;
  }, [rawSales]);

  const maxTrend = Math.max(1, ...trend.map(x => x.amount));
  const summary = salesQuery.data?.summary ?? { saleCount: 0, grossSales: 0, taxCollected: 0, itemsSold: 0 };
  const averageTicket = summary.saleCount ? summary.grossSales / summary.saleCount : 0;
  const returnCount = rawSales.filter(x => x.status.includes('Returned')).length;
  const voidCount = rawSales.filter(x => x.status === 'Voided').length;

  const receiptListItem = receiptId ? rawSales.find(x => x.id === receiptId) ?? null : null;

  const returnTotal = useMemo(() => returnLines.reduce((total, line) => {
    if (line.quantity <= 0) return total;
    const sub = Math.round(line.quantity * line.discountedUnitPrice * 100) / 100;
    const tax = Math.round(sub * line.taxRate) / 100;
    return total + sub + tax;
  }, 0), [returnLines]);

  function quickRange(days: number) {
    const end = new Date();
    const start = new Date(); start.setDate(start.getDate() - (days - 1));
    setFrom(inputDate(start)); setTo(inputDate(end));
  }

  function openReceipt(id: string) {
    setReceiptId(id);
    setReprintOpen(false);
    setReprintReason('');
  }

  function openReturn(sale: SaleListItem) {
    setReceiptId(null);
    setReturnId(sale.id);
  }

  function openVoid(sale: SaleListItem) {
    setReceiptId(null);
    setVoidSale(sale);
    setVoidReason('');
    setManagerEmail('');
    setManagerPassword('');
  }

  async function processReprint() {
    if (!receiptId || reprintReason.trim().length < 3) return;
    setReprintBusy(true);
    try {
      await salesApi.logReprint(receiptId, reprintReason.trim());
      await receiptQuery.refetch();
      setReprintOpen(false);
      setReprintReason('');
      setToast({ message: 'Reprint reason logged. Opening print dialog…', kind: 'success' });
      window.setTimeout(() => window.print(), 180);
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Receipt reprint failed.', kind: 'error' });
    } finally { setReprintBusy(false); }
  }

  async function processReturn() {
    const sale = returnQuery.data;
    if (!sale) return;
    const selected = returnLines.filter(x => x.quantity > 0);
    if (!selected.length) { setToast({ message: 'Select at least one item to return.', kind: 'error' }); return; }
    if (returnReason.trim().length < 3) { setToast({ message: 'Enter a return reason.', kind: 'error' }); return; }
    if (selected.some(x => x.quantity > x.returnable)) { setToast({ message: 'A return quantity exceeds the available quantity.', kind: 'error' }); return; }
    if (returnType === 'Exchange' && refundMethod === 'Cash') { setToast({ message: 'Exchange settlement cannot use Cash. Choose Card, Other, or Credit.', kind: 'error' }); return; }

    const payload: ProcessReturnRequest = {
      saleId: sale.saleId,
      returnType,
      refundMethod,
      returnReason: returnReason.trim(),
      refundReference: returnType === 'Refund' ? (returnReference.trim() || null) : null,
      exchangeReference: returnType === 'Exchange' ? (returnReference.trim() || null) : null,
      notes: returnNotes.trim() || null,
      lines: selected.map(x => ({
        saleLineId: x.saleLineId,
        quantityReturned: x.quantity,
        disposition: x.disposition,
        lineReason: x.lineReason.trim() || null
      }))
    };

    setReturnBusy(true);
    try {
      const result = await salesApi.processReturn(payload);
      setReturnReceipt({ ...result, returnReason: result.returnReason || payload.returnReason });
      setReturnId(null);
      await salesQuery.refetch();
      setToast({ message: `${result.returnNumber} processed successfully.`, kind: 'success' });
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Return failed.', kind: 'error' });
    } finally { setReturnBusy(false); }
  }

  async function processVoid() {
    if (!voidSale || !storeId) return;
    if (voidReason.trim().length < 3) { setToast({ message: 'Enter a clear void reason.', kind: 'error' }); return; }
    if (!managerEmail.trim() || !managerPassword) { setToast({ message: 'Manager credentials are required.', kind: 'error' }); return; }
    setVoidBusy(true);
    try {
      const approval = await salesApi.managerApproval({
        storeId,
        managerEmail: managerEmail.trim(),
        managerPassword,
        reference: voidSale.saleNumber,
        amount: voidSale.totalAmount,
        reason: voidReason.trim()
      });
      await salesApi.voidSale(voidSale.id, `${voidReason.trim()} | Approved by ${approval.approvedByName}`);
      setVoidSale(null);
      await salesQuery.refetch();
      setToast({ message: `${voidSale.saleNumber} voided and reversed successfully.`, kind: 'success' });
    } catch (e) {
      setToast({ message: e instanceof Error ? e.message : 'Void failed.', kind: 'error' });
    } finally { setVoidBusy(false); }
  }

  return (
    <section className="sales-page premium-sales-page">
      {toast && <div className={`premium-toast ${toast.kind}`}><span>{toast.kind === 'success' ? <CheckCircle2 size={17}/> : <AlertTriangle size={17}/>}</span>{toast.message}</div>}

      <div className="sales-command-hero">
        <div className="sales-hero-orb orb-one"/><div className="sales-hero-orb orb-two"/>
        <div className="sales-hero-copy">
          <span className="sales-kicker"><Sparkles size={13}/> Revenue operations</span>
          <h1>Sales &amp; Returns</h1>
          <p>Every transaction, receipt, refund and controlled reversal in one polished workspace.</p>
          <div className="sales-hero-trust">
            <span><ShieldCheck size={14}/> Permission protected</span>
            <span><RefreshCw size={14}/> Live store data</span>
            <span><Store size={14}/> Store scoped</span>
          </div>
        </div>
        <div className="sales-hero-analytics">
          <div className="hero-analytics-label"><span>7-day pulse</span><strong>{money.format(trend.reduce((s,x) => s + x.amount, 0))}</strong></div>
          <div className="sales-mini-bars">
            {trend.map(point => <div className="mini-bar-column" key={point.label}><div className="mini-bar-track"><span style={{height:`${Math.max(8,(point.amount/maxTrend)*100)}%`}}/></div><small>{point.label}</small></div>)}
          </div>
        </div>
      </div>

      <div className="sales-kpi-grid">
        <article className="sales-kpi sales-kpi-indigo"><div className="sales-kpi-icon"><ReceiptText size={19}/></div><div><span>Transactions</span><strong>{summary.saleCount}</strong><small>{money.format(averageTicket)} avg. ticket</small></div></article>
        <article className="sales-kpi sales-kpi-emerald"><div className="sales-kpi-icon"><CircleDollarSign size={19}/></div><div><span>Gross sales</span><strong>{money.format(summary.grossSales)}</strong><small>Selected period revenue</small></div></article>
        <article className="sales-kpi sales-kpi-cyan"><div className="sales-kpi-icon"><ShoppingBasket size={19}/></div><div><span>Items sold</span><strong>{summary.itemsSold}</strong><small>{money.format(summary.taxCollected)} GST collected</small></div></article>
        <article className="sales-kpi sales-kpi-amber"><div className="sales-kpi-icon"><Undo2 size={19}/></div><div><span>Exceptions</span><strong>{returnCount + voidCount}</strong><small>{returnCount} returned · {voidCount} voided</small></div></article>
      </div>

      <div className="sales-workspace-panel">
        <div className="sales-panel-top">
          <div><span className="panel-eyebrow">Transaction intelligence</span><h2>Sales ledger</h2><p>Search, inspect and action completed transactions without leaving the workspace.</p></div>
          <button className="sales-refresh-button" onClick={() => salesQuery.refetch()} disabled={salesQuery.isFetching}><RefreshCw size={15} className={salesQuery.isFetching ? 'spin' : ''}/> Refresh</button>
        </div>

        <div className="sales-filter-deck">
          <div className="sales-search-box"><Search size={17}/><input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search sale number or customer…"/>{search && <button onClick={()=>setSearch('')}><X size={14}/></button>}</div>
          <div className="sales-date-box"><CalendarDays size={15}/><input type="date" value={from} onChange={e=>setFrom(e.target.value)}/><span>to</span><input type="date" value={to} onChange={e=>setTo(e.target.value)}/></div>
          <div className="sales-quick-ranges"><button onClick={()=>quickRange(1)}>Today</button><button onClick={()=>quickRange(7)}>7D</button><button onClick={()=>quickRange(30)}>30D</button></div>
        </div>

        <div className="sales-status-tabs">
          {(['All','Completed','Returned','Voided'] as StatusFilter[]).map(item => <button key={item} className={statusFilter===item?'active':''} onClick={()=>setStatusFilter(item)}>{item}<span>{item==='All'?rawSales.length:item==='Returned'?returnCount:item==='Voided'?voidCount:rawSales.filter(x=>x.status===item).length}</span></button>)}
        </div>

        {salesQuery.isLoading ? <div className="sales-loading"><RefreshCw size={20} className="spin"/><strong>Loading transaction ledger…</strong></div>
        : salesQuery.isError ? <div className="sales-empty error"><AlertTriangle size={28}/><strong>Sales could not be loaded</strong><span>{salesQuery.error instanceof Error ? salesQuery.error.message : 'Try refreshing the workspace.'}</span></div>
        : pageRows.length === 0 ? <div className="sales-empty"><ReceiptText size={30}/><strong>No transactions found</strong><span>Adjust the date, status or search filters.</span></div>
        : <>
          <div className="sales-table-wrap">
            <table className="premium-sales-table">
              <thead><tr><th>Transaction</th><th>Customer</th><th>Payment</th><th>Cashier / register</th><th>Items</th><th className="amount">Total</th><th>Status</th><th></th></tr></thead>
              <tbody>{pageRows.map(sale => {
                const PayIcon = paymentIcon(sale.paymentMethod);
                return <tr key={sale.id} onDoubleClick={()=>openReceipt(sale.id)}>
                  <td><button className="sale-number-link" onClick={()=>openReceipt(sale.id)}>{sale.saleNumber}</button><span className="sale-date-sub"><Clock3 size={11}/>{localDate(sale.completedOn)} · {localTime(sale.completedOn)}</span></td>
                  <td><div className="sales-person"><span><UserRound size={14}/></span><div><strong>{sale.customerName}</strong><small>{sale.storeName}</small></div></div></td>
                  <td><span className="payment-chip"><PayIcon size={13}/>{sale.paymentMethod}</span></td>
                  <td><strong className="table-primary">{sale.cashierName}</strong><small className="table-secondary">{sale.registerName}</small></td>
                  <td><span className="item-count-chip"><PackageCheck size={13}/>{sale.itemCount}</span></td>
                  <td className="amount"><strong className="sale-total">{money2.format(sale.totalAmount)}</strong></td>
                  <td><span className={`sale-status ${statusClass(sale.status)}`}>{sale.status}</span></td>
                  <td><div className="sales-row-actions"><button title="Receipt" onClick={()=>openReceipt(sale.id)}><ReceiptText size={15}/></button>{canRefund && sale.status!=='FullyReturned' && sale.status!=='Voided' && <button title="Return / exchange" onClick={()=>openReturn(sale)}><RotateCcw size={15}/></button>}{canVoid && sale.status==='Completed' && <button className="danger" title="Void sale" onClick={()=>openVoid(sale)}><Ban size={15}/></button>}</div></td>
                </tr>;
              })}</tbody>
            </table>
          </div>

          <div className="sales-mobile-list">{pageRows.map(sale => <article className="sales-mobile-card" key={sale.id} onClick={()=>openReceipt(sale.id)}><div className="mobile-sale-top"><div><strong>{sale.saleNumber}</strong><span>{localDate(sale.completedOn)} · {localTime(sale.completedOn)}</span></div><span className={`sale-status ${statusClass(sale.status)}`}>{sale.status}</span></div><div className="mobile-sale-customer"><UserRound size={14}/><span>{sale.customerName}</span></div><div className="mobile-sale-bottom"><span>{sale.paymentMethod} · {sale.itemCount} items</span><strong>{money2.format(sale.totalAmount)}</strong></div></article>)}</div>

          <div className="sales-pagination"><span>Showing {(page-1)*pageSize+1}–{Math.min(page*pageSize,filtered.length)} of {filtered.length}</span><div><button disabled={page<=1} onClick={()=>setPage(p=>Math.max(1,p-1))}><ChevronLeft size={15}/></button><strong>{page} / {pageCount}</strong><button disabled={page>=pageCount} onClick={()=>setPage(p=>Math.min(pageCount,p+1))}><ChevronRight size={15}/></button></div></div>
        </>}
      </div>

      {receiptId && <ReceiptDrawer receipt={receiptQuery.data ?? null} loading={receiptQuery.isLoading} error={receiptQuery.isError} onClose={()=>setReceiptId(null)} onReprint={()=>setReprintOpen(true)} onInvoice={()=>window.open(`/invoice/${receiptId}`,'_blank')} canRefund={canRefund && receiptListItem?.status !== 'FullyReturned' && receiptListItem?.status !== 'Voided'} canVoid={canVoid && receiptListItem?.status === 'Completed'} onReturn={()=>{ if(receiptListItem) openReturn(receiptListItem); }} onVoid={()=>{ if(receiptListItem) openVoid(receiptListItem); }}/>}

      {reprintOpen && receiptQuery.data && <div className="premium-modal-backdrop"><div className="premium-dialog reprint-dialog"><div className="premium-dialog-head"><div className="dialog-icon purple"><Printer size={19}/></div><div><span>Controlled reprint</span><h3>Reason for duplicate receipt</h3></div><button onClick={()=>setReprintOpen(false)}><X size={18}/></button></div><p className="dialog-help">Reprints are logged against the sale for audit purposes.</p><label className="premium-field"><span>Reprint reason</span><textarea rows={3} value={reprintReason} onChange={e=>setReprintReason(e.target.value)} placeholder="Example: Customer requested duplicate copy"/></label><div className="modal-actions"><button className="modal-secondary" onClick={()=>setReprintOpen(false)}>Cancel</button><button className="modal-primary" disabled={reprintBusy||reprintReason.trim().length<3} onClick={processReprint}><Printer size={14}/>{reprintBusy?'Logging…':'Log & print'}</button></div></div></div>}

      {returnId && <ReturnDialog sale={returnQuery.data ?? null} loading={returnQuery.isLoading} lines={returnLines} setLines={setReturnLines} returnType={returnType} setReturnType={value=>{setReturnType(value); if(value==='Exchange'&&refundMethod==='Cash')setRefundMethod('Other');}} refundMethod={refundMethod} setRefundMethod={setRefundMethod} reason={returnReason} setReason={setReturnReason} reference={returnReference} setReference={setReturnReference} notes={returnNotes} setNotes={setReturnNotes} total={returnTotal} busy={returnBusy} onCancel={()=>setReturnId(null)} onSubmit={processReturn}/>} 

      {returnReceipt && <ReturnSuccessDialog receipt={returnReceipt} onClose={()=>setReturnReceipt(null)}/>} 

      {voidSale && <VoidDialog sale={voidSale} reason={voidReason} setReason={setVoidReason} email={managerEmail} setEmail={setManagerEmail} password={managerPassword} setPassword={setManagerPassword} busy={voidBusy} onCancel={()=>setVoidSale(null)} onSubmit={processVoid}/>} 
    </section>
  );
}

function ReceiptDrawer({ receipt, loading, error, onClose, onReprint, onInvoice, canRefund, canVoid, onReturn, onVoid }: {
  receipt: SalesReceipt | null; loading: boolean; error: boolean; onClose:()=>void; onReprint:()=>void; onInvoice:()=>void; canRefund:boolean; canVoid:boolean; onReturn:()=>void; onVoid:()=>void;
}) {
  return <div className="sales-drawer-layer"><button className="sales-drawer-scrim" onClick={onClose} aria-label="Close receipt"/><aside className="sales-receipt-drawer">
    <div className="receipt-drawer-head"><div><span>Transaction receipt</span><h2>{receipt?.saleNumber ?? 'Loading…'}</h2></div><button onClick={onClose}><X size={19}/></button></div>
    {loading ? <div className="receipt-drawer-loading"><RefreshCw size={22} className="spin"/> Loading receipt…</div>
    : error || !receipt ? <div className="sales-empty error"><AlertTriangle size={28}/><strong>Receipt unavailable</strong></div>
    : <>
      <div className="receipt-drawer-actions"><button className="receipt-action primary" onClick={onReprint}><Printer size={15}/> Reprint</button><button className="receipt-action" onClick={onInvoice}><FileText size={15}/> A4 invoice <ExternalLink size={12}/></button>{canRefund && <button className="receipt-action" onClick={onReturn}><RotateCcw size={15}/> Return</button>}{canVoid && <button className="receipt-action danger" onClick={onVoid}><Ban size={15}/> Void</button>}</div>
      <div className="sales-print-receipt">
        <div className="receipt-brand-block"><div className="receipt-brand-icon"><ReceiptText size={20}/></div><div><strong>SmartPOS Receipt</strong><span>{receipt.storeName}</span></div></div>
        {receipt.reprintCount>0 && <div className="duplicate-banner">Duplicate history · {receipt.reprintCount} reprint{receipt.reprintCount===1?'':'s'}</div>}
        <div className="receipt-meta-grid"><div><span>Date</span><strong>{localDate(receipt.completedOn)}</strong></div><div><span>Time</span><strong>{localTime(receipt.completedOn)}</strong></div><div><span>Register</span><strong>{receipt.registerName}</strong></div><div><span>Cashier</span><strong>{receipt.cashierName}</strong></div><div className="wide"><span>Customer</span><strong>{receipt.customerName}</strong></div></div>
        <div className="receipt-items"><div className="receipt-item-head"><span>Item</span><span>Qty</span><span>Amount</span></div>{receipt.lines.map((line,index)=><div className="receipt-item" key={`${line.sku}-${index}`}><div><strong>{line.name}</strong><small>{line.sku} · {money2.format(line.unitPrice)} each</small>{line.discountAmount>0&&<small className="receipt-discount">Discount −{money2.format(line.discountAmount)}{line.discountReason?` · ${line.discountReason}`:''}</small>}</div><span>{line.quantity}</span><strong>{money2.format(line.lineTotal)}</strong></div>)}</div>
        <div className="receipt-total-block"><div><span>Subtotal</span><strong>{money2.format(receipt.subtotal)}</strong></div>{receipt.discountAmount>0&&<div className="receipt-discount-row"><span>Invoice discount{receipt.discountReason?` · ${receipt.discountReason}`:''}</span><strong>−{money2.format(receipt.discountAmount)}</strong></div>}<div><span>Tax</span><strong>{money2.format(receipt.taxAmount)}</strong></div><div className="receipt-grand"><span>Total</span><strong>{money2.format(receipt.totalAmount)}</strong></div>{receipt.payments.map((payment,index)=><div key={`${payment.method}-${index}`}><span>{payment.method}{payment.reference?` · ${payment.reference}`:''}</span><strong>{money2.format(payment.amount)}</strong></div>)}{receipt.changeDue>0&&<div><span>Change due</span><strong>{money2.format(receipt.changeDue)}</strong></div>}</div>
        <div className="receipt-thanks"><strong>Thank you</strong><span>Keep this receipt for return, exchange or warranty reference.</span></div>
      </div>
      {receipt.reprintLogs.length>0&&<div className="reprint-history"><div className="reprint-history-head"><ShieldCheck size={14}/><strong>Recent reprint audit</strong></div>{receipt.reprintLogs.map(log=><div className="reprint-log" key={log.id}><div><strong>{log.reprintedByName}</strong><span>{log.reason}</span></div><small>{localDate(log.reprintedOn)} · {localTime(log.reprintedOn)}</small></div>)}</div>}
    </>}
  </aside></div>;
}

function ReturnDialog({ sale, loading, lines, setLines, returnType, setReturnType, refundMethod, setRefundMethod, reason, setReason, reference, setReference, notes, setNotes, total, busy, onCancel, onSubmit }: {
  sale: SaleForReturn|null; loading:boolean; lines:ReturnLineState[]; setLines:(v:ReturnLineState[])=>void; returnType:'Refund'|'Exchange'; setReturnType:(v:'Refund'|'Exchange')=>void; refundMethod:'Cash'|'Card'|'Other'|'Credit'; setRefundMethod:(v:'Cash'|'Card'|'Other'|'Credit')=>void; reason:string; setReason:(v:string)=>void; reference:string; setReference:(v:string)=>void; notes:string; setNotes:(v:string)=>void; total:number; busy:boolean; onCancel:()=>void; onSubmit:()=>void;
}) {
  function patch(id:string, values:Partial<ReturnLineState>){ setLines(lines.map(x=>x.saleLineId===id?{...x,...values}:x)); }
  return <div className="premium-modal-backdrop"><div className="premium-dialog return-dialog"><div className="premium-dialog-head"><div className="dialog-icon green"><RotateCcw size={19}/></div><div><span>Controlled post-sale action</span><h3>{sale?`Return · ${sale.saleNumber}`:'Preparing return…'}</h3></div><button onClick={onCancel}><X size={18}/></button></div>
    {loading||!sale?<div className="dialog-loading"><RefreshCw className="spin" size={19}/> Loading returnable items…</div>:<>
      <div className="return-summary-strip"><div><span>Customer</span><strong>{sale.customerName}</strong></div><div><span>Original payment</span><strong>{sale.originalPaymentMethod}</strong></div><div><span>Sale total</span><strong>{money2.format(sale.totalAmount)}</strong></div><div className="accent"><span>Selected value</span><strong>{money2.format(total)}</strong></div></div>
      <div className="return-mode-grid"><button className={returnType==='Refund'?'active':''} onClick={()=>setReturnType('Refund')}><Banknote size={17}/><span><strong>Refund</strong><small>Return value to customer</small></span></button><button className={returnType==='Exchange'?'active':''} onClick={()=>setReturnType('Exchange')}><RefreshCw size={17}/><span><strong>Exchange</strong><small>Record replacement value</small></span></button></div>
      <div className="return-form-grid"><label className="premium-field"><span>Settlement method</span><select value={refundMethod} onChange={e=>setRefundMethod(e.target.value as typeof refundMethod)}>{['Cash','Card','Other','Credit'].map(x=><option key={x}>{x}</option>)}</select></label><label className="premium-field"><span>{returnType==='Exchange'?'Exchange reference':'Refund reference'}</span><input value={reference} onChange={e=>setReference(e.target.value)} placeholder="Optional reference"/></label></div>
      <label className="premium-field"><span>Return reason *</span><textarea rows={2} value={reason} onChange={e=>setReason(e.target.value)} placeholder="Why is this transaction being returned?"/></label>
      <div className="return-lines-table"><div className="return-lines-head"><span>Item</span><span>Returnable</span><span>Qty</span><span>Disposition</span><span>Line reason</span></div>{lines.map(line=><div className="return-line" key={line.saleLineId}><div><strong>{line.productName}</strong><small>{line.sku}</small></div><span>{line.returnable}</span><input type="number" min="0" max={line.returnable} step="0.001" value={line.quantity} onChange={e=>patch(line.saleLineId,{quantity:Number(e.target.value)})}/><select value={line.disposition} onChange={e=>patch(line.saleLineId,{disposition:e.target.value as ReturnLineState['disposition']})}><option>Restock</option><option>Damaged</option><option>Discard</option></select><input value={line.lineReason} onChange={e=>patch(line.saleLineId,{lineReason:e.target.value})} placeholder="Optional"/></div>)}</div>
      <label className="premium-field"><span>Notes</span><textarea rows={2} value={notes} onChange={e=>setNotes(e.target.value)} placeholder="Optional internal notes"/></label>
      <div className="return-impact-card"><ShieldCheck size={18}/><div><strong>Inventory-safe return</strong><span>Restock adds sellable stock back. Damaged and Discard remain outside sellable stock.</span></div><strong>{money2.format(total)}</strong></div>
      <div className="modal-actions"><button className="modal-secondary" onClick={onCancel}>Cancel</button><button className="modal-primary" disabled={busy||total<=0||reason.trim().length<3} onClick={onSubmit}><RotateCcw size={14}/>{busy?'Processing…':`Process ${returnType.toLowerCase()}`}</button></div>
    </>}
  </div></div>;
}

function ReturnSuccessDialog({receipt,onClose}:{receipt:ReturnReceipt;onClose:()=>void}){
  return <div className="premium-modal-backdrop"><div className="premium-dialog return-success-dialog"><div className="return-success-icon"><CheckCircle2 size={28}/></div><div className="return-success-copy"><span>POST-SALE ACTION COMPLETE</span><h3>{receipt.returnNumber}</h3><p>{receipt.returnType} against {receipt.originalSaleNumber}</p></div><div className="return-success-total"><span>{receipt.returnType==='Exchange'?'Exchange value':'Refund total'}</span><strong>{money2.format(receipt.totalAmount)}</strong></div><div className="return-success-lines">{receipt.lines.map((line,index)=><div key={`${line.sku}-${index}`}><div><strong>{line.productName}</strong><span>{line.sku} · {line.disposition}</span></div><span>{line.quantityReturned} ×</span><strong>{money2.format(line.lineTotal)}</strong></div>)}</div><button className="modal-primary full" onClick={onClose}>Done</button></div></div>;
}

function VoidDialog({sale,reason,setReason,email,setEmail,password,setPassword,busy,onCancel,onSubmit}:{sale:SaleListItem;reason:string;setReason:(v:string)=>void;email:string;setEmail:(v:string)=>void;password:string;setPassword:(v:string)=>void;busy:boolean;onCancel:()=>void;onSubmit:()=>void}){
  return <div className="premium-modal-backdrop"><div className="premium-dialog void-dialog"><div className="premium-dialog-head"><div className="dialog-icon red"><Ban size={19}/></div><div><span>High-control action</span><h3>Void {sale.saleNumber}</h3></div><button onClick={onCancel}><X size={18}/></button></div><div className="void-warning"><AlertTriangle size={18}/><div><strong>This reverses the completed sale</strong><span>Stock is restored, cash movement is reversed, and customer credit is corrected where applicable.</span></div></div><div className="void-sale-summary"><div><span>Customer</span><strong>{sale.customerName}</strong></div><div><span>Total</span><strong>{money2.format(sale.totalAmount)}</strong></div><div><span>Payment</span><strong>{sale.paymentMethod}</strong></div></div><label className="premium-field"><span>Void reason *</span><textarea rows={3} value={reason} onChange={e=>setReason(e.target.value)} placeholder="Example: Duplicate invoice, wrong items scanned…"/></label><div className="manager-approval-section"><div className="manager-section-title"><ShieldCheck size={16}/><div><strong>Manager approval required</strong><span>Enter credentials for a manager authorized to approve sale voids.</span></div></div><div className="return-form-grid"><label className="premium-field"><span>Manager email</span><input type="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="manager@company.com"/></label><label className="premium-field"><span>Manager password</span><input type="password" value={password} onChange={e=>setPassword(e.target.value)} placeholder="••••••••"/></label></div></div><div className="modal-actions"><button className="modal-secondary" onClick={onCancel}>Keep sale</button><button className="modal-primary danger" disabled={busy||reason.trim().length<3||!email.trim()||!password} onClick={onSubmit}><Ban size={14}/>{busy?'Approving…':'Approve & void sale'}</button></div></div></div>;
}
