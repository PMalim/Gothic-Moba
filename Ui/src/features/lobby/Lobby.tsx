import { useEffect } from 'react';
import { useStore } from '../../state/store';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { HeroesTab, ShopTab } from './HeroTabs';
import { PlayTab } from './PlayTab';
import { ProfileTab, RankingTab } from './RankingTab';
import { ResultModal } from './ResultModal';
import { Embers, Icon, Sigil } from './ui';
import { act, lobbyStore, type Tab } from './lobbyStore';
import emblem from '../../assets/brand/emblem.svg';
import './gothic.css';
import './lobby.css';

const TABS: { id: Tab; label: string; icon: 'play' | 'heroes' | 'shop' | 'ranking' | 'profile' }[] = [
  { id: 'play', label: 'Gra', icon: 'play' },
  { id: 'heroes', label: 'Bohaterowie', icon: 'heroes' },
  { id: 'shop', label: 'Sklep', icon: 'shop' },
  { id: 'ranking', label: 'Ranking', icon: 'ranking' },
  { id: 'profile', label: 'Profil', icon: 'profile' },
];

export function Lobby() {
  const s = useStore(lobbyStore);

  useEffect(() => {
    if (s.phase !== 'lobby') return;
    const onKey = (e: KeyboardEvent) => {
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLSelectElement) return;
      const index = Number(e.key) - 1;
      if (index >= 0 && index < TABS.length) lobbyStore.set({ tab: TABS[index].id });
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [s.phase]);

  useEffect(() => {
    if (s.tab === 'ranking' || s.tab === 'profile') act('ranking.get');
  }, [s.tab]);

  useEffect(() => {
    if (!s.toast) return;
    const timer = window.setTimeout(() => lobbyStore.set({ toast: null }), 3500);
    return () => window.clearTimeout(timer);
  }, [s.toast]);

  const p = s.profile;
  const face = p?.hero ? PORTRAIT[p.hero]?.face : undefined;
  return (
    <>
      {s.phase === 'lobby' && s.auth.status === 'ok' && (
        <div className="lb" role="application" aria-label="Lobby">
          <div className="lb-bg" aria-hidden>
            <i className="lb-bg__moon" />
            <i className="lb-bg__glow" />
            <i className="lb-bg__fog lb-bg__fog--a" />
            <i className="lb-bg__fog lb-bg__fog--b" />
            <i className="lb-bg__grain" />
            <i className="lb-bg__vignette" />
            <Embers />
          </div>

          <header className="lb-top">
            <div className="lb-brand">
              <img className="lb-brand__emblem" src={emblem} alt="" draggable={false} />
              <div>
                <b>Gothic Moba</b>
                <small>Mroczna Arena</small>
              </div>
            </div>

            <nav className="lb-nav" aria-label="Zakładki">
              {TABS.map((t, i) => (
                <button key={t.id} className={`lb-nav__tab ${s.tab === t.id ? 'is-active' : ''}`} onClick={() => lobbyStore.set({ tab: t.id })}>
                  <Icon name={t.icon} />
                  <span>{t.label}</span>
                  <kbd>{i + 1}</kbd>
                </button>
              ))}
            </nav>

            <div className="lb-me">
              <div className="lb-wallet" title="Punkty na bohaterów i punkty rankingowe">
                <span className="lb-chip lb-chip--coin">
                  <Icon name="coin" size="1.05rem" />
                  <b>{p?.points ?? '—'}</b>
                </span>
                <span className="lb-chip lb-chip--lp">
                  <Icon name="shard" size="1.05rem" />
                  <b>{p?.lp ?? '—'}</b>
                  <small>LP</small>
                </span>
              </div>
              <div className="lb-player">
                <div className="lb-player__text">
                  <b>{p?.name ?? s.auth.name}</b>
                  <small>{p?.rank ? `Miejsce #${p.rank}` : 'Bez rankingu'}</small>
                </div>
                <span className="lb-avatar">{face ? <img src={face} alt="" draggable={false} /> : <Sigil size="100%" />}</span>
              </div>
            </div>
          </header>

          <div className="lb-stage" key={s.tab}>
            {s.tab === 'play' && <PlayTab />}
            {s.tab === 'heroes' && <HeroesTab />}
            {s.tab === 'shop' && <ShopTab />}
            {s.tab === 'ranking' && <RankingTab />}
            {s.tab === 'profile' && <ProfileTab />}
          </div>

          <footer className="lb-foot">
            <span>
              <kbd>1–5</kbd> zakładki
            </span>
            <span>
              <kbd>Esc</kbd> menu
            </span>
          </footer>
        </div>
      )}

      {s.toast && (
        <div key={s.toast.id} className={`gtoast is-${s.toast.kind}`} role="status">
          {s.toast.text}
        </div>
      )}
      <ResultModal />
    </>
  );
}
