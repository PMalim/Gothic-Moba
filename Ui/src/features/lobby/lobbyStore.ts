import { on, send } from '../../bridge/bridge';
import { createStore } from '../../state/store';

export interface Profile {
  loaded: boolean;
  name: string;
  points?: number;
  lp?: number;
  wins?: number;
  losses?: number;
  ranked?: number;
  rank?: number;
  hero?: string;
  owned?: string[];
}

export interface AbilityInfo {
  key: string;
  name: string;
  text: string;
  kind: string;
  cooldown: number;
  range: number;
  radius: number;
  damage: number;
  heal: number;
  seconds: number;
  cost?: number;
  adRatio?: number;
  apRatio?: number;
  healAdRatio?: number;
  healApRatio?: number;
  magic?: boolean;
  ultimate?: boolean;
  ranks?: RankInfo[];
}

export interface RankInfo {
  damage: number;
  heal: number;
  seconds: number;
  cooldown: number;
  cost: number;
  need: number;
}

export interface SkillInfo {
  rank: number;
  max: number;
  can: boolean;
  need: number;
  ult: boolean;
  cost: number;
}

export interface HeroInfo {
  key: string;
  name: string;
  title: string;
  role: string;
  lore: string;
  price: number;
  health: number;
  strength: number;
  dexterity: number;
  mana?: number;
  weapon: string;
  abilities: AbilityInfo[];
}

export type Mode = 'bots' | 'normal' | 'ranked';

export interface RoomRow {
  id: number;
  title: string;
  mode: Mode;
  state: 'room' | 'countdown' | 'running' | 'finished';
  players: number;
  bots?: number;
  max: number;
  teamSize: number;
  host: string;
  seconds: number;
}

export interface RoomPlayer {
  name: string;
  hero: string;
  host: boolean;
  you: boolean;
  kills: number;
  deaths: number;
  lp: number;
  bot?: boolean;
  id?: number;
}

export interface RoomInfo {
  id: number;
  title: string;
  mode: Mode;
  state: 'room' | 'countdown' | 'running' | 'finished';
  teamSize: number;
  isHost: boolean;
  canStart: boolean;
  botSlots?: { blue: boolean; red: boolean };
  countdown: number;
  seconds: number;
  blue: RoomPlayer[];
  red: RoomPlayer[];
  winner: string | null;
}

export interface QueueInfo {
  active: boolean;
  waiting: number;
  since: number;
}

export interface RankingRow {
  rank: number;
  name: string;
  lp: number;
  wins: number;
  losses: number;
  you: boolean;
}

export interface ResultRow {
  id: number;
  bot: boolean;
  you: boolean;
  team: number;
  name: string;
  hero: string;
  level: number;
  left: boolean;
  kills: number;
  deaths: number;
  assists: number;
  cs: number;
  gold: number;
  dmgChampions: number;
  dmgObjectives: number;
  dmgMinions: number;
  taken: number;
  items: string[];
  mvp: boolean;
}

export interface ResultTeam {
  team: number;
  kills: number;
  deaths: number;
  assists: number;
  gold: number;
  damage: number;
  towers: number;
  inhibitors: number;
  drakes: number;
  barons: number;
}

export interface LpInfo {
  own: number;
  other: number;
  chance: number;
  k: number;
  raw: number;
  limited: number;
  min: number;
  max: number;
  applied: number;
}

export interface ResultInfo {
  won: boolean;
  winner: string | null;
  team: string;
  hero: string;
  kills: number;
  deaths: number;
  assists: number;
  botKills: number;
  points: number;
  lpChange: number;
  lp: number;
  ranked: boolean;
  seconds: number;
  mode?: string;
  rows?: ResultRow[];
  teams?: ResultTeam[];
  lpInfo?: LpInfo | null;
}

export interface AuthInfo {
  status: 'pending' | 'login' | 'ok';
  name: string;
  error: string;
  busy: boolean;
}

export type Tab = 'play' | 'heroes' | 'shop' | 'ranking' | 'profile';

export const lobbyStore = createStore({
  phase: 'lobby' as 'lobby' | 'game',
  match: 0,
  tab: 'play' as Tab,
  auth: { status: 'pending', name: '', error: '', busy: false } as AuthInfo,
  profile: null as Profile | null,
  heroes: [] as HeroInfo[],
  rooms: [] as RoomRow[],
  room: null as RoomInfo | null,
  roomAt: 0,
  queue: { active: false, waiting: 0, since: 0 } as QueueInfo,
  ranking: [] as RankingRow[],
  result: null as ResultInfo | null,
  toast: null as { id: number; text: string; kind: string } | null,
  selectedRoom: -1,
});

export const abilityStore = createStore({ hero: '', readyAt: [0, 0, 0, 0] as number[], total: [0, 0, 0, 0] as number[] });

export const scoreStore = createStore({ blue: 0, red: 0, seconds: 0, base: Date.now() });

export const meStore = createStore({ points: 0, skills: [] as SkillInfo[], aa: 0, aaMagic: false, critMult: 200, kills: 0, deaths: 0, assists: 0, cs: 0, gold: 0, level: 1, xp: 0, xpFloor: 0, xpNext: 100, ad: 0, ap: 0, crit: 0, armor: 0, resist: 0, cdr: 0, regen: 0, regenHp: 2, items: [] as string[], charges: [] as number[], elixirs: [] as { key: string; ms: number }[] });

export const manaStore = createStore({ current: 0, max: 0, regen: 0, at: Date.now() });

export const buffStore = createStore({ stacks: 0, stackDamage: 2, primed: 0, haste: 0, rampage: 0, pierce: 0, hunt: 0, hidden: 0, combo: 0, iron: 0, shadow: 0, blood: 0, bleed: 0, bleedStacks: 0, fire: 0, fireCharges: 0, tribe: 0, guard: 0, at: Date.now() });

export const vitalStore = createStore({ hp: 0, maxHp: 0 });

let toastId = 1;

export function act(name: string, data: Record<string, unknown> = {}): void {
  const json = JSON.stringify(data).replace(/[\u007f-￿]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
  send('srv.action', name, json);
}

export function wireLobby(): void {
  on('hud.player', (p) => vitalStore.set({ hp: p.hp, maxHp: p.maxHp }));
  on('phase', (p) => {
    lobbyStore.set({ phase: p.phase, match: p.match });
    if (p.phase === 'lobby') scoreStore.set({ blue: 0, red: 0, seconds: 0, base: Date.now() });
  });
  on('srv', ({ name, data }) => {
    switch (name) {
      case 'auth': {
        const d = data as { status: AuthInfo['status']; name: string; error: string };
        lobbyStore.set({ auth: { status: d.status, name: d.name, error: d.error, busy: false } });
        break;
      }
      case 'profile':
        lobbyStore.set({ profile: data as Profile });
        break;
      case 'heroes':
        lobbyStore.set({ heroes: data as HeroInfo[] });
        break;
      case 'rooms':
        lobbyStore.set({ rooms: data as RoomRow[] });
        break;
      case 'room':
        lobbyStore.set({ room: (data as RoomInfo | null) ?? null, roomAt: Date.now() });
        break;
      case 'queue':
        lobbyStore.set({ queue: data as QueueInfo });
        break;
      case 'ranking':
        lobbyStore.set({ ranking: data as RankingRow[] });
        break;
      case 'result':
        lobbyStore.set({ result: data as ResultInfo });
        break;
      case 'score': {
        const d = data as { blue: number; red: number; seconds: number };
        scoreStore.set({ blue: d.blue, red: d.red, seconds: d.seconds, base: Date.now() });
        break;
      }
      case 'me':
        meStore.set(data as ReturnType<typeof meStore.get>);
        break;
      case 'mana': {
        const d = data as { current: number; max: number; regen: number };
        manaStore.set({ current: d.current, max: d.max, regen: d.regen, at: Date.now() });
        break;
      }
      case 'buffs': {
        const d = data as Partial<ReturnType<typeof buffStore.get>>;
        buffStore.set({ stacks: 0, stackDamage: 2, primed: 0, haste: 0, rampage: 0, pierce: 0, hunt: 0, hidden: 0, combo: 0, iron: 0, shadow: 0, blood: 0, bleed: 0, bleedStacks: 0, fire: 0, fireCharges: 0, tribe: 0, guard: 0, ...d, at: Date.now() });
        break;
      }
      case 'hud':
        abilityStore.set({ hero: (data as { hero: string }).hero, readyAt: [0, 0, 0, 0], total: [0, 0, 0, 0] });
        break;
      case 'cd': {
        const { slot, ms } = data as { slot: number; ms: number };
        abilityStore.set((a) => ({
          readyAt: a.readyAt.map((v, i) => (i === slot ? Date.now() + ms : v)),
          total: a.total.map((v, i) => (i === slot ? ms : v)),
        }));
        break;
      }
      case 'toast': {
        const t = data as { text: string; kind: string };
        lobbyStore.set({ toast: { id: toastId++, ...t } });
        break;
      }
    }
  });
}

export const heroOf = (key: string | undefined, heroes: HeroInfo[]): HeroInfo | undefined => heroes.find((h) => h.key === key);
