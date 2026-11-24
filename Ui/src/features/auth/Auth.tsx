import { FormEvent, useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { act, lobbyStore } from '../lobby/lobbyStore';
import { loadingStore } from '../loading/loadingStore';
import logo from '../../assets/brand/logo-transparent@4x.png';
import './auth.css';

const KEY = 'botarena.user';
const readUser = (): string => {
  try {
    return window.localStorage.getItem(KEY) ?? '';
  } catch {
    return '';
  }
};
const saveUser = (name: string): void => {
  try {
    window.localStorage.setItem(KEY, name);
  } catch {
  }
};

export function Auth() {
  const { auth } = useStore(lobbyStore);
  const loading = useStore(loadingStore);
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [user, setUser] = useState(readUser);
  const [pass, setPass] = useState('');
  const [again, setAgain] = useState('');
  const [local, setLocal] = useState('');

  useEffect(() => {
    if (!auth.busy) return;
    const timer = window.setTimeout(() => lobbyStore.set((s) => ({ auth: { ...s.auth, busy: false, error: 'Brak odpowiedzi serwera.' } })), 10_000);
    return () => window.clearTimeout(timer);
  }, [auth.busy]);

  if (auth.status !== 'login' || loading.visible) return null;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (auth.busy) return;
    const name = user.trim();
    if (name.length < 3) return setLocal('Nazwa ma co najmniej 3 znaki.');
    if (pass.length < 6) return setLocal('Hasło ma co najmniej 6 znaków.');
    if (mode === 'register' && pass !== again) return setLocal('Hasła się różnią.');
    setLocal('');
    saveUser(name);
    lobbyStore.set((s) => ({ auth: { ...s.auth, busy: true, error: '' } }));
    act(mode === 'login' ? 'auth.login' : 'auth.register', { user: name, pass });
    setPass('');
    setAgain('');
  };

  const error = local || auth.error;
  return (
    <div className="auth" role="dialog" aria-label={mode === 'login' ? 'Logowanie' : 'Rejestracja'}>
      <form className="parch auth__panel" onSubmit={submit} autoComplete="off">
        <h1 className="auth__logo">
          <img src={logo} alt="Gothic Moba" draggable={false} />
        </h1>
        <div className="auth__tabs" role="tablist">
          <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'is-active' : ''} onClick={() => (setMode('login'), setLocal(''))}>
            Logowanie
          </button>
          <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'is-active' : ''} onClick={() => (setMode('register'), setLocal(''))}>
            Rejestracja
          </button>
        </div>

        <label className="auth__field">
          <span>Nazwa gracza</span>
          <input value={user} onChange={(e) => setUser(e.target.value)} maxLength={20} autoFocus={user === ''} spellCheck={false} />
        </label>
        <label className="auth__field">
          <span>Hasło</span>
          <input type="password" value={pass} onChange={(e) => setPass(e.target.value)} maxLength={64} autoFocus={user !== ''} />
        </label>
        {mode === 'register' && (
          <label className="auth__field">
            <span>Powtórz hasło</span>
            <input type="password" value={again} onChange={(e) => setAgain(e.target.value)} maxLength={64} />
          </label>
        )}

        <div className="auth__error" role="alert">
          {error}
        </div>
        <button className="pbtn auth__submit" type="submit" disabled={auth.busy}>
          {auth.busy ? 'Chwileczkę…' : mode === 'login' ? 'Zaloguj' : 'Załóż konto'}
        </button>
        <p className="auth__hint">
          {mode === 'login'
            ? 'Nie masz konta? Zarejestruj się — punkty, bohaterowie i ranking zostaną przy koncie.'
            : 'Nazwa: 3–20 znaków (litery, cyfry, _ i -). Hasło: co najmniej 6 znaków. Stary profil z tego komputera zostanie przejęty przez nowe konto.'}
        </p>
      </form>
    </div>
  );
}
