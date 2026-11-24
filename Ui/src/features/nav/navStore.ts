import { on, send } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';
import type { Stroke } from './navGrid';
import { objOpen, wireObjects } from './objStore';
import { wireWaypoints, wpOpen } from './wpStore';

export type NavTool = 'path' | 'rough' | 'open' | 'blocked' | 'erase' | 'test';

export const navStore = createStore({
  open: false,
  strokes: [] as Stroke[],
  mode: 'grid' as 'grid' | 'waypoints' | 'objects',
  tool: 'path' as NavTool,
  radius: 200,
  route: null as number[] | null,
  routeMs: 0,
  routeFailed: false,
});

export function closeNav(): void {
  if (!navStore.get().open) return;
  navStore.set({ open: false });
  act('nav.close');
}

export const paintOnServer = (stroke: Stroke): void => act('nav.paint', { pts: stroke.pts.map((v) => Math.round(v)), r: stroke.r, cost: stroke.cost });
export const undoStroke = (): void => act('nav.undo');
export const clearStrokes = (): void => act('nav.clear');
export const testRoute = (a: [number, number], b: [number, number]): void => act('nav.route', { a, b });

export function setNavMode(mode: 'grid' | 'waypoints' | 'objects'): void {
  navStore.set({ mode });
  if (mode === 'waypoints') wpOpen();
  else if (mode === 'objects') objOpen();
}

export function wireNav(): void {
  wireWaypoints();
  wireObjects();
  on('srv', ({ name, data }) => {
    if (name === 'nav') {
      const d = data as { strokes: Stroke[] };
      navStore.set({ open: true, strokes: d.strokes ?? [], route: null, routeFailed: false });
      if (navStore.get().mode === 'waypoints') wpOpen();
      else if (navStore.get().mode === 'objects') objOpen();
    } else if (name === 'nav.strokes') {
      navStore.set({ strokes: (data as { strokes: Stroke[] }).strokes ?? [] });
    } else if (name === 'nav.route') {
      const d = data as { points: number[] | null; ms: number };
      navStore.set({ route: d.points, routeMs: d.ms, routeFailed: !d.points });
    }
  });
  on('nav.close', () => navStore.set({ open: false }));
}

export const setNavCursor = (open: boolean): void => send('nav.cursor', open);
