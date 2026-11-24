import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';

export type ObjFilter = 'all' | 'turret' | 'inhibitor' | 'base' | 'path';
export type ObjTool = 'select' | 'add' | 'delete';

export interface MapObj {
  id: string;
  k: 'turret' | 'inhibitor' | 'nexus' | 'dragon' | 'point';
  n: string;
  lane: string;
  team: number;
  x: number;
  y: number;
  z: number;
  a: number;
  t: number;
  c: boolean;
  i?: number;
  last?: boolean;
}

export const objStore = createStore({
  items: [] as MapObj[],
  selected: '',
  filter: 'all' as ObjFilter,
  tool: 'select' as ObjTool,
  search: '',
});

export const FILTERS: { id: ObjFilter; label: string }[] = [
  { id: 'all', label: 'Wszystkie' },
  { id: 'turret', label: 'Wieże' },
  { id: 'inhibitor', label: 'Inhibitory' },
  { id: 'base', label: 'Nexus i smok' },
  { id: 'path', label: 'Ścieżki' },
];

export function passes(o: MapObj, filter: ObjFilter): boolean {
  switch (filter) {
    case 'all': return true;
    case 'turret': return o.k === 'turret';
    case 'inhibitor': return o.k === 'inhibitor';
    case 'base': return o.k === 'nexus' || o.k === 'dragon';
    case 'path': return o.k === 'point';
  }
}

export const objOpen = (): void => act('nav.obj.open');
export const objMove = (id: string, x: number, z: number): void => act('nav.obj.move', { id, x: Math.round(x), z: Math.round(z) });
export const objAngle = (id: string, angle: number): void => act('nav.obj.angle', { id, angle: Math.round(angle) });
export const objAddPoint = (lane: string, after: number, x: number, z: number): void => act('nav.obj.addpoint', { lane, after, x: Math.round(x), z: Math.round(z) });
export const objDeletePoint = (id: string): void => act('nav.obj.delpoint', { id });
export const objReset = (id: string): void => act('nav.obj.reset', { id });

export function wireObjects(): void {
  on('srv', ({ name, data }) => {
    if (name !== 'obj') return;
    const d = data as { selected: string; items: MapObj[] };
    objStore.set((s) => {
      const ids = new Set(d.items.map((o) => o.id));
      const selected = d.selected && ids.has(d.selected) ? d.selected : ids.has(s.selected) ? s.selected : '';
      return { items: d.items, selected };
    });
  });
}
