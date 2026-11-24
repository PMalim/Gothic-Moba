import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useStore } from '../../state/store';
import mapImage from '../../assets/minimap/map.jpg';
import meta from '../../assets/minimap/map.json';
import { COST, costOf, navGrid, paintStroke, type Stroke } from './navGrid';
import { clearStrokes, closeNav, navStore, paintOnServer, setNavCursor, setNavMode, testRoute, undoStroke, type NavTool } from './navStore';
import { ObjectEditor } from './ObjectEditor';
import { WaypointEditor } from './WaypointEditor';
import './nav.css';

const TOOLS: { id: NavTool; label: string; key: string; hint: string; cost: number }[] = [
  { id: 'path', label: 'Ścieżka', key: '1', hint: 'preferowana trasa (ścieżka w lesie, droga) — NPC wolą nią iść', cost: COST.path },
  { id: 'rough', label: 'Gąszcz', key: '2', hint: 'trudny teren — NPC go omijają, ale przejdą, gdy trzeba', cost: COST.rough },
  { id: 'open', label: 'Otwórz', key: '3', hint: 'udostępnia fioletowe komórki (powierzchnie ze skanu bez dojścia, np. wąski pomost trasy) — łączy je z sąsiadami', cost: COST.open },
  { id: 'blocked', label: 'Blokada', key: '4', hint: 'zakaz wejścia (np. ściana, której skan nie widzi)', cost: COST.blocked },
  { id: 'erase', label: 'Gumka', key: '5', hint: 'zdejmuje malowanie (wraca teren ze skanu)', cost: COST.normal },
  { id: 'test', label: 'Test trasy', key: '6', hint: 'kliknij punkt A, potem B — serwer wyznacza trasę A*', cost: 0 },
];

const COLORS: Record<number, [number, number, number, number]> = {
  [COST.path]: [64, 140, 255, 170],
  [COST.rough]: [150, 95, 40, 170],
  [COST.blocked]: [255, 40, 40, 170],
  [COST.open]: [40, 220, 220, 190],
};

const toU = (x: number) => (meta.maxX - x) / meta.size;
const toV = (z: number) => (z - meta.minZ) / meta.size;
const fromU = (u: number) => meta.maxX - u * meta.size;
const fromV = (v: number) => meta.minZ + v * meta.size;

export function NavEditor() {
  const s = useStore(navStore);
  const grid = useMemo(() => navGrid(), []);
  const stage = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const cost = useRef<Uint8Array>(new Uint8Array(grid.width * grid.height));
  const [view, setView] = useState({ k: 1, x: 0, y: 0 });
  const [size, setSize] = useState(900);
  const [pointer, setPointer] = useState<{ x: number; z: number; px: number; py: number } | null>(null);
  const [from, setFrom] = useState<[number, number] | null>(null);
  const [showScan, setShowScan] = useState(true);
  const drag = useRef<{ mode: 'paint' | 'pan'; pts: number[]; sx: number; sy: number; vx: number; vy: number } | null>(null);

  useEffect(() => {
    if (!s.open) return;
    setNavCursor(true);
    const fit = () => setSize(Math.floor(Math.min(window.innerWidth - 300, window.innerHeight - 24)));
    fit();
    window.addEventListener('resize', fit);
    return () => {
      window.removeEventListener('resize', fit);
      setNavCursor(false);
    };
  }, [s.open]);

  const redraw = useCallback(() => {
    const c = canvas.current;
    if (!c) return;
    const g = c.getContext('2d');
    if (!g) return;
    const image = g.createImageData(grid.width, grid.height);
    const data = image.data;
    for (let j = 0; j < grid.height; j++) {
      for (let i = 0; i < grid.width; i++) {
        const idx = j * grid.width + i;
        const at = (j * grid.width + (grid.width - 1 - i)) * 4;
        const m = grid.mask[idx];
        const k = cost.current[idx];
        let rgba: [number, number, number, number] | null = null;
        const closed = m === 0 && k !== COST.open && k !== COST.blocked;
        if (k === COST.open) { if (grid.floor[idx] !== -32768) rgba = COLORS[k]; }
        else if (k !== 0 && COLORS[k] && !closed) rgba = COLORS[k];
        else if (showScan && closed && grid.floor[idx] !== -32768 && Math.abs(grid.floor[idx]) < 30000) rgba = [170, 90, 230, 70];
        else if (showScan && m === 255) rgba = [60, 210, 90, 70];
        else if (showScan && m !== 0) rgba = [245, 200, 40, 110];
        if (!rgba) continue;
        data[at] = rgba[0];
        data[at + 1] = rgba[1];
        data[at + 2] = rgba[2];
        data[at + 3] = rgba[3];
      }
    }
    g.putImageData(image, 0, 0);
  }, [grid, showScan]);

  useEffect(() => {
    cost.current = costOf(grid, s.strokes);
    redraw();
  }, [s.strokes, grid, redraw, s.open]);

  const toWorld = (clientX: number, clientY: number) => {
    const rect = stage.current!.getBoundingClientRect();
    return { x: fromU((clientX - rect.left) / rect.width), z: fromV((clientY - rect.top) / rect.height) };
  };

  const tool = TOOLS.find((t) => t.id === s.tool)!;

  const onPointerDown = (e: React.PointerEvent) => {
    (e.currentTarget as Element).setPointerCapture?.(e.pointerId);
    if (e.button === 1 || e.button === 2 || e.shiftKey) {
      drag.current = { mode: 'pan', pts: [], sx: e.clientX, sy: e.clientY, vx: view.x, vy: view.y };
      return;
    }
    if (e.button !== 0) return;
    const w = toWorld(e.clientX, e.clientY);
    if (s.tool === 'test') {
      if (!from) setFrom([Math.round(w.x), Math.round(w.z)]);
      else {
        testRoute(from, [Math.round(w.x), Math.round(w.z)]);
        setFrom(null);
      }
      return;
    }
    drag.current = { mode: 'paint', pts: [w.x, w.z], sx: 0, sy: 0, vx: 0, vy: 0 };
    paintStroke(grid, cost.current, { pts: [w.x, w.z], r: s.radius, cost: tool.cost });
    redraw();
  };

  const onPointerMove = (e: React.PointerEvent) => {
    const w = toWorld(e.clientX, e.clientY);
    const box = e.currentTarget.getBoundingClientRect();
    setPointer({ x: w.x, z: w.z, px: e.clientX - box.left, py: e.clientY - box.top });
    const d = drag.current;
    if (!d) return;
    if (d.mode === 'pan') {
      setView((v) => ({ ...v, x: d.vx + e.clientX - d.sx, y: d.vy + e.clientY - d.sy }));
      return;
    }
    const lx = d.pts[d.pts.length - 2];
    const lz = d.pts[d.pts.length - 1];
    if (Math.hypot(w.x - lx, w.z - lz) < Math.max(40, s.radius * 0.25)) return;
    paintStroke(grid, cost.current, { pts: [lx, lz, w.x, w.z], r: s.radius, cost: tool.cost });
    d.pts.push(w.x, w.z);
    redraw();
  };

  const onPointerUp = () => {
    const d = drag.current;
    drag.current = null;
    if (d?.mode !== 'paint') return;
    const chunk = 400;
    const strokes: Stroke[] = [];
    for (let n = 0; n < d.pts.length || n === 0; n += chunk - 2) strokes.push({ pts: d.pts.slice(n, n + chunk), r: s.radius, cost: tool.cost });
    navStore.set((st) => ({ strokes: [...st.strokes, ...strokes] }));
    strokes.forEach(paintOnServer);
  };

  const onWheel = (e: React.WheelEvent) => {
    const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const cx = e.clientX - rect.left;
    const cy = e.clientY - rect.top;
    setView((v) => {
      const k = Math.min(12, Math.max(0.6, v.k * factor));
      const f = k / v.k;
      return { k, x: cx - (cx - v.x) * f, y: cy - (cy - v.y) * f };
    });
  };

  useEffect(() => {
    if (!s.open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'F9') {
        closeNav();
        e.preventDefault();
        return;
      }
      if (navStore.get().mode !== 'grid') return;
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        undoStroke();
        e.preventDefault();
        return;
      }
      const t = TOOLS.find((x) => x.key === e.key);
      if (t) {
        navStore.set({ tool: t.id });
        setFrom(null);
      } else if (e.key === '[') navStore.set((st) => ({ radius: Math.max(50, st.radius - 50) }));
      else if (e.key === ']') navStore.set((st) => ({ radius: Math.min(1000, st.radius + 50) }));
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [s.open]);

  if (!s.open) return null;
  if (s.mode === 'waypoints') return <WaypointEditor />;
  if (s.mode === 'objects') return <ObjectEditor />;

  const u0 = toU(grid.originX + grid.width * grid.cell);
  const v0 = toV(grid.originZ);
  const wu = (grid.width * grid.cell) / meta.size;
  const hv = (grid.height * grid.cell) / meta.size;
  const brush = (s.radius / meta.size) * size * view.k * 2;
  const cellAt = (() => {
    if (!pointer) return null;
    const i = Math.floor((pointer.x - grid.originX) / grid.cell);
    const j = Math.floor((pointer.z - grid.originZ) / grid.cell);
    if (i < 0 || j < 0 || i >= grid.width || j >= grid.height) return null;
    return { m: grid.mask[j * grid.width + i], c: cost.current[j * grid.width + i] };
  })();
  const route = s.route;

  return (
    <div className="nav" role="dialog" aria-label="Edytor siatki nawigacji">
      <aside className="nav__bar">
        <div className="nav__modes">
          <button className="is-active">Siatka</button>
          <button onClick={() => setNavMode('waypoints')}>Waypointy</button>
          <button onClick={() => setNavMode('objects')}>Obiekty</button>
        </div>
        <h2>Nawigacja</h2>
        <p className="nav__note">Siatka ze skanu promieni (komórka {grid.cell} cm). Maluj ścieżki i gąszcz — NPC liczą trasę A* po tej siatce.</p>
        <div className="nav__tools">
          {TOOLS.map((t) => (
            <button
              key={t.id}
              className={`nav__tool nav__tool--${t.id} ${s.tool === t.id ? 'is-active' : ''}`}
              title={t.hint}
              onClick={() => {
                navStore.set({ tool: t.id });
                setFrom(null);
              }}
            >
              <kbd>{t.key}</kbd> {t.label}
            </button>
          ))}
        </div>
        <p className="nav__hint">{tool.hint}</p>
        <label className="nav__field">
          Promień pędzla: <b>{s.radius} cm</b>
          <input type="range" min={50} max={1000} step={50} value={s.radius} onChange={(e) => navStore.set({ radius: Number(e.target.value) })} />
        </label>
        <label className="nav__check">
          <input type="checkbox" checked={showScan} onChange={(e) => setShowScan(e.target.checked)} /> pokaż skan (zielone / żółte / fioletowe)
        </label>
        <div className="nav__row">
          <button className="btn btn--small" onClick={undoStroke} disabled={s.strokes.length === 0}>
            Cofnij (Ctrl+Z)
          </button>
          <button className="btn btn--small" onClick={() => window.confirm('Usunąć wszystkie ręczne poprawki?') && clearStrokes()} disabled={s.strokes.length === 0}>
            Wyczyść
          </button>
        </div>
        <p className="nav__stat">Pociągnięć: {s.strokes.length}</p>
        {route && (
          <p className="nav__stat nav__stat--ok">
            Trasa: {route.length / 2} punktów, {s.routeMs.toFixed(1)} ms
          </p>
        )}
        {s.routeFailed && <p className="nav__stat nav__stat--bad">Brak trasy między tymi punktami.</p>}
        {from && (
          <p className="nav__stat">
            A: {from[0]}, {from[1]} — wybierz B
          </p>
        )}
        <div className="nav__legend">
          <i style={{ background: 'rgb(60,210,90)' }} /> przechodnie <i style={{ background: 'rgb(245,200,40)' }} /> przy ścianie
          <br />
          <i style={{ background: 'rgb(64,140,255)' }} /> ścieżka <i style={{ background: 'rgb(150,95,40)' }} /> gąszcz <i style={{ background: 'rgb(255,40,40)' }} /> blokada <i style={{ background: 'rgb(40,220,220)' }} /> otwarte <i style={{ background: 'rgb(170,90,230)' }} /> do otwarcia
        </div>
        <p className="nav__hint">Kółko myszy — zoom, prawy / środkowy przycisk — przesuwanie, [ ] — promień. F9 / Esc zamyka.</p>
        {pointer && (
          <p className="nav__coords">
            x {Math.round(pointer.x)} z {Math.round(pointer.z)}
            {cellAt ? ` · ${cellAt.m === 0 ? 'zablokowana' : cellAt.m === 255 ? 'przechodnia' : 'przy ścianie'}${cellAt.c ? ` · koszt ${cellAt.c}` : ''}` : ''}
          </p>
        )}
        <button className="btn btn--primary" onClick={closeNav}>
          Zamknij
        </button>
      </aside>

      <div
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
          <canvas
            ref={canvas}
            width={grid.width}
            height={grid.height}
            style={{ left: `${u0 * 100}%`, top: `${v0 * 100}%`, width: `${wu * 100}%`, height: `${hv * 100}%` }}
          />
          <svg viewBox="0 0 1 1" preserveAspectRatio="none">
            {route && (
              <polyline
                points={Array.from({ length: route.length / 2 }, (_, n) => `${toU(route[n * 2])},${toV(route[n * 2 + 1])}`).join(' ')}
                fill="none"
                stroke="#ff4fd8"
                vectorEffect="non-scaling-stroke"
                style={{ strokeWidth: 3 }}
              />
            )}
            {route &&
              Array.from({ length: route.length / 2 }, (_, n) => <circle key={n} cx={toU(route[n * 2])} cy={toV(route[n * 2 + 1])} r={0.0025} fill="#fff" />)}
            {from && <circle cx={toU(from[0])} cy={toV(from[1])} r={0.006} fill="#ff4fd8" />}
          </svg>
        </div>
        {pointer && s.tool !== 'test' && <div className="nav__brush" style={{ left: pointer.px, top: pointer.py, width: brush, height: brush }} />}
      </div>
    </div>
  );
}
