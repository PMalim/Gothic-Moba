import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { loadingStore, matchFadeStore } from './loadingStore';
import logo from '../../assets/brand/logo-gold@4x.png';
import './loading.css';

const TIPS = [
  'Fale minionów wychodzą z baz co kilkanaście sekund — idź za własną falą, nie przed nią.',
  'Wieża strzela do tego, kto jest najbliżej. Niech pierwsze ciosy przyjmą miniony.',
  'Dobijaj wrogie miniony, zanim zrobią to wieże: punkty rosną za każde zabójstwo bohatera.',
  'Zdolności Z, X, C mają odnowienie. Rzucaj je w pojedynku, nie na miniony.',
  'Ranny bohater odpoczywa przy bazie — fontanna leczy dużo szybciej niż spacer.',
  'W grze rankingowej wygrana daje LP, przegrana je zabiera. Bez rang, sam licznik.',
];

export function Loading() {
  const s = useStore(loadingStore);
  const [tip, setTip] = useState(() => Math.floor(Math.random() * TIPS.length));
  useEffect(() => {
    if (!s.visible) return;
    const timer = window.setInterval(() => setTip((t) => (t + 1) % TIPS.length), 4200);
    return () => window.clearInterval(timer);
  }, [s.visible]);
  if (!s.visible) return null;
  return (
    <div className={`loading ${s.leaving ? 'is-leaving' : ''}`} role="status" aria-live="polite">
      <div className="loading__center">
        <h1 className="loading__logo">
          <img src={logo} alt="Gothic Moba" draggable={false} />
        </h1>
        <div className="loading__bar" aria-hidden>
          <i style={{ width: `${s.progress}%` }} />
        </div>
        <p className="loading__text">{s.text}</p>
      </div>
      <p className="loading__tip">{TIPS[tip]}</p>
    </div>
  );
}

export function MatchFade() {
  const s = useStore(matchFadeStore);
  if (!s.visible) return null;
  return (
    <div className={`matchfade ${s.leaving ? 'is-leaving' : ''}`} role="status" aria-live="polite">
      <p>Wczytywanie areny…</p>
    </div>
  );
}
