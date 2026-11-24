const files = import.meta.glob('../../assets/items/*.webp', { eager: true, query: '?url', import: 'default' }) as Record<string, string>;

export const ITEM_IMAGES: Record<string, string> = Object.fromEntries(
  Object.entries(files).map(([path, url]) => [path.replace(/^.*\/(.+)\.webp$/, '$1'), url]),
);
