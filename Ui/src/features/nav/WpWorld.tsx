import { useEffect, useRef } from 'react';
import { on } from '../../bridge/bridge';
import type { WpwState } from '../../bridge/protocol';
import { createStore, useStore } from '../../state/store';
import { uiStore } from '../../state/stores';
import { wpRename } from './wpStore';
import './nav.css';
import './wpw.css';

const wpw = createStore<WpwState>({ on: false });
const TOOLS = ['Wybierz / przesuń', 'Rysuj ścieżkę', 'Połącz', 'Usuń'];
const HINTS = [
  'Klik na punkt zaznacza go; przytrzymaj i celuj gdzie indziej, puść = przeniesiony.',
  'Klik na teren dodaje punkt połączony z zaznaczonym (nowy staje się zaznaczony) — rysujesz ścieżkę przez las. Klik na punkt łączy z nim. Prawy przycisk kończy łańcuch.',
  'Klik punkt A, potem B — łączy albo rozłącza; łańcuch idzie dalej od B.',
  'Klik na punkt usuwa go razem z połączeniami.',
];
const STATE_CLASS = ['', 'is-selected', 'is-hover'];

export function wireWpWorld(): void {
  on('wpw.state', (s) => wpw.set({ ...s }));
}

export function WpWorld() {
  const s = useStore(wpw);
  const layer = useRef<HTMLDivElement>(null);
  const pool = useRef<HTMLDivElement[]>([]);

  useEffect(() => {
    on('wpw.labels', ({ l }) => {
      const root = layer.current;
      if (!root) return;
      const k = window.innerWidth / uiStore.get().resX;
      const count = Math.floor(l.length / 4);
      while (pool.current.length < count) {
        const el = document.createElement('div');
        el.className = 'wpw__label';
        root.appendChild(el);
        pool.current.push(el);
      }
      pool.current.forEach((el, i) => {
        if (i >= count) {
          el.style.display = 'none';
          return;
        }
        const name = String(l[i * 4]);
        const x = Number(l[i * 4 + 1]);
        const y = Number(l[i * 4 + 2]);
        const state = Number(l[i * 4 + 3]);
        el.style.display = '';
        el.textContent = name;
        el.className = `wpw__label ${STATE_CLASS[state] ?? ''}`;
        el.style.transform = `translate3d(${(x * k).toFixed(1)}px, ${(y * k).toFixed(1)}px, 0) translate(-50%, -100%)`;
      });
    });
  }, []);

  if (!s.on) return null;
  const tool = Math.max(1, Math.min(4, s.tool ?? 1));
  const selected = (s.id ?? -1) >= 0;

  return (
    <>
      <div ref={layer} className="wpw__layer" aria-hidden="true" />
      <div className="wpw__cross" aria-hidden="true" />
      <aside className="wpw" role="dialog" aria-label="Edytor waypointów w świecie">
        <h2>
          Waypointy <small>F4</small>
        </h2>
        <p className="wpw__stat">
          Punkty: {s.count ?? 0} · połączenia: {s.links ?? 0}
        </p>
        <div className="wpw__tools">
          {TOOLS.map((label, i) => (
            <div key={label} className={`wpw__tool ${tool === i + 1 ? 'is-active' : ''}`}>
              <kbd>{i + 1}</kbd> {label}
            </div>
          ))}
        </div>
        <p className="wpw__hint">{HINTS[tool - 1]}</p>
        {s.hover ? (
          <p className="wpw__hover">
            Pod celownikiem: <b>{s.hover}</b>
          </p>
        ) : null}
        {selected ? (
          <div className="wpw__sel">
            <h3>Zaznaczony</h3>
            {s.panel ? (
              <input
                key={s.id}
                className="wp__input"
                defaultValue={s.name}
                maxLength={40}
                autoFocus
                onKeyDown={(e) => {
                  e.stopPropagation();
                  if (e.key === 'Enter') (e.target as HTMLInputElement).blur();
                }}
                onBlur={(e) => e.target.value !== s.name && wpRename(s.id!, e.target.value)}
              />
            ) : (
              <p className="wpw__name">{s.name}</p>
            )}
            <p className="wpw__stat">
              x {s.x} · y {s.y} · z {s.z}
            </p>
            <p className="wpw__stat">Połączony z: {s.near && s.near.length > 0 ? s.near.join(', ') : '—'}</p>
          </div>
        ) : (
          <p className="wpw__hint">Nic nie zaznaczono.</p>
        )}
        {s.note ? <p className="wpw__note">{s.note}</p> : null}
        <p className="wpw__keys">
          <kbd>Del</kbd> usuń · <kbd>Ctrl+Z</kbd> cofnij · <kbd>Tab</kbd> kursor (zmiana nazwy) · <kbd>Esc</kbd> wstecz · <kbd>F4</kbd> wyjście · lot: <kbd>W A S D</kbd>, <kbd>Spacja</kbd> / <kbd>X</kbd>, <kbd>Shift</kbd>
        </p>
      </aside>
    </>
  );
}
