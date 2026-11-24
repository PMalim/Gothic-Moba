import { useEffect, useMemo, useRef, useState } from 'react';
import { useStore } from '../../state/store';
import mapImage from '../../assets/minimap/map.jpg';
import meta from '../../assets/minimap/map.json';
import { closeNav, navStore, setNavMode } from './navStore';
import { wpAdd, wpDelete, wpGenerate, wpLink, wpMove, wpRename, wpRoute, wpStore, wpUndo, type WpNode, type WpTool } from './wpStore';
import './nav.css';

const TOOLS: { id: WpTool; label: string; key: string; hint: string }[] = [
  { id: 'select', label: 'Wybierz / przesuń', key: '1', hint: 'klik zaznacza punkt, przeciągnięcie go przesuwa' },
  { id: 'add', label: 'Dodaj', key: '2', hint: 'klik w wolne miejsce dodaje punkt (wysokość z siatki), klik w punkt — zaznacza' },
  { id: 'link', label: 'Połącz', key: '3', hint: 'klik punkt A, potem B — łączy albo rozłącza; ciągnie łańcuch dalej od B (rysowanie ścieżki)' },
  { id: 'delete', label: 'Usuń', key: '4', hint: 'klik w punkt usuwa go razem z połączeniami (Ctrl+Z cofa)' },
  { id: 'test', label: 'Test trasy', key: '5', hint: 'klik punkt A, potem B — serwer wyznacza trasę po grafie, tak jak NPC' },
];

const toU = (x: number) => (meta.maxX - x) / meta.size;
const toV = (z: number) => (z - meta.minZ) / meta.size;
const fromU = (u: number) => meta.maxX - u * meta.size;
const fromV = (v: number) => meta.minZ + v * meta.size;

function colorOf(name: string): string {
  if (name.startsWith('Teren-')) return '#7bd88f';
  if (/^[^-]+-(Start-|P\d|\d)/.test(name)) return '#6aa8ff';
  return '#f2c14e';
}

export function WaypointEditor() {
  const s = useStore(wpStore);
  const stage = useRef<HTMLDivElement>(null);
  const viewBox = useRef<HTMLDivElement>(null);
  const [view, setView] = useState({ k: 1, x: 0, y: 0 });
  const [size, setSize] = useState(900);
  const [pointer, setPointer] = useState<{ x: number; z: number } | null>(null);
  const [from, setFrom] = useState<[number, number] | null>(null);
  const [name, setName] = useState('');
  const drag = useRef<{ mode: 'pan' | 'node'; id: number; sx: number; sy: number; vx: number; vy: number; moved: boolean } | null>(null);

  const byId = useMemo(() => new Map(s.nodes.map((n) => [n.id, n])), [s.nodes]);
  const selected = byId.get(s.selected) ?? null;
  const neighbors = useMemo(
    () => s.links.filter(([a, b]) => a === s.selected || b === s.selected).map(([a, b]) => byId.get(a === s.selected ? b : a)).filter((n): n is WpNode => !!n),
    [s.links, s.selected, byId],
  );

  useEffect(() => setName(selected?.n ?? ''), [selected?.id, selected?.n]);

  useEffect(() => {
    const fit = () => setSize(Math.floor(Math.min(window.innerWidth - 340, window.innerHeight - 24)));
    fit();
    window.addEventListener('resize', fit);
    return () => window.removeEventListener('resize', fit);
  }, []);

  const toWorld = (clientX: number, clientY: number) => {
    const rect = stage.current!.getBoundingClientRect();
    return { x: fromU((clientX - rect.left) / rect.width), z: fromV((clientY - rect.top) / rect.height) };
  };

  const hitNode = (x: number, z: number): WpNode | null => {
    const reach = (12 / (size * view.k)) * meta.size;
    let best: WpNode | null = null;
    let bestD = reach;
    for (const n of s.nodes) {
      const d = Math.hypot(n.x - x, n.z - z);
      if (d < bestD) { bestD = d; best = n; }
    }
    return best;
  };

  const center = (n: WpNode) => {
    const rect = viewBox.current?.getBoundingClientRect();
    if (!rect) return;
    const k = Math.max(view.k, 3);
    setView({ k, x: rect.width / 2 - toU(n.x) * size * k, y: rect.height / 2 - toV(n.z) * size * k });
  };

  const onPointerDown = (e: React.PointerEvent) => {
    (e.currentTarget as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 1 || e.button === 2 || e.shiftKey) {
      drag.current = { mode: 'pan', id: -1, sx: e.clientX, sy: e.clientY, vx: view.x, vy: view.y, moved: false };
      return;
    }
    if (e.button !== 0) return;
    const w = toWorld(e.clientX, e.clientY);
    const hit = hitNode(w.x, w.z);
    switch (s.tool) {
      case 'select':
        wpStore.set({ selected: hit ? hit.id : -1 });
        if (hit) drag.current = { mode: 'node', id: hit.id, sx: e.clientX, sy: e.clientY, vx: 0, vy: 0, moved: false };
        break;
      case 'add':
        if (hit) wpStore.set({ selected: hit.id });
        else wpAdd(w.x, w.z);
        break;
      case 'link':
        if (!hit) break;
        if (s.linkFrom >= 0 && s.linkFrom !== hit.id) wpLink(s.linkFrom, hit.id);
        wpStore.set({ linkFrom: hit.id, selected: hit.id });
        break;
      case 'delete':
        if (hit) wpDelete(hit.id);
        break;
      case 'test': {
        const p: [number, number] = [Math.round(w.x), Math.round(w.z)];
        if (!from) setFrom(p);
        else {
          wpRoute(from, p);
          setFrom(null);
        }
        break;
      }
    }
  };

  const onPointerMove = (e: React.PointerEvent) => {
    const w = toWorld(e.clientX, e.clientY);
    setPointer({ x: w.x, z: w.z });
    const d = drag.current;
    if (!d) return;
    if (d.mode === 'pan') {
      setView((v) => ({ ...v, x: d.vx + e.clientX - d.sx, y: d.vy + e.clientY - d.sy }));
      return;
    }
    if (!d.moved && Math.hypot(e.clientX - d.sx, e.clientY - d.sy) < 4) return;
    d.moved = true;
    wpStore.set((st) => ({ nodes: st.nodes.map((n) => (n.id === d.id ? { ...n, x: Math.round(w.x), z: Math.round(w.z) } : n)) }));
  };

  const onPointerUp = () => {
    const d = drag.current;
    drag.current = null;
    if (d?.mode === 'node' && d.moved) {
      const n = wpStore.get().nodes.find((x) => x.id === d.id);
      if (n) wpMove(n.id, n.x, n.z);
    }
  };

  const onWheel = (e: React.WheelEvent) => {
    const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const cx = e.clientX - rect.left;
    const cy = e.clientY - rect.top;
    setView((v) => {
      const k = Math.min(16, Math.max(0.6, v.k * factor));
      const f = k / v.k;
      return { k, x: cx - (cx - v.x) * f, y: cy - (cy - v.y) * f };
    });
  };

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (navStore.get().mode !== 'waypoints') return;
      if ((e.target as HTMLElement | null)?.tagName === 'INPUT') return;
      if (e.key === 'F9') { closeNav(); e.preventDefault(); return; }
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { wpUndo(); e.preventDefault(); return; }
      const t = TOOLS.find((x) => x.key === e.key);
      if (t) { wpStore.set({ tool: t.id, linkFrom: -1 }); setFrom(null); return; }
      if ((e.key === 'Delete' || e.key === 'Backspace') && wpStore.get().selected >= 0) { wpDelete(wpStore.get().selected); e.preventDefault(); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const matches = useMemo(() => {
    const q = s.filter.trim().toLowerCase();
    return q ? s.nodes.filter((n) => n.n.toLowerCase().includes(q)).slice(0, 14) : [];
  }, [s.filter, s.nodes]);

  const tool = TOOLS.find((t) => t.id === s.tool)!;
  const px = 1 / (size * view.k);
  const labelAll = view.k >= 2.4;
  const route = s.route;
  const near = new Set(neighbors.map((n) => n.id));

  return (
    <div className="nav" role="dialog" aria-label="Edytor waypointów">
      <aside className="nav__bar">
        <div className="nav__modes">
          <button onClick={() => navStore.set({ mode: 'grid' })}>Siatka</button>
          <button className="is-active">Waypointy</button>
          <button onClick={() => setNavMode('objects')}>Obiekty</button>
        </div>
        <h2>Waypointy</h2>
        <p className="nav__note">Graf nazwanych punktów z połączeniami — po nim chodzą miniony i boty. Punkty: {s.nodes.length}, połączenia: {s.links.length}.</p>
        <div className="nav__tools">
          {TOOLS.map((t) => (
            <button key={t.id} className={`nav__tool nav__tool--test ${s.tool === t.id ? 'is-active' : ''}`} title={t.hint} onClick={() => { wpStore.set({ tool: t.id, linkFrom: -1 }); setFrom(null); }}>
              <kbd>{t.key}</kbd> {t.label}
            </button>
          ))}
        </div>
        <p className="nav__hint">{tool.hint}</p>

        <label className="nav__field">
          Szukaj punktu
          <input className="wp__input" value={s.filter} placeholder="np. Dolna, Teren-12…" onChange={(e) => wpStore.set({ filter: e.target.value })} />
        </label>
        {matches.length > 0 && (
          <ul className="wp__list">
            {matches.map((n) => (
              <li key={n.id}>
                <button onClick={() => { wpStore.set({ selected: n.id }); center(n); }}>{n.n}</button>
              </li>
            ))}
          </ul>
        )}

        {selected ? (
          <div className="wp__sel">
            <h3>Zaznaczony</h3>
            <input
              className="wp__input"
              value={name}
              maxLength={40}
              onChange={(e) => setName(e.target.value)}
              onBlur={() => name !== selected.n && wpRename(selected.id, name)}
              onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); e.stopPropagation(); }}
            />
            <p className="nav__coords">x {selected.x} · y {selected.y} · z {selected.z}</p>
            <p className="nav__hint">Połączony z ({neighbors.length}):</p>
            <ul className="wp__list">
              {neighbors.map((n) => (
                <li key={n.id}>
                  <button onClick={() => wpStore.set({ selected: n.id })}>{n.n}</button>
                  <button className="wp__x" title="Rozłącz" onClick={() => wpLink(selected.id, n.id)}>✕</button>
                </li>
              ))}
            </ul>
            <button className="btn btn--small" onClick={() => wpDelete(selected.id)}>Usuń punkt (Del)</button>
          </div>
        ) : (
          <p className="nav__hint">Nic nie zaznaczono.</p>
        )}

        <div className="nav__row">
          <button className="btn btn--small" onClick={wpUndo}>Cofnij (Ctrl+Z)</button>
          <button className="btn btn--small" onClick={() => window.confirm('Wygenerować graf od nowa ze skanu? Obecny zostanie zastąpiony (Ctrl+Z cofa).') && wpGenerate()}>Generuj ze skanu</button>
        </div>
        {route && <p className="nav__stat nav__stat--ok">Trasa: {route.length / 2} punktów, {s.routeMs.toFixed(1)} ms</p>}
        {s.routeFailed && <p className="nav__stat nav__stat--bad">Brak trasy (punkty niepołączone albo poza przechodnim terenem).</p>}
        {from && <p className="nav__stat">A: {from[0]}, {from[1]} — wybierz B</p>}
        <div className="nav__legend">
          <i style={{ background: '#6aa8ff' }} /> linia <i style={{ background: '#7bd88f' }} /> teren <i style={{ background: '#f2c14e' }} /> własne
        </div>
        <p className="nav__hint">Kółko — zoom, prawy / środkowy przycisk — przesuwanie. Nazwy widać po przybliżeniu. F9 / Esc zamyka.</p>
        {pointer && <p className="nav__coords">x {Math.round(pointer.x)} z {Math.round(pointer.z)}</p>}
        <button className="btn btn--primary" onClick={closeNav}>Zamknij</button>
      </aside>

      <div
        ref={viewBox}
        className="nav__view"
        onWheel={onWheel}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerLeave={() => setPointer(null)}
        onContextMenu={(e) => e.preventDefault()}
      >
        <div ref={stage} className="nav__stage" style={{ width: size, height: size, transform: `translate(${view.x}px, ${view.y}px) scale(${view.k})` }}>
          <img src={mapImage} alt="" draggable={false} />
          <svg viewBox="0 0 1 1" preserveAspectRatio="none">
            {s.links.map(([a, b]) => {
              const na = byId.get(a);
              const nb = byId.get(b);
              if (!na || !nb) return null;
              const hot = a === s.selected || b === s.selected;
              return <line key={`${a}-${b}`} x1={toU(na.x)} y1={toV(na.z)} x2={toU(nb.x)} y2={toV(nb.z)} stroke={hot ? '#ffffff' : 'rgba(255,255,255,0.45)'} vectorEffect="non-scaling-stroke" style={{ strokeWidth: hot ? 2 : 1 }} />;
            })}
            {route && (
              <polyline points={Array.from({ length: route.length / 2 }, (_, n) => `${toU(route[n * 2])},${toV(route[n * 2 + 1])}`).join(' ')} fill="none" stroke="#ff4fd8" vectorEffect="non-scaling-stroke" style={{ strokeWidth: 3 }} />
            )}
            {s.nodes.map((n) => {
              const sel = n.id === s.selected;
              const isFrom = n.id === s.linkFrom;
              return (
                <g key={n.id}>
                  <circle cx={toU(n.x)} cy={toV(n.z)} r={px * (sel || isFrom ? 7 : 4.5)} fill={isFrom ? '#ff9a3c' : sel ? '#ff4fd8' : colorOf(n.n)} stroke="#000" strokeWidth={px} />
                  {(labelAll || sel || near.has(n.id)) && (
                    <text x={toU(n.x) + px * 8} y={toV(n.z) - px * 6} fontSize={px * 11} fill="#fff" stroke="#000" strokeWidth={px * 2.5} paintOrder="stroke">
                      {n.n}
                    </text>
                  )}
                </g>
              );
            })}
            {from && <circle cx={toU(from[0])} cy={toV(from[1])} r={px * 7} fill="#ff4fd8" />}
          </svg>
        </div>
      </div>
    </div>
  );
}
