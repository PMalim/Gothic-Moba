import raw from '../../data/catalog.json';

export interface Category {
  id: string;
  title: string;
  names: string[];
}

export interface Entry {
  name: string;
  category: string;
}

export const ALL = 'all';
export const RECENT = 'recent';

export const categories: Category[] = raw as Category[];
export const total = categories.reduce((sum, c) => sum + c.names.length, 0);

const byId = new Map(categories.map((c) => [c.id, c]));
const categoryOfName = new Map<string, string>();
for (const c of categories) for (const n of c.names) categoryOfName.set(n, c.id);

export const shortTitle = (c: Category): string => c.title.replace(/\s*\(.*\)\s*$/, '');

export const categoryTitle = (id: string): string =>
  id === ALL ? 'Wszystkie' : id === RECENT ? 'Ostatnie' : (byId.get(id)?.title ?? id);

export const categoryIdOf = (name: string): string => categoryOfName.get(name) ?? 'other';

const RECENT_KEY = 'botarena.builder.recent';
const RECENT_LIMIT = 24;

export function loadRecent(): string[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((n): n is string => typeof n === 'string').slice(0, RECENT_LIMIT) : [];
  } catch {
    return [];
  }
}

export function rememberRecent(list: string[], name: string): string[] {
  const next = [name, ...list.filter((n) => n !== name)].slice(0, RECENT_LIMIT);
  try {
    localStorage.setItem(RECENT_KEY, JSON.stringify(next));
  } catch {
  }
  return next;
}

export function entriesFor(categoryId: string, recent: string[], query: string): Entry[] {
  let entries: Entry[];
  if (categoryId === ALL) entries = categories.flatMap((c) => c.names.map((name) => ({ name, category: c.id })));
  else if (categoryId === RECENT) entries = recent.map((name) => ({ name, category: categoryIdOf(name) }));
  else entries = (byId.get(categoryId)?.names ?? []).map((name) => ({ name, category: categoryId }));

  const words = query.toLowerCase().split(/\s+/).filter(Boolean);
  if (words.length === 0) return entries;
  return entries.filter((e) => {
    const hay = e.name.toLowerCase();
    return words.every((w) => hay.includes(w));
  });
}

export function countsFor(query: string): Map<string, number> {
  const words = query.toLowerCase().split(/\s+/).filter(Boolean);
  const counts = new Map<string, number>();
  for (const c of categories) {
    counts.set(c.id, words.length === 0 ? c.names.length : c.names.filter((n) => words.every((w) => n.toLowerCase().includes(w))).length);
  }
  return counts;
}

export function splitName(name: string): [string, string] {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? [name, ''] : [name.slice(0, dot), name.slice(dot)];
}
