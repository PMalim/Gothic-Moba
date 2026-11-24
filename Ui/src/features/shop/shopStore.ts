import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';
import { creditOfTree, recipeTree } from './recipe';

export interface ItemStats {
  ad: number;
  ap: number;
  armor: number;
  resist: number;
  health: number;
  mana: number;
  crit: number;
  cdr: number;
  hpRegen: number;
  as: number;
  ms: number;
  ls: number;
  ov: number;
  leth: number;
  armPen: number;
  magPen: number;
  magPenPct: number;
  ten: number;
  healPower: number;
  manaRegen: number;
  critDmg: number;
}

export interface ShopItem {
  key: string;
  name: string;
  group: 'basic' | 'component' | 'attack' | 'magic' | 'defense' | 'utility' | 'starter' | 'consumable';
  cost: number;
  glyph: string;
  shape?: string;
  desc?: string;
  builds: string[];
  stats: ItemStats;
  use?: { kind: 'heal' | 'mana' | 'elixir' | 'active'; amount: number; seconds: number; max: number; boost: ItemStats;  active?: string; radius?: number; cooldown?: number } | null;
}

export const GROUPS: { id: ShopItem['group'] | 'all' | 'mine'; label: string }[] = [
  { id: 'all', label: 'Wszystko' },
  { id: 'attack', label: 'Atak' },
  { id: 'magic', label: 'Magia' },
  { id: 'defense', label: 'Obrona' },
  { id: 'utility', label: 'Wsparcie' },
  { id: 'component', label: 'Komponenty' },
  { id: 'basic', label: 'Podstawowe' },
  { id: 'starter', label: 'Startowe' },
  { id: 'consumable', label: 'Użytkowe' },
];

export interface ShopAdvice {
  hero: string;
  role: string;
  build: string[];
  sections: { id: string; title: string; hint: string; items: string[] }[];
}

export type ShopView = 'recommended' | 'all';

export const shopStore = createStore({
  open: false,
  view: 'recommended' as ShopView,
  advice: null as ShopAdvice | null,
  inside: false,
  catalog: [] as ShopItem[],
  owned: [] as string[],
  charges: [] as number[],
  prices: [] as number[],
  slots: 6,
  sellRatio: 0.7,
  group: 'all' as ShopItem['group'] | 'all',
  query: '',
  selected: '',
});

interface ShopPush {
  open: boolean;
  owned?: string[];
  charges?: number[];
  prices?: number[];
  slots?: number;
  sellRatio?: number;
  catalog?: ShopItem[] | null;
  advice?: ShopAdvice | null;
}

export function priceFor(owned: string[], item: ShopItem): number {
  return Math.max(0, item.cost - creditOf(owned, item).credit);
}

export const treeOf = (item: ShopItem, owned: string[]) => recipeTree(item, owned, itemByKey);

export function nextPurchase(owned: string[], gold: number, advice: ShopAdvice | null): { buy?: ShopItem; saving?: ShopItem; missing: number } {
  if (!advice) return { missing: 0 };
  if (!owned.some(Boolean)) {
    const starter = advice.sections.find((s) => s.id === 'start')?.items.map(itemByKey).find((i) => i && i.group === 'starter' && i.cost <= gold);
    if (starter) return { buy: starter, missing: 0 };
  }
  const affordable = (target: ShopItem, depth: number): ShopItem | undefined => {
    if (priceFor(owned, target) <= gold) return target;
    if (depth > 4) return undefined;
    const missing = treeOf(target, owned).children.filter((c) => c.state === 'missing').map((c) => c.item);
    let best: ShopItem | undefined;
    for (const part of missing) {
      const pick = affordable(part, depth + 1);
      if (pick && (!best || pick.cost > best.cost)) best = pick;
    }
    return best;
  };
  for (const key of advice.build) {
    if (owned.includes(key)) continue;
    const target = itemByKey(key);
    if (!target) return { missing: 0 };
    const buy = affordable(target, 0);
    return buy ? { buy, saving: buy === target ? undefined : target, missing: priceFor(owned, target) - gold } : { saving: target, missing: priceFor(owned, target) - gold };
  }
  return { missing: 0 };
}

export const itemCdStore = createStore({ cooldowns: {} as Record<string, { ms: number; at: number }> });

export const itemByKey = (key: string): ShopItem | undefined => shopStore.get().catalog.find((i) => i.key === key);

export function closeShop(): void {
  if (!shopStore.get().open) return;
  shopStore.set({ open: false });
  act('item.close');
}

export const buyItem = (key: string): void => act('item.buy', { key });
export const sellItem = (slot: number): void => act('item.sell', { slot });

export function wireShop(): void {
  on('srv', ({ name, data }) => {
    if (name === 'shop') {
      const d = data as ShopPush;
      shopStore.set((s) => ({
        open: d.open,
        view: d.open && !s.open ? 'recommended' : s.view,
        query: d.open && !s.open ? '' : s.query,
        advice: d.advice !== undefined ? d.advice : s.advice,
        owned: d.owned ?? (d.open ? s.owned : []),
        charges: d.charges ?? (d.open ? s.charges : []),
        prices: d.prices ?? s.prices,
        slots: d.slots ?? s.slots,
        sellRatio: d.sellRatio ?? s.sellRatio,
        catalog: d.catalog && d.catalog.length ? d.catalog : s.catalog,
      }));
    } else if (name === 'item.used') {
      const d = data as { key: string; ms: number };
      itemCdStore.set((s) => ({ cooldowns: { ...s.cooldowns, [d.key]: { ms: d.ms, at: Date.now() } } }));
    } else if (name === 'shop.zone') {
      const d = data as { inside: boolean; catalog?: ShopItem[] | null };
      shopStore.set((s) => ({ inside: d.inside, open: d.inside ? s.open : false, catalog: d.catalog && d.catalog.length ? d.catalog : s.catalog }));
    } else if (name === 'me') {
      const d = data as { items?: string[]; charges?: number[] };
      if (d.items) shopStore.set({ owned: d.items, charges: d.charges ?? d.items.map(() => 1) });
    }
  });
  on('shop.close', () => shopStore.set({ open: false }));
}

export function creditOf(owned: string[], item: ShopItem): { used: number; credit: number } {
  if (!item.builds.length) return { used: 0, credit: 0 };
  const { slots, credit } = creditOfTree(treeOf(item, owned));
  return { used: slots.length, credit };
}

export const filledSlots = (owned: string[]): number => owned.filter(Boolean).length;

export function buyState(item: ShopItem, owned: string[], charges: number[], gold: number, slots: number): { price: number; can: boolean; reason: string } {
  if (item.use && item.use.kind !== 'active') {
    const at = owned.indexOf(item.key);
    const have = at >= 0 ? charges[at] ?? 1 : 0;
    const max = item.use.max ?? 1;
    if (have >= max) return { price: item.cost, can: false, reason: `Masz już maksimum (${max})` };
    if (have === 0 && filledSlots(owned) >= slots) return { price: item.cost, can: false, reason: 'Brak miejsca w ekwipunku' };
    if (gold < item.cost) return { price: item.cost, can: false, reason: 'Za mało złota' };
    return { price: item.cost, can: true, reason: '' };
  }
  const { used, credit } = creditOf(owned, item);
  const price = Math.max(0, item.cost - credit);
  if (filledSlots(owned) - used + 1 > slots) return { price, can: false, reason: 'Brak miejsca w ekwipunku' };
  if (gold < price) return { price, can: false, reason: 'Za mało złota' };
  return { price, can: true, reason: '' };
}

export const moveItem = (from: number, to: number): void => act('item.move', { from, to });
