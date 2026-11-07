import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle, BadgePercent, Banknote, Check, CircleDollarSign, CreditCard,
  KeyRound, Minus, Monitor, Pause, Plus, PlusCircle, RotateCcw, Search,
  ShieldCheck, ShoppingCart, Star, Trash2, X
} from 'lucide-react';
import { posApi } from '../features/pos/posApi';
import type { CartLine, HeldSaleListItem, PaymentLine, PosProduct, Receipt } from '../features/pos/types';
import { useStoreContext } from '../features/shared/storeContext';

const money = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', maximumFractionDigits: 2 });

function makeLine(p: PosProduct): CartLine {
  return {
    productId: p.productId, variantId: p.variantId, sku: p.sku, name: p.name,
    quantity: 1, unitPrice: p.unitPrice, discountAmount: 0, discountReason: null, taxRate: p.taxRate
  };
}

type DiscountTarget = { kind: 'invoice' } | { kind: 'line'; index: number };
type DiscountEditor = {
  target: DiscountTarget;
  mode: 'amount' | 'percent';
  amount: string;
  percent: string;
  reason: string;
  maxAmount: number;
};
type PendingApproval = { target: DiscountTarget; amount: number; reason: string; reference: string };

function newPayment(method: PaymentLine['method'], amount: number): PaymentLine {
  return { id: crypto.randomUUID(), method, amount: Math.max(0, Number(amount.toFixed(2))), reference: '' };
}

export default function PosPage() {
  const storeId = useStoreContext(s => s.storeId);
  const queryClient = useQueryClient();
  const scanRef = useRef<HTMLInputElement>(null);
  const [cart, setCart] = useState<CartLine[]>([]);
  const [search, setSearch] = useState('');
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [brandId, setBrandId] = useState<string | null>(null);
  const [customerId, setCustomerId] = useState<string>('');
  const [busyMessage, setBusyMessage] = useState('');
  const [error, setError] = useState('');
  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [cashOpen, setCashOpen] = useState(false);
  const [cashTendered, setCashTendered] = useState('');
  const [heldOpen, setHeldOpen] = useState(false);
  const [heldSales, setHeldSales] = useState<HeldSaleListItem[]>([]);
  const [activeHeldSaleId, setActiveHeldSaleId] = useState<string | null>(null);
  const [activeHeldNumber, setActiveHeldNumber] = useState<string | null>(null);
  const [invoiceDiscountAmount, setInvoiceDiscountAmount] = useState(0);
  const [invoiceDiscountReason, setInvoiceDiscountReason] = useState<string | null>(null);
  const [discountEditor, setDiscountEditor] = useState<DiscountEditor | null>(null);
  const [discountValidation, setDiscountValidation] = useState('');
  const [approvalPending, setApprovalPending] = useState<PendingApproval | null>(null);
  const [managerEmail, setManagerEmail] = useState('');
  const [managerPassword, setManagerPassword] = useState('');
  const [approvalReason, setApprovalReason] = useState('');
  const [approvalError, setApprovalError] = useState('');
  const [approvalBusy, setApprovalBusy] = useState(false);
  const [splitOpen, setSplitOpen] = useState(false);
  const [splitLines, setSplitLines] = useState<PaymentLine[]>([]);
  const [splitError, setSplitError] = useState('');

  const bootstrap = useQuery({
    queryKey: ['pos-bootstrap', storeId],
    queryFn: () => posApi.bootstrap(storeId),
    enabled: !!storeId
  });

  const productsQuery = useQuery({
    queryKey: ['pos-products', storeId, categoryId, brandId, search],
    queryFn: () => posApi.products(storeId!, categoryId, brandId, search),
    enabled: !!storeId && !!bootstrap.data?.context,
    initialData: bootstrap.data?.products,
    placeholderData: previous => previous
  });

  useEffect(() => { scanRef.current?.focus(); }, [bootstrap.data?.context?.registerId]);

  const context = bootstrap.data?.context;
  const customers = bootstrap.data?.customers ?? [];
  const selectedCustomer = customers.find(x => x.id === customerId) ?? null;
  const customerName = selectedCustomer?.name ?? 'Walk-In Customer';
  const products = productsQuery.data ?? bootstrap.data?.products ?? [];

  const totals = useMemo(() => {
    const gross = cart.reduce((sum,l) => sum + l.quantity * l.unitPrice, 0);
    const lineDiscount = cart.reduce((sum,l) => sum + Math.min(Math.max(0,l.discountAmount), l.quantity*l.unitPrice), 0);
    const subtotal = gross - lineDiscount;
    const tax = cart.reduce((sum,l) => {
      const lineGross = l.quantity*l.unitPrice;
      const disc = Math.min(Math.max(0,l.discountAmount), lineGross);
      return sum + (lineGross-disc) * l.taxRate / 100;
    }, 0);
    const invoiceBase = Math.max(0, subtotal + tax);
    const invoiceDiscount = Math.min(Math.max(0, invoiceDiscountAmount), invoiceBase);
    const total = Math.max(0, invoiceBase - invoiceDiscount);
    return { gross, lineDiscount, subtotal, tax, invoiceBase, invoiceDiscount, total, totalDiscount: lineDiscount + invoiceDiscount };
  }, [cart, invoiceDiscountAmount]);

  useEffect(() => {
    if (invoiceDiscountAmount > totals.invoiceBase) {
      setInvoiceDiscountAmount(totals.invoiceBase);
    }
  }, [invoiceDiscountAmount, totals.invoiceBase]);

  function resetSaleState() {
    setCart([]);
    setCustomerId('');
    setInvoiceDiscountAmount(0);
    setInvoiceDiscountReason(null);
    setActiveHeldSaleId(null);
    setActiveHeldNumber(null);
  }

  function addProduct(product: PosProduct) {
    if (product.isOutOfStock) return;
    setCart(current => {
      const i = current.findIndex(x => x.productId === product.productId && x.variantId === product.variantId);
      if (i < 0) return [...current, makeLine(product)];
      const next = [...current];
      next[i] = { ...next[i], quantity: next[i].quantity + 1 };
      return next;
    });
  }

  function changeQty(index: number, delta: number) {
    setCart(current => {
      const next = [...current];
      const qty = next[index].quantity + delta;
      if (qty <= 0) return next.filter((_,i) => i !== index);
      const maxDiscount = qty * next[index].unitPrice;
      next[index] = {
        ...next[index],
        quantity: qty,
        discountAmount: Math.min(next[index].discountAmount, maxDiscount),
        discountReason: next[index].discountAmount > 0 ? next[index].discountReason : null
      };
      return next;
    });
  }

  async function scan(code: string) {
    const value = code.trim();
    if (!value || !storeId) return;
    setError('');
    try {
      const product = await posApi.barcode(storeId, value);
      addProduct(product);
    } catch {
      setError(`No product found for barcode/SKU "${value}".`);
    } finally {
      if (scanRef.current) scanRef.current.value = '';
      scanRef.current?.focus();
    }
  }

  async function checkout(method: string, amountTendered = 0, payments: PaymentLine[] = []) {
    if (!context || cart.length === 0) return;
    if (method === 'Credit' && selectedCustomer && selectedCustomer.creditLimit > 0 && selectedCustomer.balance + totals.total > selectedCustomer.creditLimit) {
      setError(`Credit limit exceeded. Available udhaar is ${money.format(selectedCustomer.availableCredit)}.`);
      return;
    }
    setBusyMessage('Completing sale…'); setError('');
    try {
      const result = await posApi.checkout({
        storeId: context.storeId,
        registerId: context.registerId,
        customerId: selectedCustomer?.id ?? null,
        customerName,
        paymentMethod: method,
        amountTendered,
        invoiceDiscountAmount: totals.invoiceDiscount,
        discountReason: invoiceDiscountReason,
        heldSaleId: activeHeldSaleId,
        lines: cart,
        payments: payments.map(p => ({ method: p.method, amount: p.amount, reference: p.reference.trim() || null }))
      });
      setReceipt(result);
      resetSaleState();
      setCashOpen(false); setCashTendered(''); setSplitOpen(false); setSplitLines([]);
      await queryClient.invalidateQueries({queryKey:['pos-products']});
      await queryClient.invalidateQueries({queryKey:['dashboard']});
      await queryClient.invalidateQueries({queryKey:['shifts']});
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Sale could not be completed.');
    } finally { setBusyMessage(''); }
  }

  async function holdSale() {
    if (!context || cart.length === 0) return;
    setBusyMessage('Holding sale…'); setError('');
    try {
      await posApi.hold({
        storeId: context.storeId, registerId: context.registerId,
        customerId: selectedCustomer?.id ?? null, customerName,
        invoiceDiscountAmount: totals.invoiceDiscount, discountReason: invoiceDiscountReason, lines: cart
      });
      resetSaleState();
    } catch (e) { setError(e instanceof Error ? e.message : 'Could not hold sale.'); }
    finally { setBusyMessage(''); }
  }

  async function openHeldSales() {
    if (!context) return;
    setBusyMessage('Loading held sales…');
    try { setHeldSales(await posApi.held(context.storeId, context.registerId)); setHeldOpen(true); }
    catch(e) { setError(e instanceof Error ? e.message : 'Could not load held sales.'); }
    finally { setBusyMessage(''); }
  }

  async function retrieveHeld(id: string) {
    setBusyMessage('Retrieving sale…');
    try {
      const held = await posApi.heldSale(id);
      setCart(held.lines);
      setCustomerId(held.customerId ?? '');
      setActiveHeldSaleId(held.id);
      setActiveHeldNumber(held.heldNumber);
      setInvoiceDiscountAmount(held.invoiceDiscountAmount ?? 0);
      setInvoiceDiscountReason(held.discountReason ?? null);
      setHeldOpen(false);
    } catch(e) { setError(e instanceof Error ? e.message : 'Could not retrieve held sale.'); }
    finally { setBusyMessage(''); }
  }

  async function cancelHeld(id: string) {
    setBusyMessage('Cancelling held sale…');
    try {
      await posApi.cancelHeld(id);
      if (context) setHeldSales(await posApi.held(context.storeId, context.registerId));
    } catch(e) { setError(e instanceof Error ? e.message : 'Could not cancel held sale.'); }
    finally { setBusyMessage(''); }
  }

  async function toggleFavorite(product: PosProduct) {
    await posApi.favorite(product.productId, !product.isFavorite);
    await queryClient.invalidateQueries({ queryKey: ['pos-products'] });
    await queryClient.invalidateQueries({ queryKey: ['pos-bootstrap'] });
  }

  function openDiscount(target: DiscountTarget) {
    const line = target.kind === 'line' ? cart[target.index] : null;
    const maxAmount = line ? line.quantity * line.unitPrice : totals.invoiceBase;
    const existing = line ? line.discountAmount : invoiceDiscountAmount;
    const reason = line ? line.discountReason ?? '' : invoiceDiscountReason ?? '';
    setDiscountEditor({
      target,
      mode:'amount',
      amount: existing.toFixed(2),
      percent: maxAmount > 0 ? ((existing/maxAmount)*100).toFixed(2) : '0',
      reason,
      maxAmount
    });
    setDiscountValidation('');
  }

  function discountAmount(editor: DiscountEditor) {
    const raw = editor.mode === 'percent'
      ? editor.maxAmount * Number(editor.percent || 0) / 100
      : Number(editor.amount || 0);
    return Math.round(Math.min(Math.max(0, raw), editor.maxAmount) * 100) / 100;
  }

  function requestDiscountApproval() {
    if (!discountEditor) return;
    const amount = discountAmount(discountEditor);
    if (amount <= 0) {
      applyApprovedDiscount(discountEditor.target, 0, null);
      setDiscountEditor(null);
      return;
    }
    if (discountEditor.reason.trim().length < 3) {
      setDiscountValidation('Enter a reason for this discount.');
      return;
    }
    const reference = discountEditor.target.kind === 'line'
      ? cart[discountEditor.target.index]?.sku ?? 'Line discount'
      : 'Invoice discount';
    setApprovalPending({ target: discountEditor.target, amount, reason: discountEditor.reason.trim(), reference });
    setApprovalReason(discountEditor.reason.trim());
    setManagerEmail(''); setManagerPassword(''); setApprovalError('');
  }

  function applyApprovedDiscount(target: DiscountTarget, amount: number, reason: string | null) {
    if (target.kind === 'invoice') {
      setInvoiceDiscountAmount(amount);
      setInvoiceDiscountReason(amount > 0 ? reason : null);
      return;
    }
    setCart(current => current.map((line,index) => index === target.index
      ? { ...line, discountAmount: amount, discountReason: amount > 0 ? reason : null }
      : line));
  }

  async function approveDiscount() {
    if (!approvalPending || !context) return;
    if (!managerEmail.trim() || !managerPassword || approvalReason.trim().length < 3) {
      setApprovalError('Manager email, password and approval reason are required.');
      return;
    }
    setApprovalBusy(true); setApprovalError('');
    try {
      const approved = await posApi.managerApproval({
        storeId: context.storeId,
        approvalType: 'Discount',
        managerEmail: managerEmail.trim(),
        managerPassword,
        reference: approvalPending.reference,
        amount: approvalPending.amount,
        reason: approvalReason.trim()
      });
      const reason = `${approvalPending.reason} | Approved by ${approved.approvedByName}`;
      applyApprovedDiscount(approvalPending.target, approvalPending.amount, reason);
      setApprovalPending(null);
      setDiscountEditor(null);
    } catch (e) {
      setApprovalError(e instanceof Error ? e.message : 'Manager approval failed.');
    } finally { setApprovalBusy(false); }
  }

  function openSplit() {
    setSplitLines([newPayment('Cash', totals.total)]);
    setSplitError('');
    setSplitOpen(true);
  }

  function addSplitLine() {
    const paid = splitLines.reduce((sum,l)=>sum+l.amount,0);
    const remaining = Math.max(0, totals.total-paid);
    if (remaining <= 0.005) { setSplitError('Reduce an existing line before adding another payment method.'); return; }
    const method: PaymentLine['method'] = splitLines.some(x=>x.method==='Card') ? 'Cash' : 'Card';
    setSplitLines(lines => [...lines, newPayment(method, remaining)]);
    setSplitError('');
  }

  const splitPaid = splitLines.reduce((sum,l)=>sum+(Number(l.amount)||0),0);
  const splitRemaining = Math.round((totals.total-splitPaid)*100)/100;
  const splitHasCredit = splitLines.some(l=>l.method==='Credit' && l.amount>0);
  const splitCreditAmount = splitLines.filter(l=>l.method==='Credit').reduce((s,l)=>s+l.amount,0);
  const splitCanComplete = splitLines.length>0 && splitLines.every(l=>l.amount>0) && Math.abs(splitRemaining)<0.005 && (!splitHasCredit || !!selectedCustomer);

  async function completeSplit() {
    if (!splitCanComplete) return;
    if (splitHasCredit && selectedCustomer && selectedCustomer.creditLimit > 0 && selectedCustomer.balance + splitCreditAmount > selectedCustomer.creditLimit) {
      setSplitError(`Credit limit exceeded. Available udhaar is ${money.format(selectedCustomer.availableCredit)}.`);
      return;
    }
    await checkout('Split',0,splitLines);
  }

  if (!storeId) return <div className="empty-state"><AlertTriangle size={34}/><h2>No store selected</h2><p>Select a store from the header.</p></div>;
  if (bootstrap.isLoading) return <div className="skeleton-page">Opening register…</div>;
  if (bootstrap.isError) return <div className="empty-state"><AlertTriangle size={34}/><h2>POS could not load</h2><p>{bootstrap.error instanceof Error ? bootstrap.error.message : 'Unknown error'}</p></div>;
  if (!context) return <div className="empty-state"><AlertTriangle size={34}/><h2>No active register</h2><p>Add an active register for this store in Setup.</p><a className="secondary-button link-button" href="/setup/registers">Open Registers</a></div>;
  if (context.requiresShift && !context.hasOpenShift) return <div className="empty-state"><Banknote size={34}/><h2>Open a shift before billing</h2><p>This register has a cash drawer and requires an active shift.</p><a className="primary-button link-button" href="/react/shifts">Open Shifts & Cash</a></div>;

  return (
    <section className="pos-page modern-pos-page">
      <div className="pos-command-header">
        <div><span className="pos-kicker">CHECKOUT WORKSPACE</span><h1>Point of Sale</h1></div>
        <div className="pos-command-meta"><span className="online-indicator"><i/> Register online</span><span>{context.storeName}</span><strong>{context.registerName}</strong></div>
      </div>
      <div className="pos-status-strip modern-pos-status-strip">
        <span><strong>{context.storeName}</strong> · {context.registerName}</span>
        {context.hasOpenShift && <span>Shift <strong>{context.shiftNumber}</strong>{context.shiftCashBalance != null && <> · Drawer <strong>{money.format(context.shiftCashBalance)}</strong></>}</span>}
        {activeHeldNumber && <span className="held-indicator">Retrieved {activeHeldNumber}</span>}
        {totals.totalDiscount > 0 && <span className="discount-active-pill"><BadgePercent size={12}/> Savings {money.format(totals.totalDiscount)}</span>}
      </div>

      {error && <div className="inline-error"><span>{error}</span><button onClick={() => setError('')}><X size={16}/></button></div>}
      {busyMessage && <div className="busy-bar">{busyMessage}</div>}

      <div className="pos-toolbar modern-pos-toolbar">
        <div className="scan-box modern-scan-box">
          <ShoppingCart size={19}/>
          <input ref={scanRef} placeholder="Scan barcode, SKU, or type product code…" onKeyDown={e => e.key === 'Enter' && scan((e.target as HTMLInputElement).value)}/>
          <button onClick={() => scan(scanRef.current?.value ?? '')}><Plus size={18}/></button>
        </div>
        <button className="secondary-button" onClick={holdSale} disabled={!cart.length}><Pause size={18}/> Hold</button>
        <button className="secondary-button" onClick={openHeldSales}><RotateCcw size={18}/> Held</button>
        <button className="secondary-button" onClick={() => window.open(`/customer-display/${context.registerId}`, '_blank')}><Monitor size={18}/> Display</button>
      </div>

      <div className="pos-layout modern-pos-layout">
        <section className="product-pane modern-product-pane">
          <div className="filter-toolbar">
            <div className="product-search"><Search size={18}/><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Filter products…"/></div>
            <select value={brandId ?? ''} onChange={e => setBrandId(e.target.value || null)}><option value="">All brands</option>{bootstrap.data!.filters.brands.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select>
          </div>
          <div className="category-strip">
            <button className={`category-chip ${categoryId === null ? 'active':''}`} onClick={() => setCategoryId(null)}>All</button>
            {bootstrap.data!.filters.categories.map(x => <button key={x.id} className={`category-chip ${categoryId === x.id ? 'active':''}`} onClick={() => setCategoryId(x.id)}>{x.name}</button>)}
          </div>
          <div className="product-grid modern-product-grid">
            {products.map(product => <div className={`product-card modern-product-card ${product.isOutOfStock ? 'disabled':''}`} key={`${product.productId}-${product.variantId ?? ''}`}>
              <button className={`favorite-toggle ${product.isFavorite ? 'active':''}`} onClick={() => toggleFavorite(product)} title="Quick button"><Star size={15} fill={product.isFavorite ? 'currentColor':'none'}/></button>
              <button className="product-card-main" onClick={() => addProduct(product)} disabled={product.isOutOfStock}>
                <div className="product-image">{product.imageUrl ? <img src={product.imageUrl} alt=""/> : <span>{product.name.slice(0,2).toUpperCase()}</span>}</div>
                <strong>{product.name}</strong><small>{product.sku}</small>{product.isOutOfStock ? <span className="stock-badge out">Out of stock</span> : product.stockOnHand <= 5 ? <span className="stock-badge low">Low stock</span> : <span className="stock-badge good">In stock</span>}
                <div className="product-meta"><span>{money.format(product.unitPrice)}</span><em>{product.stockOnHand} {product.unitAbbreviation ?? 'in stock'}</em></div>
              </button>
            </div>)}
          </div>
        </section>

        <aside className="cart-pane modern-cart-pane">
          <div className="cart-heading modern-cart-heading">
            <div className="customer-field"><span>Customer</span><select value={customerId} onChange={e => setCustomerId(e.target.value)}><option value="">Walk-In Customer</option>{customers.map(c => <option key={c.id} value={c.id}>{c.display}</option>)}</select></div>
            <span className="cart-count">{cart.reduce((s,x)=>s+x.quantity,0)}</span>
          </div>
          {selectedCustomer && <div className="credit-strip">Balance {money.format(selectedCustomer.balance)} · Available udhaar {money.format(selectedCustomer.availableCredit)}</div>}
          <div className="cart-lines">
            {!cart.length && <div className="cart-empty"><ShoppingCart size={32}/><strong>Cart is empty</strong><span>Scan or tap a product to begin.</span></div>}
            {cart.map((line,index) => <div className={`cart-line ${line.discountAmount>0?'discounted':''}`} key={`${line.productId}-${line.variantId ?? ''}`}>
              <div className="cart-line-top"><div><strong>{line.name}</strong><span>{line.sku} · {money.format(line.unitPrice)} each</span>{line.discountAmount>0 && <small className="line-discount-note">Discount -{money.format(line.discountAmount)}</small>}</div><button className="ghost-danger" onClick={() => setCart(c => c.filter((_,i)=>i!==index))}><Trash2 size={17}/></button></div>
              <div className="qty-row"><div className="qty-control"><button onClick={()=>changeQty(index,-1)}><Minus size={15}/></button><span>{line.quantity}</span><button onClick={()=>changeQty(index,1)}><Plus size={15}/></button></div><div className="cart-line-actions"><button className="line-discount-button" onClick={()=>openDiscount({kind:'line',index})}><BadgePercent size={14}/> Discount</button><strong>{money.format(line.quantity*line.unitPrice-line.discountAmount)}</strong></div></div>
            </div>)}
          </div>
          <div className="cart-summary modern-cart-summary">
            <div><span>Subtotal</span><strong>{money.format(totals.gross)}</strong></div>
            {totals.lineDiscount > 0 && <div className="discount-row"><span>Item discounts</span><strong>-{money.format(totals.lineDiscount)}</strong></div>}
            <div><span>Tax</span><strong>{money.format(totals.tax)}</strong></div>
            <button className={`invoice-discount-row ${totals.invoiceDiscount>0?'active':''}`} disabled={!cart.length} onClick={()=>openDiscount({kind:'invoice'})}><span><BadgePercent size={14}/> Invoice discount</span><strong>{totals.invoiceDiscount>0?`-${money.format(totals.invoiceDiscount)}`:'Apply'}</strong></button>
            <div className="grand-total"><span>Total</span><strong>{money.format(totals.total)}</strong></div>
          </div>
          <div className="payment-actions modern-payment-actions">
            <button className="pay-button cash" disabled={!cart.length} onClick={() => {setCashTendered(String(totals.total.toFixed(2)));setCashOpen(true)}}><Banknote size={20}/> Cash</button>
            <button className="pay-button card" disabled={!cart.length} onClick={() => checkout('Card',0)}><CreditCard size={20}/> Card</button>
            <button className="pay-button credit" disabled={!cart.length || !selectedCustomer} onClick={() => checkout('Credit',0)}>Udhaar</button>
            <button className="pay-button split" disabled={!cart.length} onClick={openSplit}><CircleDollarSign size={18}/> Split Payment</button>
          </div>
        </aside>
      </div>

      {cashOpen && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card">
        <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">CASH PAYMENT</span><h2>Total {money.format(totals.total)}</h2></div><button onClick={()=>setCashOpen(false)}><X size={20}/></button></div>
        <div className="modal-callout cash-callout"><Banknote size={18}/><div><strong>Fast cash checkout</strong><span>Enter the amount received. SmartPOS calculates change automatically.</span></div></div>
        <label className="premium-field"><span>Amount tendered</span><div className="money-input"><em>Rs</em><input autoFocus type="number" step="0.01" min={totals.total} value={cashTendered} onChange={e=>setCashTendered(e.target.value)}/></div></label>
        <div className="payment-change-card"><span>Change due</span><strong>{money.format(Math.max(0,Number(cashTendered||0)-totals.total))}</strong></div>
        <div className="modal-actions"><button className="modal-secondary" onClick={()=>setCashOpen(false)}>Cancel</button><button className="modal-primary" disabled={Number(cashTendered||0)<totals.total} onClick={()=>checkout('Cash',Number(cashTendered))}><Check size={15}/> Complete sale</button></div>
      </div></div>}

      {discountEditor && !approvalPending && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card">
        <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">CONTROLLED DISCOUNT</span><h2>{discountEditor.target.kind==='invoice'?'Invoice discount':`Discount · ${cart[discountEditor.target.index]?.name ?? 'Item'}`}</h2></div><button onClick={()=>setDiscountEditor(null)}><X size={20}/></button></div>
        <div className="modal-callout discount-callout"><BadgePercent size={18}/><div><strong>Manager approval required</strong><span>Any positive discount is approved with manager credentials and recorded for audit.</span></div></div>
        <div className="discount-mode-toggle"><button className={discountEditor.mode==='amount'?'active':''} onClick={()=>setDiscountEditor({...discountEditor,mode:'amount'})}>Amount</button><button className={discountEditor.mode==='percent'?'active':''} onClick={()=>setDiscountEditor({...discountEditor,mode:'percent'})}>Percent</button></div>
        {discountEditor.mode==='amount' ? <label className="premium-field"><span>Discount amount <em>max {money.format(discountEditor.maxAmount)}</em></span><div className="money-input"><em>Rs</em><input autoFocus type="number" min="0" max={discountEditor.maxAmount} step="0.01" value={discountEditor.amount} onChange={e=>setDiscountEditor({...discountEditor,amount:e.target.value})}/></div></label> : <label className="premium-field"><span>Discount percent</span><div className="percent-input"><input autoFocus type="number" min="0" max="100" step="0.1" value={discountEditor.percent} onChange={e=>setDiscountEditor({...discountEditor,percent:e.target.value})}/><em>%</em></div><small>Calculated discount: {money.format(discountAmount(discountEditor))}</small></label>}
        <label className="premium-field"><span>Discount reason</span><textarea rows={3} value={discountEditor.reason} onChange={e=>setDiscountEditor({...discountEditor,reason:e.target.value})} placeholder="Promotion, damaged packaging, loyalty adjustment…"/></label>
        {discountValidation && <div className="modal-error">{discountValidation}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={()=>{applyApprovedDiscount(discountEditor.target,0,null);setDiscountEditor(null)}}>Remove discount</button><button className="modal-primary" onClick={requestDiscountApproval}><ShieldCheck size={15}/> Continue</button></div>
      </div></div>}

      {approvalPending && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card">
        <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">MANAGER APPROVAL</span><h2>Authorize {money.format(approvalPending.amount)} discount</h2></div><button onClick={()=>setApprovalPending(null)}><X size={20}/></button></div>
        <div className="manager-approval-banner"><ShieldCheck size={22}/><div><strong>Manager authorization</strong><span>Store Manager, Business Owner or Super Admin credentials are accepted.</span></div></div>
        <label className="premium-field"><span>Manager email</span><div className="icon-input"><KeyRound size={15}/><input autoFocus type="email" value={managerEmail} onChange={e=>setManagerEmail(e.target.value)} placeholder="manager@company.com"/></div></label>
        <label className="premium-field"><span>Manager password</span><input type="password" value={managerPassword} onChange={e=>setManagerPassword(e.target.value)}/></label>
        <label className="premium-field"><span>Approval reason</span><textarea rows={3} value={approvalReason} onChange={e=>setApprovalReason(e.target.value)}/></label>
        {approvalError && <div className="modal-error">{approvalError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={()=>setApprovalPending(null)}>Back</button><button className="modal-primary" disabled={approvalBusy} onClick={approveDiscount}><ShieldCheck size={15}/>{approvalBusy?'Authorizing…':'Approve discount'}</button></div>
      </div></div>}

      {splitOpen && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card wide">
        <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">FLEXIBLE CHECKOUT</span><h2>Split payment</h2></div><button onClick={()=>setSplitOpen(false)}><X size={20}/></button></div>
        <div className="split-payment-summary"><div><span>Total due</span><strong>{money.format(totals.total)}</strong></div><div><span>Allocated</span><strong>{money.format(splitPaid)}</strong></div><div className={Math.abs(splitRemaining)<0.005?'balanced':'remaining'}><span>Remaining</span><strong>{money.format(splitRemaining)}</strong></div></div>
        <div className="split-lines"><div className="split-line-head"><span>Method</span><span>Amount</span><span>Reference</span><span/></div>{splitLines.map((line,index)=><div className="split-line" key={line.id}><select value={line.method} onChange={e=>setSplitLines(current=>current.map((x,i)=>i===index?{...x,method:e.target.value as PaymentLine['method']}:x))}><option>Cash</option><option>Card</option><option>Other</option><option>Credit</option></select><input type="number" min="0.01" step="0.01" value={line.amount} onChange={e=>setSplitLines(current=>current.map((x,i)=>i===index?{...x,amount:Number(e.target.value)}:x))}/><input placeholder="Optional" value={line.reference} onChange={e=>setSplitLines(current=>current.map((x,i)=>i===index?{...x,reference:e.target.value}:x))}/><button disabled={splitLines.length<=1} onClick={()=>setSplitLines(current=>current.filter((_,i)=>i!==index))}><Trash2 size={15}/></button></div>)}</div>
        <button className="add-payment-line" onClick={addSplitLine}><PlusCircle size={15}/> Add payment line</button>
        {splitHasCredit && <div className={`modal-callout ${selectedCustomer?'':'warning'}`}><CreditCard size={18}/><div><strong>Credit / Udhaar</strong><span>{selectedCustomer ? `${selectedCustomer.name} · available ${money.format(selectedCustomer.availableCredit)}` : 'Select a customer on the POS screen before using a credit line.'}</span></div></div>}
        {splitError && <div className="modal-error">{splitError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={()=>setSplitOpen(false)}>Cancel</button><button className="modal-primary" disabled={!splitCanComplete || !!busyMessage} onClick={completeSplit}><Check size={15}/> Complete split sale</button></div>
      </div></div>}

      {heldOpen && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card wide">
        <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">PARKED SALES</span><h2>Held Sales</h2></div><button onClick={()=>setHeldOpen(false)}><X size={20}/></button></div>
        <div className="held-list premium-held-list">{heldSales.length ? heldSales.map(h => <div className="held-item premium-held-item" key={h.id}><button className="held-main" onClick={()=>retrieveHeld(h.id)}><div><strong>{h.heldNumber}</strong><span>{h.customerName} · {h.itemCount} items</span></div><div><strong>{money.format(h.totalAmount)}</strong><small>{h.expiryText}</small></div></button><button className="held-cancel" onClick={()=>cancelHeld(h.id)} title="Cancel held sale"><Trash2 size={15}/></button></div>) : <div className="cart-empty"><span>No active held sales.</span></div>}</div>
      </div></div>}

      {receipt && <div className="modal-backdrop premium-modal-backdrop"><div className="modal-card premium-modal-card receipt-modal">
        <div className="sale-success-icon"><Check size={24}/></div>
        <div className="receipt-success-copy"><span>SALE COMPLETED</span><h2>{receipt.saleNumber}</h2><p>Transaction completed successfully.</p></div>
        <div className="receipt-summary premium-receipt-summary"><div><span>Customer</span><strong>{receipt.customerName}</strong></div><div><span>Payment</span><strong>{receipt.paymentMethod}</strong></div>{receipt.discountAmount>0 && <div><span>Discount</span><strong>-{money.format(receipt.discountAmount)}</strong></div>}<div className="receipt-total"><span>Total</span><strong>{money.format(receipt.totalAmount)}</strong></div>{receipt.changeDue>0 && <div><span>Change</span><strong>{money.format(receipt.changeDue)}</strong></div>}</div>
        {receipt.payments && receipt.payments.length>1 && <div className="receipt-payment-lines">{receipt.payments.map((p,i)=><div key={i}><span>{p.method}{p.reference?` · ${p.reference}`:''}</span><strong>{money.format(p.amount)}</strong></div>)}</div>}
        <button className="primary-button modal-primary" onClick={()=>setReceipt(null)}>Start new sale</button>
      </div></div>}
    </section>
  );
}
