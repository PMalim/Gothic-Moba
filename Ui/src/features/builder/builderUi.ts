import { on, send } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { ALL, RECENT, categories, entriesFor, loadRecent, rememberRecent, type Entry } from './catalog';

export type ManagerTab = 'gallery' | 'list' | 'placed';

export const GALLERY_PAGE = 12;

export const builderUi = createStore({
  tab: 'gallery' as ManagerTab,
  category: ALL,
  query: '',
  selected: '',
  recent: loadRecent(),
  scrollSignal: 0,
  galleryPage: 0,
});

export interface PlacedRow {
  id: number;
  visual: string;
  x: number;
  y: number;
  z: number;
  hidden: boolean;
}

export const placedStore = createStore({
  rows: [] as PlacedRow[],
  cam: [0, 0, 0] as [number, number, number],
  selected: -1,
  query: '',
  loaded: false,
});

export const currentEntries = (): Entry[] => {
  const s = builderUi.get();
  return entriesFor(s.category, s.recent, s.query);
};

export function selectVisual(name: string, scroll = false): void {
  const s = builderUi.get();
  builderUi.set({ selected: name, scrollSignal: scroll ? s.scrollSignal + 1 : s.scrollSignal });
  const index = currentEntries().findIndex((e) => e.name === name);
  if (index >= 0) builderUi.set({ galleryPage: Math.floor(index / GALLERY_PAGE) });
  send('builder.select', name);
}

export function pickVisual(name: string): void {
  selectVisual(name);
  builderUi.set((s) => ({ recent: rememberRecent(s.recent, name) }));
  send('builder.action', 'look');
}

function keepSelectionValid(): void {
  const entries = currentEntries();
  const s = builderUi.get();
  if (entries.length > 0 && !entries.some((e) => e.name === s.selected)) selectVisual(entries[0].name, true);
  builderUi.set({ galleryPage: Math.min(s.galleryPage, Math.max(0, Math.ceil(entries.length / GALLERY_PAGE) - 1)) });
}

export function setCategory(category: string): void {
  builderUi.set({ category, galleryPage: 0 });
  keepSelectionValid();
}

export function setQuery(query: string): void {
  builderUi.set({ query, galleryPage: 0 });
  keepSelectionValid();
}

export function stepSelection(delta: number): void {
  const entries = currentEntries();
  if (entries.length === 0) return;
  const at = entries.findIndex((e) => e.name === builderUi.get().selected);
  const next = at < 0 ? 0 : Math.min(entries.length - 1, Math.max(0, at + delta));
  selectVisual(entries[next].name, true);
}

export function stepCategory(delta: number): void {
  const ids = [ALL, RECENT, ...categories.map((c) => c.id)];
  const at = ids.indexOf(builderUi.get().category);
  setCategory(ids[(at + delta + ids.length) % ids.length]);
}

export function setGalleryPage(page: number): void {
  const pages = Math.max(1, Math.ceil(currentEntries().length / GALLERY_PAGE));
  builderUi.set({ galleryPage: Math.min(pages - 1, Math.max(0, page)) });
}

export function selectPlaced(id: number): void {
  placedStore.set({ selected: id });
  send('builder.highlight', id);
}

export const gotoPlaced = (id: number): void => {
  selectPlaced(id);
  send('builder.goto', id);
};

export const grabPlaced = (id: number): void => send('builder.grab', id);

export const hidePlaced = (id: number): void => send('builder.hide', id);
export const showAllPlaced = (): void => send('builder.action', 'showall');

export function removePlaced(id: number): void {
  send('builder.remove', id);
  placedStore.set((s) => ({ rows: s.rows.filter((r) => r.id !== id), selected: s.selected === id ? -1 : s.selected }));
}

export function wireBuilder(): void {
  on('builder.key', ({ key }) => {
    if (key === 'next') stepSelection(1);
    else if (key === 'prev') stepSelection(-1);
    else stepCategory(key === 'catNext' ? 1 : -1);
  });
  on('builder.filter', ({ text }) => {
    builderUi.set({ category: ALL });
    setQuery(text);
  });
  on('builder.state', (p) => {
    if (p.on && !builderUi.get().selected) keepSelectionValid();
    if (!p.on) placedStore.set({ selected: -1 });
  });
  on('builder.vobs', ({ cam, rows }) =>
    placedStore.set((s) => ({
      cam,
      rows: rows.map(([id, visual, x, y, z, hidden]) => ({ id, visual, x, y, z, hidden: hidden === 1 })),
      selected: rows.some((r) => r[0] === s.selected) ? s.selected : -1,
      loaded: true,
    })),
  );
}
