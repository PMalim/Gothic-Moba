import { useEffect, useMemo, useRef } from 'react';
import { ALL, RECENT, categories, categoryIdOf, categoryTitle, countsFor, entriesFor, shortTitle, splitName, total } from './catalog';
import { builderUi, pickVisual, selectVisual, setCategory, setQuery } from './builderUi';
import { Thumb, dimensionsOf, formatDimensions } from './Previews';
import { VirtualList } from './VirtualList';
import { useStore } from '../../state/store';

export function CatalogFilters() {
  const s = useStore(builderUi);
  const counts = useMemo(() => countsFor(s.query), [s.query]);
  const sum = [...counts.values()].reduce((a, n) => a + n, 0);
  return (
    <div className="filters">
      <div className="search">
        <input
          type="search"
          value={s.query}
          placeholder={`Szukaj wśród ${total} obiektów… (np. barrel, chest, door)`}
          spellCheck={false}
          onChange={(e) => setQuery(e.target.value)}
        />
        {s.query && (
          <button className="search__clear" onClick={() => setQuery('')} aria-label="Wyczyść szukanie">
            ×
          </button>
        )}
      </div>
      <nav className="chips" aria-label="Kategorie">
        <Chip id={ALL} label="Wszystkie" count={sum} />
        <Chip id={RECENT} label="Ostatnie" count={s.recent.length} />
        {categories.map((c) => (
          <Chip key={c.id} id={c.id} label={shortTitle(c)} count={counts.get(c.id) ?? 0} title={c.title} />
        ))}
      </nav>
    </div>
  );
}

function Chip({ id, label, count, title }: { id: string; label: string; count: number; title?: string }) {
  const current = useStore(builderUi).category;
  return (
    <button className={`chip ${current === id ? 'is-active' : ''} ${count === 0 ? 'is-empty' : ''}`} onClick={() => setCategory(id)} title={title}>
      {label}
      <span className="chip__count">{count}</span>
    </button>
  );
}

export function GalleryTab() {
  const s = useStore(builderUi);
  const entries = useMemo(() => entriesFor(s.category, s.recent, s.query), [s.category, s.recent, s.query]);
  const selectedRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    selectedRef.current?.scrollIntoView({ block: 'nearest' });
  }, [s.selected, s.scrollSignal]);

  return (
    <div className="tabpane">
      <CatalogFilters />
      <div className="gallery">
        {entries.map((entry) => {
          const [base, ext] = splitName(entry.name);
          const dim = dimensionsOf(entry.name);
          const selected = entry.name === s.selected;
          return (
            <button
              key={entry.name}
              ref={selected ? selectedRef : undefined}
              className={`cell ${selected ? 'is-selected' : ''}`}
              onClick={() => selectVisual(entry.name)}
              onDoubleClick={() => pickVisual(entry.name)}
              title={`${entry.name} — ${categoryTitle(categoryIdOf(entry.name))}${dim ? ' — ' + formatDimensions(dim) : ''}`}
            >
              <Thumb name={entry.name} />
              <span className="cell__name">
                {base}
                <span className="dim">{ext}</span>
              </span>
            </button>
          );
        })}
        {entries.length === 0 && <div className="empty">Nic nie pasuje do frazy.</div>}
      </div>
      <div className="pager">
        <span>{entries.length} obiektów</span>
        <span className="pager__tip">Podwójne kliknięcie = weź obiekt i buduj</span>
      </div>
    </div>
  );
}

export function ListTab() {
  const s = useStore(builderUi);
  const entries = useMemo(() => entriesFor(s.category, s.recent, s.query), [s.category, s.recent, s.query]);
  const selectedIndex = entries.findIndex((e) => e.name === s.selected);

  const onKey = (e: React.KeyboardEvent<HTMLElement>) => {
    if (entries.length === 0) return;
    const move = (delta: number) => {
      e.preventDefault();
      const at = Math.min(Math.max(selectedIndex + delta, 0), entries.length - 1);
      selectVisual(entries[at].name, true);
    };
    if (e.key === 'ArrowDown') move(1);
    else if (e.key === 'ArrowUp') move(-1);
    else if (e.key === 'PageDown') move(10);
    else if (e.key === 'PageUp') move(-10);
    else if (e.key === 'Enter' && selectedIndex >= 0) {
      e.preventDefault();
      pickVisual(entries[selectedIndex].name);
    }
  };

  return (
    <div className="tabpane" onKeyDown={onKey}>
      <CatalogFilters />
      <VirtualList
        items={entries}
        rowHeight={30}
        selectedIndex={selectedIndex}
        scrollSignal={s.scrollSignal}
        onSelect={(entry) => selectVisual(entry.name)}
        onActivate={(entry) => pickVisual(entry.name)}
        empty="Nic nie pasuje do frazy."
        renderRow={(entry, _i, isSelected) => {
          const [base, ext] = splitName(entry.name);
          return (
            <div className={`row ${isSelected ? 'is-selected' : ''}`}>
              <span className="row__name">
                {base}
                <span className="dim">{ext}</span>
              </span>
              <span className="row__tag">{categoryTitle(entry.category).replace(/\s*\(.*\)$/, '')}</span>
            </div>
          );
        }}
      />
      <div className="pager">
        <span>
          {entries.length} obiektów · <kbd>↑</kbd> <kbd>↓</kbd> wybór · <kbd>Enter</kbd> / podwójne kliknięcie = weź obiekt i buduj
        </span>
      </div>
    </div>
  );
}
