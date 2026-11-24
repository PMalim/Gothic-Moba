import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';

export interface BoardRow {
  id: number;
  team: number;
  name: string;
  hero: string;
  bot: boolean;
  you: boolean;
  lp: number;
  level: number;
  kills: number;
  deaths: number;
  assists: number;
  cs: number;
  items: string[];
  ad: number;
  ap: number;
  armor: number;
  resist: number;
  alive: boolean;
}

export interface BoardTimer {
  key: 'drake' | 'baron' | string;
  name: string;
  state: 'alive' | 'wait' | 'off' | 'soon';
  left: number;
}

export const boardStore = createStore({
  visible: false,
  rows: [] as BoardRow[],
  timers: [] as BoardTimer[],
  seconds: 0,
  at: 0,
  blue: 0,
  red: 0,
});

export function wireBoard(): void {
  on('board.show', ({ on: visible }) => {
    boardStore.set({ visible });
    if (visible) act('board.get');
  });
  on('phase', (p) => {
    if (p.phase === 'lobby') boardStore.set({ visible: false, rows: [] });
  });
  on('srv', ({ name, data }) => {
    if (name !== 'board') return;
    const d = data as { rows: BoardRow[]; seconds: number; blue: number; red: number; timers?: BoardTimer[] };
    boardStore.set({ rows: d.rows, seconds: d.seconds, blue: d.blue, red: d.red, timers: d.timers ?? [], at: Date.now() });
  });
}
