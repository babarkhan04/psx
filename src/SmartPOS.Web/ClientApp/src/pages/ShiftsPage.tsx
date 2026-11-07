import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowDownToLine, ArrowUpFromLine, Banknote, CalendarClock, CheckCircle2,
  CircleDollarSign, Clock3, FileText, LockOpen, Play, ReceiptText, RefreshCw,
  ShieldCheck, Sparkles, Store, X
} from 'lucide-react';
import { shiftsApi } from '../features/shifts/shiftsApi';
import type { CashDenominationLine, ShiftListItem, ShiftReport } from '../features/shifts/types';
import { useStoreContext } from '../features/shared/storeContext';
import { useAuthStore } from '../features/auth/authStore';

const money = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', maximumFractionDigits: 2 });
const compactMoney = new Intl.NumberFormat('en-PK', { style: 'currency', currency: 'PKR', notation: 'compact', maximumFractionDigits: 1 });
const dateTime = new Intl.DateTimeFormat('en-PK', { day: '2-digit', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' });

const permissions = {
  open: 'operations.shifts.open',
  close: 'operations.shifts.close',
  cash: 'operations.shifts.cashmove'
};

const defaultDenominations = [5000, 1000, 500, 100, 50, 20, 10, 5, 2, 1];

type Modal = 'open' | 'cash' | 'nosale' | 'close' | 'report' | null;

export default function ShiftsPage() {
  const storeId = useStoreContext(s => s.storeId);
  const user = useAuthStore(s => s.user)!;
  const queryClient = useQueryClient();
  const [openOnly, setOpenOnly] = useState(false);
  const [modal, setModal] = useState<Modal>(null);
  const [selectedShift, setSelectedShift] = useState<ShiftListItem | null>(null);
  const [report, setReport] = useState<ShiftReport | null>(null);
  const [actionError, setActionError] = useState('');

  const [registerId, setRegisterId] = useState('');
  const [openingFloat, setOpeningFloat] = useState('0');
  const [openingNotes, setOpeningNotes] = useState('');

  const [movementType, setMovementType] = useState<'CashIn' | 'CashOut'>('CashIn');
  const [movementAmount, setMovementAmount] = useState('');
  const [movementNotes, setMovementNotes] = useState('');
  const [noSaleReason, setNoSaleReason] = useState('');

  const [countedCash, setCountedCash] = useState('0');
  const [closingNotes, setClosingNotes] = useState('');
  const [denominations, setDenominations] = useState<CashDenominationLine[]>(
    defaultDenominations.map((value, index) => ({ denominationValue: value, quantity: 0, sortOrder: index + 1 }))
  );

  const canOpen = user.permissions.includes(permissions.open);
  const canClose = user.permissions.includes(permissions.close);
  const canCash = user.permissions.includes(permissions.cash);

  const shifts = useQuery({
    queryKey: ['shifts', storeId, openOnly],
    queryFn: () => shiftsApi.list(storeId!, openOnly),
    enabled: !!storeId
  });

  const registers = useQuery({
    queryKey: ['shift-registers', storeId],
    queryFn: () => shiftsApi.registers(storeId!),
    enabled: !!storeId
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['shifts'] }),
      queryClient.invalidateQueries({ queryKey: ['shift-registers'] }),
      queryClient.invalidateQueries({ queryKey: ['pos-bootstrap'] }),
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    ]);
  };

  const action = useMutation({
    mutationFn: async (fn: () => Promise<unknown>) => fn(),
    onSuccess: async () => {
      setModal(null);
      setActionError('');
      await refresh();
    },
    onError: (e) => setActionError(e instanceof Error ? e.message : 'The action could not be completed.')
  });

  const rows = shifts.data ?? [];
  const active = rows.filter(s => s.status === 'Open');
  const today = new Date();
  const todays = rows.filter(s => {
    const d = new Date(s.openedOn);
    return d.getFullYear() === today.getFullYear() && d.getMonth() === today.getMonth() && d.getDate() === today.getDate();
  });
  const activeFloat = active.reduce((sum, s) => sum + s.openingFloat, 0);
  const todayVariance = todays.filter(s => s.status === 'Closed').reduce((sum, s) => sum + s.variance, 0);
  const availableRegisters = (registers.data ?? []).filter(r => !r.hasOpenShift);

  const denominationTotal = useMemo(
    () => denominations.reduce((sum, line) => sum + line.denominationValue * line.quantity, 0),
    [denominations]
  );
  const closeExpected = report?.expectedCash ?? selectedShift?.expectedCash ?? 0;
  const closeVariance = Number(countedCash || 0) - closeExpected;

  function resetModal() {
    setModal(null);
    setActionError('');
  }

  function showOpen() {
    const first = availableRegisters[0]?.id ?? '';
    setRegisterId(first);
    setOpeningFloat('0');
    setOpeningNotes('');
    setActionError('');
    setModal('open');
  }

  function showCash(shift: ShiftListItem) {
    setSelectedShift(shift);
    setMovementType('CashIn');
    setMovementAmount('');
    setMovementNotes('');
    setActionError('');
    setModal('cash');
  }

  function showNoSale(shift: ShiftListItem) {
    setSelectedShift(shift);
    setNoSaleReason('');
    setActionError('');
    setModal('nosale');
  }

  async function showReport(shift: ShiftListItem, nextModal: 'report' | 'close' = 'report') {
    if (!storeId) return;
    setSelectedShift(shift);
    setActionError('');
    try {
      const value = await shiftsApi.report(storeId, shift.id);
      setReport(value);
      if (nextModal === 'close') {
        setCountedCash(String(value.expectedCash.toFixed(2)));
        setClosingNotes('');
        setDenominations(defaultDenominations.map((v, index) => ({ denominationValue: v, quantity: 0, sortOrder: index + 1 })));
      }
      setModal(nextModal);
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Could not load shift report.');
    }
  }

  function updateDenomination(index: number, quantity: number) {
    setDenominations(current => {
      const next = [...current];
      next[index] = { ...next[index], quantity: Math.max(0, Math.floor(quantity || 0)) };
      const total = next.reduce((sum, line) => sum + line.denominationValue * line.quantity, 0);
      setCountedCash(String(total.toFixed(2)));
      return next;
    });
  }

  if (!storeId) {
    return <div className="empty-state"><Store size={36}/><h2>Select a store</h2><p>Choose a store in the header to manage shifts and cash.</p></div>;
  }

  return (
    <section className="premium-shifts-page">
      <div className="shifts-hero premium-page-hero">
        <div className="premium-page-hero-copy">
          <span className="premium-kicker"><Sparkles size={13}/> CASH CONTROL CENTER</span>
          <h1>Shifts &amp; Cash</h1>
          <p>Control register openings, drawer movements and end-of-day accountability from one focused workspace.</p>
          <div className="hero-trust-row">
            <span><ShieldCheck size={14}/> Store-scoped security</span>
            <span><ReceiptText size={14}/> X / Z reporting</span>
            <span><Clock3 size={14}/> Live shift status</span>
          </div>
        </div>
        <div className="shifts-hero-action">
          <div className="hero-action-orb"><CircleDollarSign size={26}/></div>
          <div><small>Open registers</small><strong>{active.length}</strong></div>
          {canOpen && <button className="premium-hero-button" onClick={showOpen} disabled={!availableRegisters.length}><Play size={16}/> Open shift</button>}
        </div>
      </div>

      {actionError && <div className="inline-error premium-inline-error"><span>{actionError}</span><button onClick={() => setActionError('')}><X size={16}/></button></div>}

      <div className="shift-kpi-grid">
        <article className="shift-kpi-card violet"><div className="shift-kpi-icon"><CalendarClock size={20}/></div><div><span>Open shifts</span><strong>{active.length}</strong><small>Active cash drawers</small></div></article>
        <article className="shift-kpi-card emerald"><div className="shift-kpi-icon"><Banknote size={20}/></div><div><span>Active float</span><strong>{compactMoney.format(activeFloat)}</strong><small>Opening cash across registers</small></div></article>
        <article className="shift-kpi-card blue"><div className="shift-kpi-icon"><ReceiptText size={20}/></div><div><span>Today</span><strong>{todays.length}</strong><small>Shifts opened today</small></div></article>
        <article className={`shift-kpi-card ${todayVariance === 0 ? 'slate' : todayVariance > 0 ? 'amber' : 'rose'}`}><div className="shift-kpi-icon"><CircleDollarSign size={20}/></div><div><span>Closed variance</span><strong>{money.format(todayVariance)}</strong><small>{todayVariance === 0 ? 'Balanced today' : todayVariance > 0 ? 'Net over' : 'Net short'}</small></div></article>
      </div>

      {active.length > 0 && <section className="active-shifts-section premium-panel">
        <div className="premium-panel-heading">
          <div><span className="panel-kicker">LIVE REGISTERS</span><h2>Active shifts</h2><p>Cash controls stay one click away while the register is open.</p></div>
          <span className="live-counter"><i/>{active.length} live</span>
        </div>
        <div className="active-shift-grid">
          {active.map(shift => <article className="active-shift-card" key={shift.id}>
            <div className="active-shift-top">
              <div className="register-avatar">{shift.registerName.slice(0,2).toUpperCase()}</div>
              <div className="active-shift-title"><span>{shift.registerName}</span><strong>{shift.shiftNumber}</strong><small>Opened by {shift.openedByName}</small></div>
              <span className="premium-status open"><i/> Open</span>
            </div>
            <div className="active-shift-metrics">
              <div><span>Opening float</span><strong>{money.format(shift.openingFloat)}</strong></div>
              <div><span>Opened</span><strong>{dateTime.format(new Date(shift.openedOn))}</strong></div>
            </div>
            <div className="active-shift-actions">
              {canCash && <button onClick={() => showCash(shift)}><ArrowUpFromLine size={15}/> Cash move</button>}
              {canCash && <button onClick={() => showNoSale(shift)}><LockOpen size={15}/> No-sale</button>}
              <button onClick={() => showReport(shift)}><FileText size={15}/> X report</button>
              {canClose && <button className="danger-soft" onClick={() => showReport(shift,'close')}><CheckCircle2 size={15}/> Close</button>}
            </div>
          </article>)}
        </div>
      </section>}

      <section className="premium-panel shift-history-panel">
        <div className="premium-panel-heading history-heading">
          <div><span className="panel-kicker">SHIFT HISTORY</span><h2>Register activity</h2><p>Audit-ready history for the currently selected store.</p></div>
          <div className="history-controls">
            <label className="premium-switch"><input type="checkbox" checked={openOnly} onChange={e => setOpenOnly(e.target.checked)}/><span/><em>Open only</em></label>
            <button className="icon-refresh" onClick={refresh} title="Refresh"><RefreshCw size={16}/></button>
          </div>
        </div>

        <div className="premium-table-wrap">
          <table className="premium-table shift-table">
            <thead><tr><th>Shift</th><th>Register</th><th>Opened</th><th>Cashier</th><th>Status</th><th className="text-right">Float</th><th className="text-right">Variance</th><th/></tr></thead>
            <tbody>
              {rows.map(shift => <tr key={shift.id}>
                <td><strong className="table-primary">{shift.shiftNumber}</strong><small>{shift.storeName}</small></td>
                <td>{shift.registerName}</td>
                <td>{dateTime.format(new Date(shift.openedOn))}</td>
                <td>{shift.openedByName}</td>
                <td><span className={`premium-status ${shift.status === 'Open' ? 'open' : 'closed'}`}><i/>{shift.status}</span></td>
                <td className="text-right">{money.format(shift.openingFloat)}</td>
                <td className={`text-right ${shift.status === 'Closed' && shift.variance !== 0 ? (shift.variance > 0 ? 'value-over' : 'value-short') : ''}`}>{shift.status === 'Closed' ? money.format(shift.variance) : '—'}</td>
                <td className="text-right"><button className="table-action" onClick={() => showReport(shift)}>{shift.status === 'Open' ? 'X report' : 'Z report'}</button></td>
              </tr>)}
              {!shifts.isLoading && !rows.length && <tr><td colSpan={8}><div className="table-empty">No shifts found for this store.</div></td></tr>}
            </tbody>
          </table>
        </div>
      </section>

      {modal === 'open' && <ModalShell kicker="START REGISTER" title="Open a new shift" onClose={resetModal}>
        <div className="modal-callout"><Play size={18}/><div><strong>Ready the register</strong><span>Choose an available register and record the opening cash float.</span></div></div>
        <label className="premium-field"><span>Register</span><select value={registerId} onChange={e => setRegisterId(e.target.value)}>{availableRegisters.map(r => <option key={r.id} value={r.id}>{r.display}</option>)}</select></label>
        <label className="premium-field"><span>Opening float</span><div className="money-input"><em>Rs</em><input type="number" min="0" step="1" value={openingFloat} onChange={e => setOpeningFloat(e.target.value)}/></div><small>Cash already present in the drawer.</small></label>
        <label className="premium-field"><span>Notes <em>optional</em></span><textarea rows={3} value={openingNotes} onChange={e => setOpeningNotes(e.target.value)} placeholder="Opening handover or register notes…"/></label>
        {actionError && <div className="modal-error">{actionError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={resetModal}>Cancel</button><button className="modal-primary" disabled={!registerId || action.isPending} onClick={() => action.mutate(() => shiftsApi.open(storeId, {registerId, openingFloat:Number(openingFloat||0), notes:openingNotes.trim()||null}))}><Play size={15}/>{action.isPending ? 'Opening…' : 'Open shift'}</button></div>
      </ModalShell>}

      {modal === 'cash' && selectedShift && <ModalShell kicker="DRAWER MOVEMENT" title="Cash in / out" onClose={resetModal}>
        <div className="modal-summary-strip"><span>{selectedShift.registerName}</span><strong>{selectedShift.shiftNumber}</strong></div>
        <div className="cash-direction-toggle"><button className={movementType==='CashIn'?'active in':''} onClick={()=>setMovementType('CashIn')}><ArrowDownToLine size={17}/> Cash in</button><button className={movementType==='CashOut'?'active out':''} onClick={()=>setMovementType('CashOut')}><ArrowUpFromLine size={17}/> Cash out</button></div>
        <label className="premium-field"><span>Amount</span><div className="money-input"><em>Rs</em><input autoFocus type="number" min="0.01" step="0.01" value={movementAmount} onChange={e=>setMovementAmount(e.target.value)}/></div></label>
        <label className="premium-field"><span>Reason / notes</span><textarea rows={3} value={movementNotes} onChange={e=>setMovementNotes(e.target.value)} placeholder="Petty cash, bank deposit, change top-up…"/></label>
        {actionError && <div className="modal-error">{actionError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={resetModal}>Cancel</button><button className="modal-primary" disabled={Number(movementAmount)<=0 || action.isPending} onClick={()=>action.mutate(()=>shiftsApi.cashMovement(storeId,{shiftId:selectedShift.id,movementType,amount:Number(movementAmount),notes:movementNotes.trim()||null}))}>Record movement</button></div>
      </ModalShell>}

      {modal === 'nosale' && selectedShift && <ModalShell kicker="AUDITED ACTION" title="No-sale drawer open" onClose={resetModal}>
        <div className="modal-callout warning"><LockOpen size={18}/><div><strong>This action is audited</strong><span>Record why the cash drawer is being opened without a sale.</span></div></div>
        <label className="premium-field"><span>Reason</span><textarea autoFocus rows={4} value={noSaleReason} onChange={e=>setNoSaleReason(e.target.value)} placeholder="Change request, manager inspection, cash check…"/></label>
        {actionError && <div className="modal-error">{actionError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={resetModal}>Cancel</button><button className="modal-primary" disabled={noSaleReason.trim().length<3 || action.isPending} onClick={()=>action.mutate(()=>shiftsApi.noSale(storeId,{shiftId:selectedShift.id,reason:noSaleReason.trim()}))}>Record no-sale</button></div>
      </ModalShell>}

      {modal === 'report' && report && <ModalShell kicker={report.status === 'Open' ? 'X REPORT' : 'Z REPORT'} title={`${report.shiftNumber}`} onClose={resetModal} wide>
        <div className="report-identity"><div><span>Store</span><strong>{report.storeName}</strong></div><div><span>Register</span><strong>{report.registerName}</strong></div><div><span>Opened by</span><strong>{report.openedByName}</strong></div><div><span>Status</span><strong>{report.status}</strong></div></div>
        <div className="report-kpi-grid"><ReportMetric label="Gross sales" value={money.format(report.grossSales)}/><ReportMetric label="Cash sales" value={money.format(report.cashSales)}/><ReportMetric label="Card sales" value={money.format(report.cardSales)}/><ReportMetric label="Credit sales" value={money.format(report.creditSales)}/></div>
        <div className="report-ledger"><div><span>Opening float</span><strong>{money.format(report.openingFloat)}</strong></div><div><span>Cash in</span><strong className="value-positive">+{money.format(report.cashIn)}</strong></div><div><span>Cash out</span><strong className="value-negative">-{money.format(report.cashOut)}</strong></div><div><span>Cash refunds</span><strong>{money.format(report.cashRefunds)}</strong></div><div><span>No-sale drawer opens</span><strong>{report.noSaleDrawerOpenCount}</strong></div><div className="report-ledger-total"><span>Expected cash</span><strong>{money.format(report.expectedCash)}</strong></div>{report.status==='Closed' && <><div><span>Counted cash</span><strong>{money.format(report.countedCash)}</strong></div><div><span>Variance</span><strong className={report.variance===0?'value-positive':report.variance>0?'value-over':'value-short'}>{money.format(report.variance)}</strong></div></>}</div>
        <div className="modal-actions"><button className="modal-primary" onClick={resetModal}>Done</button></div>
      </ModalShell>}

      {modal === 'close' && selectedShift && report && <ModalShell kicker="END OF SHIFT" title={`Close ${selectedShift.shiftNumber}`} onClose={resetModal} wide>
        <div className="close-shift-banner"><div><span>Expected cash</span><strong>{money.format(closeExpected)}</strong></div><div><span>Counted cash</span><strong>{money.format(Number(countedCash||0))}</strong></div><div className={closeVariance===0?'balanced':closeVariance>0?'over':'short'}><span>Variance</span><strong>{money.format(closeVariance)}</strong><small>{closeVariance===0?'Balanced':closeVariance>0?'Over':'Short'}</small></div></div>
        <div className="denomination-section"><div className="denomination-title"><div><strong>Cash denomination count</strong><span>Enter notes and coins. Counted cash updates automatically.</span></div><button onClick={()=>{setDenominations(defaultDenominations.map((v,index)=>({denominationValue:v,quantity:0,sortOrder:index+1})));setCountedCash(String(closeExpected.toFixed(2)))}}>Clear</button></div><div className="denomination-list">{denominations.map((line,index)=><div className="denomination-row" key={line.denominationValue}><strong>{money.format(line.denominationValue)}</strong><div className="denom-qty"><button onClick={()=>updateDenomination(index,line.quantity-1)}>−</button><input type="number" min="0" value={line.quantity} onChange={e=>updateDenomination(index,Number(e.target.value))}/><button onClick={()=>updateDenomination(index,line.quantity+1)}>+</button></div><span>{money.format(line.denominationValue*line.quantity)}</span></div>)}</div><div className="denomination-total"><span>Denomination total</span><strong>{money.format(denominationTotal)}</strong></div></div>
        <label className="premium-field"><span>Counted cash <em>manual override</em></span><div className="money-input"><em>Rs</em><input type="number" min="0" step="0.01" value={countedCash} onChange={e=>setCountedCash(e.target.value)}/></div></label>
        <label className="premium-field"><span>Closing notes <em>optional</em></span><textarea rows={3} value={closingNotes} onChange={e=>setClosingNotes(e.target.value)} placeholder="Handover, variance explanation, closing note…"/></label>
        {actionError && <div className="modal-error">{actionError}</div>}
        <div className="modal-actions"><button className="modal-secondary" onClick={resetModal}>Cancel</button><button className="modal-primary danger" disabled={Number(countedCash)<0 || action.isPending} onClick={()=>action.mutate(()=>shiftsApi.close(storeId,{shiftId:selectedShift.id,countedCash:Number(countedCash||0),denominations:denominations.filter(x=>x.quantity>0),notes:closingNotes.trim()||null}))}><CheckCircle2 size={15}/>{action.isPending?'Closing…':'Close shift'}</button></div>
      </ModalShell>}
    </section>
  );
}

function ModalShell({ kicker, title, children, onClose, wide = false }: { kicker:string; title:string; children:React.ReactNode; onClose:()=>void; wide?:boolean }) {
  return <div className="modal-backdrop premium-modal-backdrop"><div className={`modal-card premium-modal-card ${wide?'wide':''}`}>
    <div className="modal-heading premium-modal-heading"><div><span className="eyebrow">{kicker}</span><h2>{title}</h2></div><button onClick={onClose}><X size={20}/></button></div>
    {children}
  </div></div>;
}

function ReportMetric({label,value}:{label:string;value:string}) {
  return <div className="report-metric"><span>{label}</span><strong>{value}</strong></div>;
}
