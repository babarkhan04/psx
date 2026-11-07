import {
  AlertTriangle, ArrowDownRight, ArrowUpRight, Boxes, Check, ClipboardList, Filter,
  History, PackageCheck, Plus, RefreshCw, Search, SlidersHorizontal, Sparkles,
  TrendingDown, Warehouse, X
} from 'lucide-react';
import { useMemo, useState, type CSSProperties } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { inventoryApi } from '../features/inventory/inventoryApi';
import type { StockAdjustmentLine, StockAdjustmentRequest, StockItem } from '../features/inventory/types';
import { useStoreContext } from '../features/shared/storeContext';
import { useAuthStore } from '../features/auth/authStore';

const money = new Intl.NumberFormat('en-PK', {
  style: 'currency', currency: 'PKR', maximumFractionDigits: 0
});

type InventoryTab = 'stock' | 'ledger';

export default function InventoryPage() {
  const storeId = useStoreContext(s => s.storeId);
  const user = useAuthStore(s => s.user)!;
  const canManage = user.permissions.includes('catalog.inventory.manage');
  const queryClient = useQueryClient();

  const [tab, setTab] = useState<InventoryTab>('stock');
  const [search, setSearch] = useState('');
  const [lowOnly, setLowOnly] = useState(false);
  const [movement, setMovement] = useState<'all' | 'Opening' | 'Adjustment' | 'Damage' | 'Loss' | 'Purchase' | 'Sale' | 'Transfer' | 'Return' | 'Void'>('all');
  const [adjustOpen, setAdjustOpen] = useState(false);
  const [quickStock, setQuickStock] = useState<StockItem | null>(null);
  const [toast, setToast] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);

  const inventory = useQuery({
    queryKey: ['inventory', storeId],
    queryFn: () => inventoryApi.bootstrap(storeId!, false, 220),
    enabled: !!storeId,
    staleTime: 15_000
  });

  const data = inventory.data;

  const productMap = useMemo(() => new Map((data?.products ?? []).map(x => [x.id, x])), [data?.products]);

  const stockRows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data?.stock ?? []).filter(row => {
      const searchOk = !term || row.productName.toLowerCase().includes(term) || row.productSku.toLowerCase().includes(term) || (row.variantName ?? '').toLowerCase().includes(term);
      return searchOk && (!lowOnly || row.isLowStock);
    });
  }, [data?.stock, search, lowOnly]);

  const ledgerRows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data?.ledger ?? []).filter(row => {
      const textOk = !term ||
        row.productName.toLowerCase().includes(term) ||
        (row.reference ?? '').toLowerCase().includes(term) ||
        (row.notes ?? '').toLowerCase().includes(term);
      const movementOk = movement === 'all' || row.movementType === movement;
      return textOk && movementOk;
    });
  }, [data?.ledger, search, movement]);

  const metrics = useMemo(() => {
    const rows = data?.stock ?? [];
    const totalUnits = rows.reduce((sum, x) => sum + x.quantityOnHand, 0);
    const low = rows.filter(x => x.isLowStock).length;
    const out = rows.filter(x => x.quantityOnHand <= 0).length;
    const value = rows.reduce((sum, x) => {
      const product = productMap.get(x.productId);
      return sum + x.quantityOnHand * (product?.costPrice ?? 0);
    }, 0);
    const healthy = rows.length ? Math.max(0, Math.round(((rows.length - low) / rows.length) * 100)) : 100;
    return { totalUnits, low, out, value, healthy, rows: rows.length };
  }, [data?.stock, productMap]);

  function notify(kind: 'ok' | 'error', text: string) {
    setToast({ kind, text });
    window.setTimeout(() => setToast(null), 4200);
  }

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['inventory'] });
    await queryClient.invalidateQueries({ queryKey: ['catalog'] });
  }

  if (!storeId) {
    return <div className="premium-empty"><Warehouse size={34}/><h2>Select a store</h2><p>Inventory is always scoped to an authorized store.</p></div>;
  }

  if (inventory.isLoading) return <div className="premium-loading">Reading live stock and movement history…</div>;
  if (inventory.isError || !data) {
    return <div className="premium-empty"><Boxes size={34}/><h2>Inventory could not load</h2><p>Refresh the page or verify store access.</p></div>;
  }

  return (
    <section className="premium-inventory-page">
      {toast && <div className={`premium-toast ${toast.kind}`}>{toast.text}</div>}

      <div className="inventory-hero">
        <div className="inventory-hero-grid" />
        <div className="inventory-hero-copy">
          <span className="premium-kicker"><Sparkles size={13}/> Stock Command Center</span>
          <h1>Know what you have, <em>before it becomes a problem.</em></h1>
          <p>Live stock health, reorder pressure and every inventory movement — built for fast retail decisions.</p>
          <div className="inventory-hero-actions">
            {canManage && <button className="inventory-primary" onClick={() => setAdjustOpen(true)}><Plus size={16}/> Post adjustment</button>}
            <button className="inventory-secondary" onClick={() => refresh()}><RefreshCw size={15}/> Refresh stock</button>
          </div>
        </div>

        <div className="inventory-health-card">
          <div className="health-ring" style={{'--health': `${metrics.healthy}%`} as CSSProperties}>
            <div><strong>{metrics.healthy}%</strong><span>Stock health</span></div>
          </div>
          <div className="health-copy">
            <span>Current store condition</span>
            <strong>{metrics.low === 0 ? 'Inventory looks healthy' : `${metrics.low} item${metrics.low === 1 ? '' : 's'} need attention`}</strong>
            <small>{metrics.out} currently out of stock</small>
          </div>
        </div>
      </div>

      <div className="inventory-kpi-grid">
        <article className="inventory-kpi indigo"><div className="inventory-kpi-icon"><Boxes size={20}/></div><div><span>Units on hand</span><strong>{metrics.totalUnits.toLocaleString()}</strong><small>Across {metrics.rows} stock positions</small></div></article>
        <article className="inventory-kpi cyan"><div className="inventory-kpi-icon"><PackageCheck size={20}/></div><div><span>Inventory cost</span><strong>{money.format(metrics.value)}</strong><small>Cost-value estimate</small></div></article>
        <article className="inventory-kpi amber"><div className="inventory-kpi-icon"><AlertTriangle size={20}/></div><div><span>Low stock</span><strong>{metrics.low}</strong><small>At or below reorder level</small></div></article>
        <article className="inventory-kpi red"><div className="inventory-kpi-icon"><TrendingDown size={20}/></div><div><span>Out of stock</span><strong>{metrics.out}</strong><small>Immediate replenishment candidates</small></div></article>
      </div>

      <div className="inventory-workspace">
        <div className="inventory-panel-head">
          <div>
            <span>Inventory intelligence</span>
            <h2>{tab === 'stock' ? 'Stock positions' : 'Movement ledger'}</h2>
          </div>
          <div className="inventory-tabs">
            <button className={tab === 'stock' ? 'active' : ''} onClick={() => setTab('stock')}><Boxes size={15}/> Stock</button>
            <button className={tab === 'ledger' ? 'active' : ''} onClick={() => setTab('ledger')}><History size={15}/> Ledger</button>
          </div>
        </div>

        <div className="inventory-toolbar">
          <div className="inventory-search"><Search size={16}/><input value={search} onChange={e => setSearch(e.target.value)} placeholder={tab === 'stock' ? 'Search product, SKU or variant…' : 'Search product, reference or notes…'}/>{search && <button onClick={() => setSearch('')}><X size={14}/></button>}</div>

          {tab === 'stock' ? (
            <label className="inventory-low-toggle"><input type="checkbox" checked={lowOnly} onChange={e => setLowOnly(e.target.checked)}/><span><AlertTriangle size={14}/> Low stock only</span></label>
          ) : (
            <div className="inventory-movement-filter"><Filter size={14}/><select value={movement} onChange={e => setMovement(e.target.value as typeof movement)}><option value="all">All movements</option><option>Opening</option><option>Adjustment</option><option>Damage</option><option>Loss</option><option>Purchase</option><option>Sale</option><option>Transfer</option><option>Return</option><option>Void</option></select></div>
          )}
        </div>

        {tab === 'stock' ? (
          stockRows.length === 0 ? <div className="inventory-empty"><Boxes size={32}/><strong>No stock positions match</strong><span>Try a different search or turn off low-stock-only.</span></div> :
          <div className="stock-table-wrap">
            <div className="stock-table-head"><span>Product</span><span>On hand</span><span>Reorder level</span><span>Health</span><span>Cost value</span><span></span></div>
            {stockRows.map(row => {
              const product = productMap.get(row.productId);
              const ratio = row.reorderLevel > 0 ? Math.min(100, Math.max(0, (row.quantityOnHand / Math.max(row.reorderLevel * 2, 1)) * 100)) : 100;
              const out = row.quantityOnHand <= 0;
              const value = row.quantityOnHand * (product?.costPrice ?? 0);
              return <div className={`stock-table-row ${row.isLowStock ? 'low' : ''}`} key={row.id}>
                <div className="stock-product-cell">
                  <div className="stock-product-avatar">{product?.imageUrl ? <img src={product.imageUrl} alt=""/> : row.productName.slice(0,2).toUpperCase()}</div>
                  <div><strong>{row.productName}</strong><small>{row.productSku}{row.variantName ? ` · ${row.variantName}` : ''}</small></div>
                </div>
                <div><strong className={out ? 'qty-out' : row.isLowStock ? 'qty-low' : ''}>{row.quantityOnHand}</strong><small>units</small></div>
                <div><strong>{row.reorderLevel}</strong><small>threshold</small></div>
                <div className="stock-health-cell">
                  <div className="stock-health-track"><span style={{width:`${ratio}%`}} className={out ? 'out' : row.isLowStock ? 'low' : 'good'}/></div>
                  <small>{out ? 'Out of stock' : row.isLowStock ? 'Reorder' : 'Healthy'}</small>
                </div>
                <div><strong>{money.format(value)}</strong><small>at cost</small></div>
                <div className="stock-row-actions">{canManage && <button onClick={() => setQuickStock(row)}><SlidersHorizontal size={14}/> Adjust</button>}</div>
              </div>;
            })}
          </div>
        ) : (
          ledgerRows.length === 0 ? <div className="inventory-empty"><History size={32}/><strong>No movements match</strong><span>Try another movement type or clear the search.</span></div> :
          <div className="ledger-timeline">
            {ledgerRows.map(row => {
              const positive = row.quantityChange > 0;
              return <article className="ledger-event" key={row.id}>
                <div className={`ledger-direction ${positive ? 'in' : 'out'}`}>{positive ? <ArrowUpRight size={17}/> : <ArrowDownRight size={17}/>}</div>
                <div className="ledger-event-main">
                  <div className="ledger-event-top"><div><strong>{row.productName}</strong><span>{row.variantName ?? row.movementType}</span></div><time>{new Date(row.createdOn).toLocaleString()}</time></div>
                  <div className="ledger-event-meta"><span className={`movement-pill ${row.movementType.toLowerCase()}`}>{row.movementType}</span>{row.reference && <span>Ref · {row.reference}</span>}{row.createdBy && <span>By · {row.createdBy}</span>}</div>
                  {row.notes && <p>{row.notes}</p>}
                </div>
                <div className={`ledger-qty ${positive ? 'positive' : 'negative'}`}><strong>{positive ? '+' : ''}{row.quantityChange}</strong><span>Balance {row.quantityAfter}</span></div>
              </article>;
            })}
          </div>
        )}
      </div>

      {(adjustOpen || quickStock) && <AdjustmentDialog
        storeId={storeId}
        products={data.products}
        initialProduct={quickStock}
        onClose={() => {setAdjustOpen(false); setQuickStock(null);}}
        onSaved={async () => {setAdjustOpen(false);setQuickStock(null);notify('ok','Inventory movement posted successfully.');await refresh();}}
        onError={text => notify('error', text)}
      />}
    </section>
  );
}

function AdjustmentDialog({
  storeId, products, initialProduct, onClose, onSaved, onError
}: {
  storeId: string;
  products: { id:string; name:string; sku:string }[];
  initialProduct: StockItem | null;
  onClose: () => void;
  onSaved: () => Promise<void>;
  onError: (text:string) => void;
}) {
  const [movementType, setMovementType] = useState<StockAdjustmentRequest['movementType']>('Adjustment');
  const [reference, setReference] = useState('');
  const [notes, setNotes] = useState('');
  const [productId, setProductId] = useState(initialProduct?.productId ?? '');
  const [quantity, setQuantity] = useState<number>(0);
  const [lines, setLines] = useState<StockAdjustmentLine[]>(initialProduct ? [{
    productId: initialProduct.productId,
    productName: initialProduct.productName,
    quantityChange: 0
  }] : []);

  const save = useMutation({
    mutationFn: async () => {
      const prepared = lines.filter(x => x.quantityChange !== 0);
      if (prepared.length === 0) throw new Error('Add at least one non-zero stock movement.');
      await inventoryApi.adjust({ storeId, movementType, reference: reference || null, notes: notes || null, lines: prepared });
    },
    onSuccess: onSaved,
    onError: error => onError(error instanceof Error ? error.message : 'Adjustment failed.')
  });

  function addLine() {
    if (!productId || quantity === 0) return;
    const product = products.find(x => x.id === productId);
    if (!product) return;
    setLines(current => {
      const existing = current.find(x => x.productId === productId);
      if (existing) return current.map(x => x.productId === productId ? {...x,quantityChange:x.quantityChange + quantity} : x);
      return [...current,{productId,productName:product.name,quantityChange:quantity}];
    });
    setProductId('');
    setQuantity(0);
  }

  function updateLine(index:number, value:number) {
    setLines(current => current.map((x,i)=>i===index?{...x,quantityChange:value}:x));
  }

  return <div className="premium-modal-backdrop">
    <div className="inventory-adjust-dialog">
      <div className="adjust-dialog-head">
        <div className="adjust-dialog-icon"><ClipboardList size={20}/></div>
        <div><span>Controlled stock movement</span><h3>{initialProduct ? `Adjust · ${initialProduct.productName}` : 'Post inventory adjustment'}</h3></div>
        <button onClick={onClose}><X size={18}/></button>
      </div>

      <div className="adjust-info-banner"><Sparkles size={15}/><div><strong>Every change is ledgered</strong><span>Use a clear reference and note so future audits explain why stock moved.</span></div></div>

      <div className="adjust-form-grid">
        <label><span>Movement type</span><select value={movementType} onChange={e=>setMovementType(e.target.value as StockAdjustmentRequest['movementType'])}><option>Adjustment</option><option>Opening</option><option>Damage</option><option>Loss</option></select></label>
        <label><span>Reference</span><input value={reference} onChange={e=>setReference(e.target.value)} placeholder="e.g. COUNT-SEP-01"/></label>
        <label className="wide"><span>Notes</span><textarea rows={2} value={notes} onChange={e=>setNotes(e.target.value)} placeholder="Reason for this stock movement…"/></label>
      </div>

      <div className="adjust-line-builder">
        <div className="adjust-product-select"><Search size={15}/><select value={productId} onChange={e=>setProductId(e.target.value)}><option value="">Select product…</option>{products.map(x=><option key={x.id} value={x.id}>{x.name} · {x.sku}</option>)}</select></div>
        <input type="number" step="0.001" value={quantity} onChange={e=>setQuantity(Number(e.target.value))} placeholder="+/- qty"/>
        <button onClick={addLine} disabled={!productId || quantity===0}><Plus size={15}/> Add line</button>
      </div>
      <p className="adjust-quantity-hint">Use a positive quantity to add stock and a negative quantity to reduce stock.</p>

      <div className="adjust-lines">
        {lines.length===0 ? <div className="adjust-lines-empty">No products added yet.</div> : lines.map((line,index)=><div className="adjust-line" key={`${line.productId}-${index}`}>
          <div><strong>{line.productName}</strong><span>{line.productId.slice(0,8)}</span></div>
          <input type="number" step="0.001" value={line.quantityChange} onChange={e=>updateLine(index,Number(e.target.value))}/>
          <span className={line.quantityChange>0?'positive':line.quantityChange<0?'negative':''}>{line.quantityChange>0?'Stock in':line.quantityChange<0?'Stock out':'No change'}</span>
          <button onClick={()=>setLines(current=>current.filter((_,i)=>i!==index))}><X size={14}/></button>
        </div>)}
      </div>

      <div className="adjust-dialog-footer">
        <button className="editor-cancel" onClick={onClose} disabled={save.isPending}>Cancel</button>
        <button className="editor-save" onClick={()=>save.mutate()} disabled={save.isPending}><Check size={16}/>{save.isPending?'Posting…':'Post movement'}</button>
      </div>
    </div>
  </div>;
}
