import {
  BadgeDollarSign, Barcode, Boxes, Check, ChevronRight, CircleDollarSign, Download,
  Eye, EyeOff, Grid2X2, ImagePlus, Layers3, List, MoreHorizontal, PackagePlus,
  Percent, Plus, Search, ShieldCheck, Sparkles, Store, Tags, Trash2, Upload, X
} from 'lucide-react';
import { useMemo, useRef, useState, type CSSProperties } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { catalogApi } from '../features/catalog/catalogApi';
import type {
  CatalogBootstrap, ProductDetail, ProductListItem, ProductVariant
} from '../features/catalog/types';
import { useStoreContext } from '../features/shared/storeContext';
import { useAuthStore } from '../features/auth/authStore';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const money = new Intl.NumberFormat('en-PK', {
  style: 'currency', currency: 'PKR', maximumFractionDigits: 0
});

type EditorTab = 'details' | 'pricing' | 'stores' | 'variants';

function marginPercent(product: { costPrice: number; sellPrice: number }) {
  return product.sellPrice <= 0 ? 0 : ((product.sellPrice - product.costPrice) / product.sellPrice) * 100;
}

function emptyModel(data: CatalogBootstrap, storeId: string | null): ProductDetail {
  return {
    id: EMPTY_GUID,
    sku: '',
    name: '',
    description: '',
    barcode: '',
    categoryId: null,
    brandId: null,
    unitId: null,
    taxCategoryId: null,
    costPrice: 0,
    sellPrice: 0,
    reorderLevel: 0,
    imageUrl: null,
    isActive: true,
    additionalBarcodes: [],
    variants: [],
    storePrices: data.stores.map(store => ({
      storeId: store.id,
      storeName: store.displayName,
      price: null
    })),
    storeAssignments: data.stores.map(store => ({
      storeId: store.id,
      storeName: store.displayName,
      isVisibleOnPos: store.id === storeId
    }))
  };
}

export default function ProductsPage() {
  const storeId = useStoreContext(s => s.storeId);
  const user = useAuthStore(s => s.user)!;
  const canManage = user.permissions.includes('catalog.products.manage');
  const queryClient = useQueryClient();
  const importRef = useRef<HTMLInputElement>(null);

  const [search, setSearch] = useState('');
  const [assignedOnly, setAssignedOnly] = useState(false);
  const [category, setCategory] = useState('All');
  const [brand, setBrand] = useState('All');
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('all');
  const [view, setView] = useState<'grid' | 'list'>('grid');
  const [editor, setEditor] = useState<ProductDetail | null>(null);
  const [editorTab, setEditorTab] = useState<EditorTab>('details');
  const [editorLoading, setEditorLoading] = useState(false);
  const [barcodeDraft, setBarcodeDraft] = useState('');
  const [toast, setToast] = useState<{ kind: 'ok' | 'error' | 'info'; text: string } | null>(null);

  const catalog = useQuery({
    queryKey: ['catalog', storeId, search, assignedOnly],
    queryFn: () => catalogApi.bootstrap(storeId, search, assignedOnly),
    staleTime: 20_000
  });

  const data = catalog.data;
  const products = useMemo(() => {
    let rows = data?.products ?? [];
    if (category !== 'All') rows = rows.filter(x => x.categoryName === category);
    if (brand !== 'All') rows = rows.filter(x => x.brandName === brand);
    if (status === 'active') rows = rows.filter(x => x.isActive);
    if (status === 'inactive') rows = rows.filter(x => !x.isActive);
    return rows;
  }, [data?.products, category, brand, status]);

  const metrics = useMemo(() => {
    const rows = data?.products ?? [];
    const active = rows.filter(x => x.isActive).length;
    const assigned = rows.filter(x => x.isAssignedToSelectedStore).length;
    const avgMargin = rows.length
      ? rows.reduce((sum, x) => sum + marginPercent(x), 0) / rows.length
      : 0;
    const retailValue = rows.reduce((sum, x) => sum + x.sellPrice, 0);
    return { count: rows.length, active, assigned, avgMargin, retailValue };
  }, [data?.products]);

  function notify(kind: 'ok' | 'error' | 'info', text: string) {
    setToast({ kind, text });
    window.setTimeout(() => setToast(null), 4200);
  }

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['catalog'] });
    await queryClient.invalidateQueries({ queryKey: ['inventory'] });
  }

  async function openEditor(item?: ProductListItem) {
    if (!canManage || !data) return;
    setEditorTab('details');
    setBarcodeDraft('');
    if (!item) {
      setEditor(emptyModel(data, storeId));
      return;
    }
    setEditorLoading(true);
    try {
      setEditor(await catalogApi.product(item.id));
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Could not load product.');
    } finally {
      setEditorLoading(false);
    }
  }

  const saveMutation = useMutation({
    mutationFn: async (model: ProductDetail) => {
      if (!model.sku.trim() || !model.name.trim()) throw new Error('SKU and product name are required.');
      if (model.costPrice < 0 || model.sellPrice < 0 || model.reorderLevel < 0) {
        throw new Error('Pricing and reorder values cannot be negative.');
      }
      if (model.id === EMPTY_GUID) return catalogApi.create(model);
      await catalogApi.update(model.id, model);
      return { id: model.id };
    },
    onSuccess: async () => {
      setEditor(null);
      notify('ok', 'Product saved successfully.');
      await refresh();
    },
    onError: (error) => notify('error', error instanceof Error ? error.message : 'Could not save product.')
  });

  async function toggleActive(product: ProductListItem) {
    try {
      await catalogApi.setActive(product.id, !product.isActive);
      notify('ok', `${product.name} is now ${product.isActive ? 'inactive' : 'active'}.`);
      await refresh();
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Status update failed.');
    }
  }

  async function removeProduct(product: ProductListItem) {
    if (!window.confirm(`Delete "${product.name}"? This cannot be undone.`)) return;
    try {
      await catalogApi.delete(product.id);
      notify('ok', 'Product removed.');
      await refresh();
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Product could not be deleted.');
    }
  }

  async function bulkVisibility(visible: boolean) {
    if (!storeId || products.length === 0) return;
    const action = visible ? 'make visible in POS' : 'hide from POS';
    if (!window.confirm(`${action} for ${products.length} currently filtered product(s)?`)) return;
    try {
      await catalogApi.assign(storeId, products.map(x => x.id), visible);
      notify('ok', visible ? 'Filtered products are now visible in this store.' : 'Filtered products are hidden in this store.');
      await refresh();
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Store catalog update failed.');
    }
  }

  async function importFile(file: File | undefined) {
    if (!file) return;
    try {
      const result = await catalogApi.importProducts(file, storeId);
      const suffix = result.errors.length ? ` ${result.errors.slice(0, 2).join(' ')}` : '';
      notify(result.errors.length ? 'info' : 'ok',
        `Import complete: ${result.created} created, ${result.updated} updated, ${result.skipped} skipped.${suffix}`);
      await refresh();
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Import failed.');
    } finally {
      if (importRef.current) importRef.current.value = '';
    }
  }

  async function uploadImage(file: File | undefined) {
    if (!file || !editor) return;
    try {
      const result = await catalogApi.uploadImage(file);
      setEditor({ ...editor, imageUrl: result.url });
      notify('ok', 'Product image uploaded.');
    } catch (error) {
      notify('error', error instanceof Error ? error.message : 'Image upload failed.');
    }
  }

  function updateStorePrice(storeIdValue: string, value: string) {
    if (!editor) return;
    setEditor({
      ...editor,
      storePrices: editor.storePrices.map(x =>
        x.storeId === storeIdValue ? { ...x, price: value === '' ? null : Number(value) } : x
      )
    });
  }

  function updateStoreAssignment(storeIdValue: string, checked: boolean) {
    if (!editor) return;
    setEditor({
      ...editor,
      storeAssignments: editor.storeAssignments.map(x =>
        x.storeId === storeIdValue ? { ...x, isVisibleOnPos: checked } : x
      )
    });
  }

  function addVariant() {
    if (!editor) return;
    const variant: ProductVariant = {
      id: EMPTY_GUID, sku: '', name: '', barcode: '', costPrice: null, sellPrice: null, isActive: true
    };
    setEditor({ ...editor, variants: [...editor.variants, variant] });
  }

  function updateVariant(index: number, patch: Partial<ProductVariant>) {
    if (!editor) return;
    const variants = [...editor.variants];
    variants[index] = { ...variants[index], ...patch };
    setEditor({ ...editor, variants });
  }

  if (catalog.isLoading) return <div className="premium-loading">Building your product studio…</div>;
  if (catalog.isError || !data) {
    return <div className="premium-empty"><Boxes size={34}/><h2>Products could not load</h2><p>Refresh the page or verify the React catalog API.</p></div>;
  }

  return (
    <section className="premium-products-page">
      {toast && <div className={`premium-toast ${toast.kind}`}>{toast.text}</div>}

      <div className="catalog-hero">
        <div className="catalog-hero-glow" />
        <div className="catalog-hero-copy">
          <span className="premium-kicker"><Sparkles size={13}/> Catalog Studio</span>
          <h1>Your products, presented like a <em>brand</em>.</h1>
          <p>Control every SKU, price, barcode and store assignment from one polished product workspace.</p>
          <div className="catalog-hero-actions">
            {canManage && <button className="catalog-primary-action" onClick={() => openEditor()}><PackagePlus size={17}/> New product</button>}
            {canManage && <button className="catalog-ghost-action" onClick={() => importRef.current?.click()}><Upload size={16}/> Import Excel</button>}
            <button className="catalog-ghost-action" onClick={() => window.open('/api/products/export', '_blank', 'noopener,noreferrer')}><Download size={16}/> Export</button>
            <input ref={importRef} hidden type="file" accept=".xlsx" onChange={e => importFile(e.target.files?.[0])}/>
          </div>
        </div>
        <div className="catalog-hero-orbit">
          <div className="orbit-card orbit-one"><Tags size={18}/><span>Catalog size</span><strong>{metrics.count}</strong></div>
          <div className="orbit-card orbit-two"><Percent size={18}/><span>Avg. margin</span><strong>{metrics.avgMargin.toFixed(1)}%</strong></div>
          <div className="orbit-card orbit-three"><Store size={18}/><span>Store visible</span><strong>{metrics.assigned}</strong></div>
        </div>
      </div>

      <div className="catalog-kpi-grid">
        <article className="catalog-kpi purple"><div className="catalog-kpi-icon"><Layers3 size={20}/></div><div><span>Total products</span><strong>{metrics.count}</strong><small>{metrics.active} currently active</small></div></article>
        <article className="catalog-kpi green"><div className="catalog-kpi-icon"><ShieldCheck size={20}/></div><div><span>Current store</span><strong>{metrics.assigned}</strong><small>Visible on this POS</small></div></article>
        <article className="catalog-kpi blue"><div className="catalog-kpi-icon"><BadgeDollarSign size={20}/></div><div><span>Average margin</span><strong>{metrics.avgMargin.toFixed(1)}%</strong><small>Across loaded products</small></div></article>
        <article className="catalog-kpi amber"><div className="catalog-kpi-icon"><CircleDollarSign size={20}/></div><div><span>Price sum</span><strong>{money.format(metrics.retailValue)}</strong><small>Combined default retail</small></div></article>
      </div>

      <div className="catalog-panel">
        <div className="catalog-toolbar">
          <div className="catalog-search">
            <Search size={17}/>
            <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Search products, SKU or barcode…" />
            {search && <button onClick={() => setSearch('')}><X size={15}/></button>}
          </div>

          <div className="catalog-filters">
            <select value={category} onChange={e => setCategory(e.target.value)}>
              <option>All</option>{data.categories.map(x => <option key={x.id}>{x.name}</option>)}
            </select>
            <select value={brand} onChange={e => setBrand(e.target.value)}>
              <option>All</option>{data.brands.map(x => <option key={x.id}>{x.name}</option>)}
            </select>
            <select value={status} onChange={e => setStatus(e.target.value as typeof status)}>
              <option value="all">All status</option><option value="active">Active</option><option value="inactive">Inactive</option>
            </select>
          </div>

          <label className="catalog-check">
            <input type="checkbox" checked={assignedOnly} onChange={e => setAssignedOnly(e.target.checked)}/>
            <span>Current store only</span>
          </label>

          <div className="catalog-view-switch">
            <button className={view === 'grid' ? 'active' : ''} onClick={() => setView('grid')}><Grid2X2 size={16}/></button>
            <button className={view === 'list' ? 'active' : ''} onClick={() => setView('list')}><List size={16}/></button>
          </div>
        </div>

        <div className="catalog-subtoolbar">
          <div><strong>{products.length}</strong> products match your current view</div>
          {canManage && storeId && <div className="catalog-bulk-actions">
            <button onClick={() => bulkVisibility(true)}><Eye size={14}/> Make visible</button>
            <button onClick={() => bulkVisibility(false)}><EyeOff size={14}/> Hide from POS</button>
          </div>}
        </div>

        {products.length === 0 ? (
          <div className="catalog-empty"><Boxes size={36}/><strong>No products match these filters</strong><span>Try clearing a filter or import your catalog.</span></div>
        ) : view === 'grid' ? (
          <div className="product-studio-grid">
            {products.map(product => (
              <article className="studio-product-card" key={product.id}>
                <button className="studio-product-visual" onClick={() => openEditor(product)} disabled={!canManage}>
                  {product.imageUrl ? <img src={product.imageUrl} alt=""/> : <div className="product-monogram">{product.name.slice(0, 2).toUpperCase()}</div>}
                  <span className={`store-visibility-badge ${product.isAssignedToSelectedStore ? 'visible' : 'hidden'}`}>
                    {product.isAssignedToSelectedStore ? <Eye size={11}/> : <EyeOff size={11}/>}
                    {product.isAssignedToSelectedStore ? 'In store' : 'Hidden'}
                  </span>
                  {!product.isActive && <span className="inactive-ribbon">Inactive</span>}
                </button>
                <div className="studio-product-body">
                  <div className="product-category-line"><span>{product.categoryName ?? 'Uncategorized'}</span><small>{product.brandName ?? 'No brand'}</small></div>
                  <h3>{product.name}</h3>
                  <div className="product-sku"><Barcode size={12}/>{product.sku}</div>
                  <div className="product-price-row">
                    <div><small>Retail</small><strong>{money.format(product.sellPrice)}</strong></div>
                    <div><small>Margin</small><strong className={marginPercent(product) < 10 ? 'margin-low' : ''}>{marginPercent(product).toFixed(1)}%</strong></div>
                  </div>
                  <div className="product-card-footer">
                    <span>{product.variantCount ? `${product.variantCount} variant${product.variantCount === 1 ? '' : 's'}` : 'Standard item'}</span>
                    {canManage && <div className="product-actions">
                      <button title="Edit" onClick={() => openEditor(product)}><ChevronRight size={15}/></button>
                      <button title={product.isActive ? 'Deactivate' : 'Activate'} onClick={() => toggleActive(product)}>{product.isActive ? <EyeOff size={14}/> : <Eye size={14}/>}</button>
                      <button title="Delete" className="danger" onClick={() => removeProduct(product)}><Trash2 size={14}/></button>
                    </div>}
                  </div>
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="catalog-list-wrap">
            <div className="catalog-list-head"><span>Product</span><span>Category / Brand</span><span>Cost</span><span>Retail</span><span>Margin</span><span>Store</span><span></span></div>
            {products.map(product => (
              <div className="catalog-list-row" key={product.id}>
                <div className="catalog-product-cell">
                  <div className="catalog-mini-image">{product.imageUrl ? <img src={product.imageUrl} alt=""/> : product.name.slice(0,2).toUpperCase()}</div>
                  <div><strong>{product.name}</strong><small>{product.sku}{product.barcode ? ` · ${product.barcode}` : ''}</small></div>
                </div>
                <div><strong>{product.categoryName ?? '—'}</strong><small>{product.brandName ?? '—'}</small></div>
                <div>{money.format(product.costPrice)}</div>
                <div><strong>{money.format(product.sellPrice)}</strong></div>
                <div><span className={`margin-pill ${marginPercent(product) < 10 ? 'low' : ''}`}>{marginPercent(product).toFixed(1)}%</span></div>
                <div><span className={`catalog-status ${product.isAssignedToSelectedStore ? 'good' : 'muted'}`}>{product.isAssignedToSelectedStore ? 'Visible' : 'Hidden'}</span></div>
                <div className="list-row-actions">{canManage && <><button onClick={() => openEditor(product)}>Edit</button><button onClick={() => toggleActive(product)}><MoreHorizontal size={15}/></button></>}</div>
              </div>
            ))}
          </div>
        )}
      </div>

      {(editor || editorLoading) && (
        <div className="product-editor-layer">
          <button className="product-editor-scrim" onClick={() => !saveMutation.isPending && setEditor(null)} aria-label="Close editor"/>
          <aside className="product-editor">
            {editorLoading || !editor ? <div className="premium-loading compact">Loading product…</div> : <>
              <div className="product-editor-head">
                <div className="editor-product-avatar">{editor.imageUrl ? <img src={editor.imageUrl} alt=""/> : <Boxes size={21}/>}</div>
                <div><span>{editor.id === EMPTY_GUID ? 'Create product' : 'Product editor'}</span><h2>{editor.name || 'Untitled product'}</h2></div>
                <button className="editor-close" onClick={() => setEditor(null)}><X size={19}/></button>
              </div>

              <div className="product-editor-tabs">
                {(['details','pricing','stores','variants'] as EditorTab[]).map(tab => (
                  <button key={tab} className={editorTab === tab ? 'active' : ''} onClick={() => setEditorTab(tab)}>
                    {tab === 'details' ? 'Details' : tab === 'pricing' ? 'Pricing & tax' : tab === 'stores' ? 'Stores' : 'Barcodes & variants'}
                  </button>
                ))}
              </div>

              <div className="product-editor-body">
                {editorTab === 'details' && <>
                  <div className="editor-section-title"><span>Product identity</span><strong>Make the item instantly recognizable.</strong></div>
                  <div className="editor-form-grid">
                    <label><span>SKU *</span><input value={editor.sku} onChange={e => setEditor({...editor, sku:e.target.value})}/></label>
                    <label className="wide"><span>Product name *</span><input value={editor.name} onChange={e => setEditor({...editor, name:e.target.value})}/></label>
                    <label className="wide"><span>Description</span><textarea rows={3} value={editor.description ?? ''} onChange={e => setEditor({...editor, description:e.target.value})}/></label>
                    <label><span>Category</span><select value={editor.categoryId ?? ''} onChange={e => setEditor({...editor, categoryId:e.target.value || null})}><option value="">Unassigned</option>{data.categories.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
                    <label><span>Brand</span><select value={editor.brandId ?? ''} onChange={e => setEditor({...editor, brandId:e.target.value || null})}><option value="">Unassigned</option>{data.brands.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
                    <label><span>Unit</span><select value={editor.unitId ?? ''} onChange={e => setEditor({...editor, unitId:e.target.value || null})}><option value="">Unassigned</option>{data.units.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
                    <label><span>Primary barcode</span><input value={editor.barcode ?? ''} onChange={e => setEditor({...editor, barcode:e.target.value})}/></label>
                  </div>

                  <div className="product-image-studio">
                    <div className="editor-image-preview">{editor.imageUrl ? <img src={editor.imageUrl} alt=""/> : <ImagePlus size={30}/>}</div>
                    <div className="editor-image-copy"><strong>Product imagery</strong><span>Use a clean square image for a premium POS catalog.</span><div><label className="image-upload-button"><Upload size={14}/> Upload image<input hidden type="file" accept="image/*" onChange={e => uploadImage(e.target.files?.[0])}/></label><button onClick={() => setEditor({...editor,imageUrl:null})}>Remove</button></div></div>
                    <input className="image-url-input" placeholder="Or paste image URL…" value={editor.imageUrl ?? ''} onChange={e => setEditor({...editor,imageUrl:e.target.value || null})}/>
                  </div>

                  <label className="editor-switch"><input type="checkbox" checked={editor.isActive} onChange={e => setEditor({...editor,isActive:e.target.checked})}/><span><strong>Active for sale</strong><small>Inactive products remain in history but are unavailable for new sales.</small></span></label>
                </>}

                {editorTab === 'pricing' && <>
                  <div className="editor-section-title"><span>Commercial setup</span><strong>Protect margin while keeping pricing flexible.</strong></div>
                  <div className="pricing-editor-grid">
                    <label><span>Cost price</span><input type="number" min="0" value={editor.costPrice} onChange={e => setEditor({...editor,costPrice:Number(e.target.value)})}/></label>
                    <label><span>Default retail</span><input type="number" min="0" value={editor.sellPrice} onChange={e => setEditor({...editor,sellPrice:Number(e.target.value)})}/></label>
                    <label><span>Reorder level</span><input type="number" min="0" value={editor.reorderLevel} onChange={e => setEditor({...editor,reorderLevel:Number(e.target.value)})}/></label>
                    <label><span>Tax category</span><select value={editor.taxCategoryId ?? ''} onChange={e => setEditor({...editor,taxCategoryId:e.target.value || null})}><option value="">No tax</option>{data.taxes.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
                  </div>
                  <div className="margin-spotlight">
                    <div className="margin-gauge" style={{'--margin': `${Math.min(Math.max(marginPercent(editor),0),100)}%`} as CSSProperties}><span>{marginPercent(editor).toFixed(1)}%</span></div>
                    <div><span>Gross margin</span><strong>{money.format(Math.max(0, editor.sellPrice - editor.costPrice))} gross profit per unit</strong><small>Based on default retail and cost price.</small></div>
                  </div>
                  <div className="store-price-editor">
                    <div className="editor-section-title small"><span>Store overrides</span><strong>Leave blank to use default retail.</strong></div>
                    {editor.storePrices.map(price => <div className="store-price-row" key={price.storeId}><div><Store size={15}/><span>{price.storeName}</span></div><input type="number" min="0" placeholder="Default" value={price.price ?? ''} onChange={e => updateStorePrice(price.storeId,e.target.value)}/></div>)}
                  </div>
                </>}

                {editorTab === 'stores' && <>
                  <div className="editor-section-title"><span>Store availability</span><strong>Choose where this product appears in POS.</strong></div>
                  <div className="store-visibility-list">
                    {editor.storeAssignments.map(assignment => {
                      const override = editor.storePrices.find(x => x.storeId === assignment.storeId)?.price;
                      return <label className={`store-visibility-row ${assignment.isVisibleOnPos ? 'selected' : ''}`} key={assignment.storeId}>
                        <div className="store-visibility-icon"><Store size={18}/></div>
                        <div><strong>{assignment.storeName}</strong><span>{override != null ? `Override ${money.format(override)}` : `Default ${money.format(editor.sellPrice)}`}</span></div>
                        <input type="checkbox" checked={assignment.isVisibleOnPos} onChange={e => updateStoreAssignment(assignment.storeId,e.target.checked)}/>
                        <span className="switch-ui"/>
                      </label>;
                    })}
                  </div>
                </>}

                {editorTab === 'variants' && <>
                  <div className="editor-section-title"><span>Scan & variants</span><strong>Support alternate barcodes, sizes, shades or packs.</strong></div>
                  <div className="barcode-studio">
                    <div className="barcode-add-row"><Barcode size={17}/><input placeholder="Additional barcode" value={barcodeDraft} onChange={e => setBarcodeDraft(e.target.value)} onKeyDown={e => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        const code = barcodeDraft.trim();
                        if (code && !editor.additionalBarcodes.some(x => x.toLowerCase() === code.toLowerCase())) setEditor({...editor,additionalBarcodes:[...editor.additionalBarcodes,code]});
                        setBarcodeDraft('');
                      }
                    }}/><button onClick={() => {const code=barcodeDraft.trim(); if(code && !editor.additionalBarcodes.some(x=>x.toLowerCase()===code.toLowerCase())) setEditor({...editor,additionalBarcodes:[...editor.additionalBarcodes,code]}); setBarcodeDraft('');}}><Plus size={15}/> Add</button></div>
                    <div className="barcode-chip-list">{editor.additionalBarcodes.length === 0 ? <span className="barcode-empty">No alternate barcodes yet.</span> : editor.additionalBarcodes.map(code => <span className="barcode-chip" key={code}>{code}<button onClick={() => setEditor({...editor,additionalBarcodes:editor.additionalBarcodes.filter(x=>x!==code)})}><X size={11}/></button></span>)}</div>
                  </div>

                  <div className="variant-editor-head"><div><strong>Variants</strong><span>Optional child SKUs with independent barcode and price.</span></div><button onClick={addVariant}><Plus size={14}/> Add variant</button></div>
                  <div className="variant-editor-list">
                    {editor.variants.length === 0 ? <div className="variant-empty"><Layers3 size={24}/><span>No variants configured.</span></div> : editor.variants.map((variant,index) => <div className="variant-editor-row" key={`${variant.id}-${index}`}>
                      <input placeholder="Variant SKU" value={variant.sku} onChange={e=>updateVariant(index,{sku:e.target.value})}/>
                      <input placeholder="Variant name" value={variant.name} onChange={e=>updateVariant(index,{name:e.target.value})}/>
                      <input placeholder="Barcode" value={variant.barcode ?? ''} onChange={e=>updateVariant(index,{barcode:e.target.value})}/>
                      <input type="number" min="0" placeholder="Retail" value={variant.sellPrice ?? ''} onChange={e=>updateVariant(index,{sellPrice:e.target.value===''?null:Number(e.target.value)})}/>
                      <button onClick={() => setEditor({...editor,variants:editor.variants.filter((_,i)=>i!==index)})}><Trash2 size={14}/></button>
                    </div>)}
                  </div>
                </>}
              </div>

              <div className="product-editor-footer">
                <div><span>Default retail</span><strong>{money.format(editor.sellPrice)}</strong></div>
                <div><span>Margin</span><strong>{marginPercent(editor).toFixed(1)}%</strong></div>
                <span className="editor-footer-spacer"/>
                <button className="editor-cancel" onClick={() => setEditor(null)} disabled={saveMutation.isPending}>Cancel</button>
                <button className="editor-save" onClick={() => saveMutation.mutate(editor)} disabled={saveMutation.isPending}><Check size={16}/>{saveMutation.isPending ? 'Saving…' : 'Save product'}</button>
              </div>
            </>}
          </aside>
        </div>
      )}
    </section>
  );
}
