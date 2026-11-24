import type { ShopItem } from './shopStore';

export type RecipeState = 'root' | 'own' | 'covered' | 'missing';

export interface RecipeNode {
  item: ShopItem;
  state: RecipeState;
  slot: number;
  children: RecipeNode[];
}

const MAX_DEPTH = 6;

export function recipeTree(item: ShopItem, owned: string[], find: (key: string) => ShopItem | undefined): RecipeNode {
  const free = owned.map((key, slot) => (key ? slot : -1)).filter((slot) => slot >= 0);
  const expand = (node: ShopItem, depth: number, covered: boolean): RecipeNode[] => {
    if (depth > MAX_DEPTH) return [];
    const parts = node.builds.map(find).filter((p): p is ShopItem => !!p);
    const slots = parts.map((part) => {
      if (covered) return -1;
      const at = free.findIndex((slot) => owned[slot] === part.key);
      return at < 0 ? -1 : free.splice(at, 1)[0];
    });
    return parts.map((part, n) => {
      const own = slots[n] >= 0;
      return { item: part, state: own ? 'own' : covered ? 'covered' : 'missing', slot: slots[n], children: expand(part, depth + 1, covered || own) };
    });
  };
  return { item, state: 'root', slot: -1, children: expand(item, 1, false) };
}

export function creditOfTree(tree: RecipeNode): { slots: number[]; credit: number } {
  const slots: number[] = [];
  let credit = 0;
  const walk = (node: RecipeNode) => {
    for (const child of node.children) {
      if (child.state === 'own') {
        slots.push(child.slot);
        credit += child.item.cost;
      } else walk(child);
    }
  };
  walk(tree);
  return { slots, credit };
}

export const buildsInto = (key: string, catalog: ShopItem[]): ShopItem[] =>
  catalog.filter((i) => i.builds.includes(key)).sort((a, b) => a.cost - b.cost);
