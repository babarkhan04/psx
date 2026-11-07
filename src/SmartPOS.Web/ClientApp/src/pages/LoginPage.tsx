import { FormEvent, useState } from 'react';
import { ArrowRight, BarChart3, Boxes, LockKeyhole, ScanBarcode, ShieldCheck, Sparkles, UserRound, Zap } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../features/auth/authApi';
import { useAuthStore } from '../features/auth/authStore';
import { useStoreContext } from '../features/shared/storeContext';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const setUser = useAuthStore(s => s.setUser);
  const setStoreId = useStoreContext(s => s.setStoreId);
  const navigate = useNavigate();

  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('');
    try { await authApi.login(email, password); const user = await authApi.me(); setUser(user); setStoreId(user.selectedStoreId ?? user.stores[0]?.id ?? null); navigate('/dashboard', {replace:true}); }
    catch (err) { setError(err instanceof Error ? err.message : 'Sign in failed.'); }
    finally { setBusy(false); }
  }

  return <div className="modern-login-page">
    <section className="login-showcase">
      <div className="login-grid-pattern"/>
      <div className="login-logo"><span><Zap size={20} fill="currentColor"/></span><strong>SmartPOS</strong><small>Retail OS</small></div>
      <div className="showcase-copy"><span className="showcase-kicker"><Sparkles size={14}/> A smarter counter experience</span><h1>Run the store.<br/><em>See everything.</em></h1><p>Checkout, stock, shifts and business intelligence in one clean workspace built for fast retail teams.</p></div>
      <div className="showcase-mini-dashboard"><div className="mini-top"><div><span>Today</span><strong>Retail pulse</strong></div><span className="mini-live"><i/> Live</span></div><div className="mini-cards"><div><BarChart3 size={18}/><span>Sales</span><strong>PKR 248K</strong><small>+12.4%</small></div><div><Boxes size={18}/><span>Stock</span><strong>1,284</strong><small>18 low</small></div></div><div className="mini-chart">{[32,48,40,66,56,82,72,92,76,88,96].map((h,i)=><span key={i} style={{height:`${h}%`}}/>)}</div></div>
      <div className="showcase-features"><span><ScanBarcode size={16}/> Fast barcode checkout</span><span><ShieldCheck size={16}/> Role & store security</span><span><BarChart3 size={16}/> Live owner insights</span></div>
    </section>

    <section className="login-form-side"><form className="modern-login-card" onSubmit={submit}><div className="mobile-login-logo"><span><Zap size={18} fill="currentColor"/></span><strong>SmartPOS</strong></div><span className="form-kicker">Welcome back</span><h2>Sign in to your workspace</h2><p>Use the same account you use in the current SmartPOS system.</p><label>Email<div className="modern-input"><UserRound size={18}/><input type="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@company.com" autoFocus/></div></label><label>Password<div className="modern-input"><LockKeyhole size={18}/><input type="password" value={password} onChange={e=>setPassword(e.target.value)} placeholder="Enter your password"/></div></label>{error && <div className="modern-form-error">{error}</div>}<button className="modern-login-button" disabled={busy}><span>{busy ? 'Signing in…' : 'Sign in to SmartPOS'}</span><ArrowRight size={18}/></button><div className="login-security-note"><ShieldCheck size={15}/><span>Secure sign-in using your existing SmartPOS Identity account.</span></div></form></section>
  </div>;
}
