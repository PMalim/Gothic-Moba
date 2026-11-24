import { useEffect, useMemo, useRef, useState } from 'react';
import { useStore } from '../../state/store';
import mapImage from '../../assets/minimap/map.jpg';
import meta from '../../assets/minimap/map.json';
import { closeNav, navStore, setNavMode } from './navStore';
import { FILTERS, objAddPoint, objAngle, objDeletePoint, objMove, objReset, objStore, passes, type MapObj, type ObjFilter, type ObjTool } from './objStore';
import './nav.css';

const TOOLS: { id: ObjTool; label: string; key: string; hint: string }[] = [
  { id: 'select', label: 'Wybierz / przesuń', key: '1', hint: 'klik zaznacza obiekt, przeciągnięcie go przesuwa — zmiana działa od razu na serwerze (także w trwającej grze)' },
  { id: 'add', label: 'Dodaj punkt ścieżki', key: '2', hint: 'klik na mapie wstawia punkt do najbliższego odcinka trasy (widać tylko ścieżki)' },
  { id: 'delete', label: 'Usuń punkt ścieżki', key: '3', hint: 'klik w punkt trasy usuwa go (spawnów baz nie da się usunąć)' },
];

const toU = (x: number) => (meta.maxX - x) / meta.size;
const toV = (z: number) => (z - meta.minZ) / meta.size;
const fromU = (u: number) => meta.maxX - u * meta.size;
const fromV = (v: number) => meta.minZ + v * meta.size;

const TEAM = ['#5b9bff', '#ff6b6b'];
const LANE_COLORS = ['#f2c14e', '#7bd88f', '#c792ea', '#4fd1c5'];

function segmentDistance(px: number, pz: number, a: MapObj, b: MapObj): number {
  const sx = b.x - a.x;
  const sz = b.z - a.z;
  const len2 = sx * sx + sz * sz || 1;
  const t = Math.max(0, Math.min(1, ((px - a.x) * sx + (pz - a.z) * sz) / len2));
  return Math.hypot(px - (a.x + sx * t), pz - (a.z + sz * t));
}

export function ObjectEditor() {
  const s = useStore(objStore);
  const stage = useRef<HTMLDivElement>(null);
  const viewBox = useRef<HTMLDivElement>(null);
  const [view, setView] = useState({ k: 1, x: 0, y: 0 });
  const [size, setSize] = useState(900);
  const [pointer, setPointer] = useState<{ x: number; z: number } | null>(null);
  const [angle, setAngle] = useState('');
  const drag = useRef<{ mode: 'pan' | 'obj'; id: string; sx: number; sy: number; vx: number; vy: number; moved: boolean } | null>(null);

  const visible = useMemo(() => s.items.filter((o) => passes(o, s.filter)), [s.items, s.filter]);
  const selected = s.items.find((o) => o.id === s.selected) ?? null;
  const lanes = useMemo(() => {
    const map = new Map<string, MapObj[]>();
    for (const o of s.items) if (o.k === 'point') (map.get(o.lane) ?? map.set(o.lane, []).get(o.lane)!).push(o);
    for (const list of map.values()) list.sort((a, b) => (a.i ?? 0) - (b.i ?? 0));
    return [...map.entries()];
  }, [s.items]);
  const showPaths = s.filter === 'all' || s.filter === 'path';

  useEffect(() => setAngle(selected && selected.k !== 'point' ? String(selected.a) : ''), [selected?.id, selected?.a, selected?.k]);

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

  const hitObject = (x: number, z: number): MapObj | null => {
    const reach = (14 / (size * view.k)) * meta.size;
    let best: MapObj | null = null;
    let bestD = reach;
    for (const o of visible) {
      const d = Math.hypot(o.x - x, o.z - z) * (o.k === 'point' ? 1.35 : 1);
      if (d < bestD) { bestD = d; best = o; }
    }
    return best;
  };

  const center = (o: MapObj) => {
    const rect = viewBox.current?.getBoundingClientRect();
    if (!rect) return;
    const k = Math.max(view.k, 3);
    setView({ k, x: rect.width / 2 - toU(o.x) * size * k, y: rect.height / 2 - toV(o.z) * size * k });
  };

  const setFilter = (filter: ObjFilter) => objStore.set({ filter, selected: selected && passes(selected, filter) ? selected.id : '' });

  const setTool = (tool: ObjTool) => {
    if (tool !== 'select' && !(s.filter === 'all' || s.filter === 'path')) objStore.set({ tool, filter: 'path' });
    else objStore.set({ tool });
  };

  const onPointerDown = (e: React.PointerEvent) => {
    (e.currentTarget as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 1 || e.button === 2 || e.shiftKey) {
      drag.current = { mode: 'pan', id: '', sx: e.clientX, sy: e.clientY, vx: view.x, vy: view.y, moved: false };
      return;
    }
    if (e.button !== 0) return;
    const w = toWorld(e.clientX, e.clientY);
    const hit = hitObject(w.x, w.z);
    switch (s.tool) {
      case 'select':
        objStore.set({ selected: hit ? hit.id : '' });
        if (hit) drag.current = { mode: 'obj', id: hit.id, sx: e.clientX, sy: e.clientY, vx: 0, vy: 0, moved: false };
        break;
      case 'add': {
        if (hit) { objStore.set({ selected: hit.id }); break; }
        let best: { lane: string; after: number; d: number } | null = null;
        for (const [lane, pts] of lanes)
          for (let n = 0; n + 1 < pts.length; n++) {
            const d = segmentDistance(w.x, w.z, pts[n], pts[n + 1]);
            if (!best || d < best.d) best = { lane, after: pts[n].i ?? n, d };
          }
        if (best) objAddPoint(best.lane, best.after, w.x, w.z);
        break;
      }
      case 'delete':
        if (hit?.k === 'point') objDeletePoint(hit.id);
        break;
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
    objStore.set((st) => ({ items: st.items.map((o) => (o.id === d.id ? { ...o, x: Math.round(w.x), z: Math.round(w.z) } : o)) }));
  };

  const onPointerUp = () => {
    const d = drag.current;
    drag.current = null;
    if (d?.mode === 'obj' && d.moved) {
      const o = objStore.get().items.find((x) => x.id === d.id);
      if (o) objMove(o.id, o.x, o.z);
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
      if (navStore.get().mode !== 'objects') return;
      if ((e.target as HTMLElement | null)?.tagName === 'INPUT') return;
      if (e.key === 'F9') { closeNav(); e.preventDefault(); return; }
      const t = TOOLS.find((x) => x.key === e.key);
      if (t) { setTool(t.id); return; }
      const cur = objStore.get();
      const sel = cur.items.find((o) => o.id === cur.selected);
      if ((e.key === 'Delete' || e.key === 'Backspace') && sel?.k === 'point') { objDeletePoint(sel.id); e.preventDefault(); }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const matches = useMemo(() => {
    const q = s.search.trim().toLowerCase();
    return q ? s.items.filter((o) => o.n.toLowerCase().includes(q)).slice(0, 14) : [];
  }, [s.search, s.items]);

  const tool = TOOLS.find((t) => t.id === s.tool)!;
  const px = 1 / (size * view.k);
  const labelAll = view.k >= 2.2;
  const counts = useMemo(() => ({ turrets: s.items.filter((o) => o.k === 'turret').length, inhibitors: s.items.filter((o) => o.k === 'inhibitor').length, points: s.items.filter((o) => o.k === 'point').length }), [s.items]);

  const commitAngle = () => {
    if (!selected || selected.k === 'point') return;
    const v = Number(angle);
    if (Number.isFinite(v) && Math.round(v) !== selected.a) objAngle(selected.id, v);
    else setAngle(String(selected.a));
  };

  const marker = (o: MapObj) => {
    const cx = toU(o.x);
    const cy = toV(o.z);
    const sel = o.id === s.selected;
    const color = o.k === 'point' ? '#ffffff' : TEAM[o.team] ?? '#ffffff';
    const stroke = sel ? '#ff4fd8' : o.c ? '#ff9a3c' : '#000';
    const sw = px * (sel || o.c ? 2.4 : 1);
    switch (o.k) {
      case 'turret':
        return <circle cx={cx} cy={cy} r={px * (sel ? 9 : 7)} fill={color} stroke={stroke} strokeWidth={sw} />;
      case 'inhibitor': {
        const r = px * (sel ? 10 : 8);
        return <polygon points={`${cx},${cy - r} ${cx + r},${cy} ${cx},${cy + r} ${cx - r},${cy}`} fill={color} stroke={stroke} strokeWidth={sw} />;
      }
      case 'nexus': {
        const r = px * (sel ? 11 : 9);
        return <rect x={cx - r} y={cy - r} width={r * 2} height={r * 2} fill={color} stroke={stroke} strokeWidth={sw} />;
      }
      case 'dragon': {
        const r = px * (sel ? 10 : 8);
        return <polygon points={`${cx},${cy - r} ${cx + r},${cy + r * 0.8} ${cx - r},${cy + r * 0.8}`} fill={color} stroke={stroke} strokeWidth={sw} />;
      }
      default: {
        const lane = lanes.findIndex(([name]) => name === o.lane);
        return <circle cx={cx} cy={cy} r={px * (sel ? 7 : o.last || o.i === 0 ? 6 : 4.5)} fill={sel ? '#ff4fd8' : LANE_COLORS[Math.max(0, lane) % LANE_COLORS.length]} stroke={o.c && !sel ? '#ff9a3c' : '#000'} strokeWidth={sw} />;
      }
    }
  };

  return (
    <div className="nav" role="dialog" aria-label="Edytor obiektów">
      <aside className="nav__bar">
        <div className="nav__modes">
          <button onClick={() => setNavMode('grid')}>Siatka</button>
          <button onClick={() => setNavMode('waypoints')}>Waypointy</button>
          <button className="is-active">Obiekty</button>
        </div>
        <h2>Obiekty</h2>
        <p className="nav__note">Wieże ({counts.turrets}), inhibitory ({counts.inhibitors}), nexusy, głowy smoków i punkty ścieżek ({counts.points}). Zmiany działają od razu na serwerze i zostają zapisane.</p>

        <div className="obj__filters">
          {FILTERS.map((f) => (
            <button key={f.id} className={`nav__tool ${s.filter === f.id ? 'is-active' : ''}`} onClick={() => setFilter(f.id)}>
              {f.label}
            </button>
          ))}
        </div>

        <div className="nav__tools">
          {TOOLS.map((t) => (
            <button key={t.id} className={`nav__tool nav__tool--test ${s.tool === t.id ? 'is-active' : ''}`} title={t.hint} onClick={() => setTool(t.id)}>
              <kbd>{t.key}</kbd> {t.label}
            </button>
          ))}
        </div>
        <p className="nav__hint">{tool.hint}</p>

        <label className="nav__field">
          Szukaj obiektu
          <input className="wp__input" value={s.search} placeholder="np. Wieża 2, Inhibitor, Dolna…" onChange={(e) => objStore.set({ search: e.target.value })} />
        </label>
        {matches.length > 0 && (
          <ul className="wp__list">
            {matches.map((o) => (
              <li key={o.id}>
                <button onClick={() => { objStore.set({ selected: o.id, filter: passes(o, s.filter) ? s.filter : 'all' }); center(o); }}>{o.n}</button>
              </li>
            ))}
          </ul>
        )}

        {selected ? (
          <div className="wp__sel">
            <h3>{selected.n}{selected.c ? ' *' : ''}</h3>
            <p className="nav__coords">x {selected.x} · y {selected.y} · z {selected.z}</p>
            {selected.k !== 'point' && (
              <label className="nav__field">
                Kąt (stopnie)
                <div className="nav__row">
                  <button className="btn btn--small" onClick={() => objAngle(selected.id, selected.a - 15)}>−15°</button>
                  <input
                    className="wp__input"
                    value={angle}
                    inputMode="numeric"
                    onChange={(e) => setAngle(e.target.value)}
                    onBlur={commitAngle}
                    onKeyDown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); e.stopPropagation(); }}
                  />
                  <button className="btn btn--small" onClick={() => objAngle(selected.id, selected.a + 15)}>+15°</button>
                </div>
              </label>
            )}
            <div className="nav__row">
              {selected.k === 'point' && !selected.last && selected.i !== 0 && (
                <button className="btn btn--small" onClick={() => objDeletePoint(selected.id)}>Usuń punkt (Del)</button>
              )}
              <button
                className="btn btn--small"
                disabled={!selected.c}
                title={selected.k === 'point' ? 'Przywraca całą trasę tej linii do domyślnej' : 'Przywraca miejsce i kąt z kodu'}
                onClick={() => window.confirm(selected.k === 'point' ? `Przywrócić domyślną trasę linii „${selected.lane}”?` : 'Przywrócić domyślne miejsce i kąt?') && objReset(selected.id)}
              >
                Przywróć domyślne
              </button>
            </div>
          </div>
        ) : (
          <p className="nav__hint">Nic nie zaznaczono.</p>
        )}

        <div className="nav__legend">
          <i style={{ background: TEAM[0] }} /> niebiescy <i style={{ background: TEAM[1] }} /> czerwoni <i style={{ background: '#ff9a3c' }} /> zmienione (*)
        </div>
        <p className="nav__hint">● wieża · ◆ inhibitor · ■ nexus · ▲ głowa smoka · kropki połączone linią — ścieżka linii (od bazy niebieskich).</p>
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
            {showPaths &&
              lanes.map(([name, pts], laneIndex) => (
                <polyline
                  key={name}
                  points={pts.map((p) => `${toU(p.x)},${toV(p.z)}`).join(' ')}
                  fill="none"
                  stroke={LANE_COLORS[laneIndex % LANE_COLORS.length]}
                  strokeOpacity={0.8}
                  vectorEffect="non-scaling-stroke"
                  style={{ strokeWidth: 2 }}
                />
              ))}
            {visible.map((o) => (
              <g key={o.id}>
                {marker(o)}
                {(labelAll || o.id === s.selected) && o.k !== 'point' && (
                  <text x={toU(o.x) + px * 10} y={toV(o.z) - px * 8} fontSize={px * 11} fill="#fff" stroke="#000" strokeWidth={px * 2.5} paintOrder="stroke">
                    {o.n}
                  </text>
                )}
                {(o.id === s.selected || (labelAll && o.k === 'point')) && o.k === 'point' && (
                  <text x={toU(o.x) + px * 8} y={toV(o.z) - px * 6} fontSize={px * 10} fill="#fff" stroke="#000" strokeWidth={px * 2.5} paintOrder="stroke">
                    {(o.i ?? 0) + 1}
                  </text>
                )}
              </g>
            ))}
          </svg>
        </div>
      </div>
    </div>
  );
}
