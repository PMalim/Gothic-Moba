export type Rgb = [number, number, number];

export interface ChatLinePayload {
  kind: 'player' | 'system';
  author?: string;
  authorColor?: Rgb;
  color: Rgb;
  text: string;
  time: string;
}

export interface ChatCommand {
  name: string;
  aliases: string[];
  description: string;
  admin: boolean;
}

export interface AdminPlayer {
  id: number;
  name: string;
  hp: number;
  maxHp: number;
  ping: number;
  dead: boolean;
  ghost: boolean;
  admin: boolean;
}

export interface WorldEntity {
  id: number;
  kind: 'bot' | 'tower' | 'player';
  name: string;
  team: number;
  maxHp: number;
}

export interface BuilderState {
  on: boolean;
  panelFocus: boolean;
  mode: 'place' | 'grab';
  grabbed: number;
  yaw: number;
  pitch: number;
  roll: number;
  scale: number;
  lift: number;
  collision: boolean;
  hasPoint: boolean;
  vobs: number;
  storage: string;
  note: string;
  previewOk: boolean;
  hoverId: number;
  hover: string;
  locked: boolean;
  hidden: number;
  holding: boolean;
}

export interface Inbound {
  init: { name: string; resX: number; resY: number; admin: boolean };
  'chat.line': ChatLinePayload;
  'chat.open': { text: string };
  'chat.close': Record<string, never>;
  'chat.commands': { commands: ChatCommand[] };
  'admin.state': { admin: boolean };
  'admin.players': { players: AdminPlayer[]; minions: number };
  'admin.panel': { open: boolean };
  fly: { on: boolean; speed: number; pitch: number; yaw: number };
  'builder.state': BuilderState;
  'builder.filter': { text: string };
  'builder.key': { key: 'next' | 'prev' | 'catNext' | 'catPrev' };
  'world.add': WorldEntity;
  'world.remove': { id: number };
  'world.frame': { f: number[] };
  'world.focus': { id: number; locked: boolean };
  'gold.pop': { x: number; y: number; amount: number };
  'dmg.pop': { x: number; y: number; amount: number; kind: number; taken: boolean };
  'menu.state': { open: boolean };
  srv: { name: string; data: unknown };
  phase: { phase: 'lobby' | 'game'; match: number };
  'hud.player': { hp: number; maxHp: number };
  'mm.me': { x: number; z: number; ax: number; az: number };
  'mm.mode': Record<string, never>;
  'board.show': { on: boolean };
  'shop.close': Record<string, never>;
  'nav.close': Record<string, never>;
  'wpw.state': WpwState;
  'wpw.labels': { l: (string | number)[] };
  'builder.vobs': { cam: [number, number, number]; rows: [number, string, number, number, number, number][] };
}

export interface Outbound {
  ready: [];
  'chat.send': [text: string];
  'chat.closed': [];
  'ui.typing': [on: boolean];
  'builder.select': [visual: string];
  'builder.set': [key: BuilderSetting, value: number];
  'builder.action': [name: BuilderAction];
  'builder.goto': [id: number];
  'builder.grab': [id: number];
  'builder.remove': [id: number];
  'builder.highlight': [id: number];
  'builder.hide': [id: number];
  'admin.action': [action: number, target: number];
  'admin.close': [];
  'fly.toggle': [];
  'menu.close': [];
  'menu.exit': [];
  'srv.action': [name: string, json: string];
  'shop.cursor': [open: boolean];
  'nav.cursor': [open: boolean];
}

export type BuilderSetting = 'yaw' | 'pitch' | 'roll' | 'scale' | 'lift' | 'collision';
export type BuilderAction = 'look' | 'panel' | 'delete' | 'cancel' | 'reset' | 'exit' | 'showall' | 'lock';

export const AdminAction = {
  Kick: 1,
  Ban: 2,
  Kill: 3,
  Heal: 4,
  Respawn: 5,
  Goto: 6,
  Bring: 7,
  Freeze: 9,
  Unfreeze: 10,
  KillMobs: 13,
  Wave: 14,
  Reset: 15,
} as const;

export interface WpwState {
  on: boolean;
  tool?: number;
  panel?: boolean;
  count?: number;
  links?: number;
  note?: string;
  id?: number;
  name?: string;
  x?: number;
  y?: number;
  z?: number;
  near?: string[];
  hover?: string;
}
