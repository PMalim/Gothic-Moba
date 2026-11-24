import { on } from '../bridge/bridge';
import type { AdminPlayer, BuilderState, ChatCommand, ChatLinePayload } from '../bridge/protocol';
import { createStore } from './store';

export const uiStore = createStore({
  name: '',
  admin: false,
  resX: 1920,
  resY: 1080,
});

export interface ChatLine extends ChatLinePayload {
  id: number;
  at: number;
}

export const MAX_CHAT_LINES = 120;

export const chatStore = createStore({
  open: false,
  prefill: '',
  openCount: 0,
  lines: [] as ChatLine[],
  commands: [] as ChatCommand[],
});

let nextLineId = 1;

export const adminStore = createStore({
  open: false,
  players: [] as AdminPlayer[],
  minions: 0,
  selected: -1,
});

export const menuStore = createStore({ open: false });

export const flyStore = createStore({ on: false, speed: 0, pitch: 0, yaw: 0 });

export const builderDefaults: BuilderState = {
  on: false,
  panelFocus: false,
  mode: 'place',
  grabbed: -1,
  yaw: 0,
  pitch: 0,
  roll: 0,
  scale: 1,
  lift: 0,
  collision: true,
  hasPoint: false,
  vobs: 0,
  storage: '',
  note: '',
  previewOk: true,
  hoverId: -1,
  hover: '',
  locked: false,
  hidden: 0,
  holding: false,
};

export const builderStore = createStore({ ...builderDefaults });

export function wireStores(): void {
  on('init', (p) => uiStore.set({ name: p.name, admin: p.admin, resX: p.resX, resY: p.resY }));

  on('chat.line', (line) => {
    const entry: ChatLine = { ...line, id: nextLineId++, at: Date.now() };
    chatStore.set((s) => ({ lines: [...s.lines, entry].slice(-MAX_CHAT_LINES) }));
  });
  on('chat.open', (p) => chatStore.set((s) => ({ open: true, prefill: p.text, openCount: s.openCount + 1 })));
  on('chat.close', () => chatStore.set({ open: false }));
  on('chat.commands', (p) => chatStore.set({ commands: p.commands }));

  on('admin.state', (p) => {
    uiStore.set({ admin: p.admin });
    if (!p.admin) adminStore.set({ open: false });
  });
  on('admin.players', (p) =>
    adminStore.set((s) => ({
      players: p.players,
      minions: p.minions,
      selected: p.players.some((pl) => pl.id === s.selected) ? s.selected : -1,
    })),
  );
  on('admin.panel', (p) => adminStore.set({ open: p.open }));

  on('fly', (p) => flyStore.set(p));
  on('menu.state', (p) => menuStore.set({ open: p.open }));

  on('builder.state', (p) => builderStore.set(p));
}
