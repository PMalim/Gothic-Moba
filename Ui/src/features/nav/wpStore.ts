import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';

export type WpTool = 'select' | 'add' | 'link' | 'delete' | 'test';

export interface WpNode {
  id: number;
  n: string;
  x: number;
  y: number;
  z: number;
}

export const wpStore = createStore({
  nodes: [] as WpNode[],
  links: [] as [number, number][],
  selected: -1,
  linkFrom: -1,
  tool: 'select' as WpTool,
  route: null as number[] | null,
  routeMs: 0,
  routeFailed: false,
  filter: '',
});

export const wpOpen = (): void => act('nav.wp.open');
export const wpAdd = (x: number, z: number): void => act('nav.wp.add', { x: Math.round(x), z: Math.round(z) });
export const wpMove = (id: number, x: number, z: number): void => act('nav.wp.move', { id, x: Math.round(x), z: Math.round(z) });
export const wpRename = (id: number, name: string): void => act('nav.wp.rename', { id, name });
export const wpDelete = (id: number): void => act('nav.wp.delete', { id });
export const wpLink = (a: number, b: number): void => act('nav.wp.link', { a, b });
export const wpGenerate = (): void => act('nav.wp.generate');
export const wpUndo = (): void => act('nav.wp.undo');
export const wpRoute = (a: [number, number], b: [number, number]): void => act('nav.wp.route', { a, b });

export function wireWaypoints(): void {
  on('srv', ({ name, data }) => {
    if (name === 'wp') {
      const d = data as { selected: number; nodes: WpNode[]; links: [number, number][] };
      wpStore.set((s) => {
        const ids = new Set(d.nodes.map((n) => n.id));
        const selected = d.selected >= 0 ? d.selected : ids.has(s.selected) ? s.selected : -1;
        return { nodes: d.nodes, links: d.links, selected, linkFrom: ids.has(s.linkFrom) ? s.linkFrom : -1 };
      });
    } else if (name === 'wp.route') {
      const d = data as { points: number[] | null; ms: number };
      wpStore.set({ route: d.points, routeMs: d.ms, routeFailed: !d.points });
    }
  });
}
