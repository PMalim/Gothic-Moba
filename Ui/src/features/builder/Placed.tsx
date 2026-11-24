import { useEffect, useMemo, useState } from 'react';
import { gotoPlaced, grabPlaced, hidePlaced, placedStore, removePlaced, selectPlaced, showAllPlaced, type PlacedRow } from './builderUi';
import { categoryIdOf, categoryTitle, splitName } from './catalog';
import { Thumb } from './Previews';
import { VirtualList } from './VirtualList';
import { useStore } from '../../state/store';

const distanceM = (r: PlacedRow, cam: [number, number, number]): number => Math.hypot(r.x - cam[0], r.y - cam[1], r.z - cam[2]) / 100;

export function PlacedTab() {
  const p = useStore(placedStore);
  const [armed, setArmed] = useState(-1);

  useEffect(() => {
    if (armed < 0) return;
    const timer = window.setTimeout(() => setArmed(-1), 3000);
    return () => window.clearTimeout(timer);
  }, [armed]);

  const rows = useMemo(() => {
    const words = p.query.toLowerCase().split(/\s+/).filter(Boolean);
    const withDistance = p.rows
      .filter((r) => words.every((w) => r.visual.toLowerCase().includes(w) || String(r.id) === w.replace('#', '')))
      .map((r) => ({ row: r, d: distanceM(r, p.cam) }));
    withDistance.sort((a, b) => a.d - b.d);
    return withDistance;
  }, [p.rows, p.cam, p.query]);

  const selectedIndex = rows.findIndex((r) => r.row.id === p.selected);
  const hiddenCount = p.rows.filter((r) => r.hidden).length;

  return (
    <div className="tabpane">
      <div className="filters">
        <div className="search">
          <input
            type="search"
            value={p.query}
            placeholder={`Szukaj wśród ${p.rows.length} postawionych (nazwa albo numer)…`}
            spellCheck={false}
            onChange={(e) => placedStore.set({ query: e.target.value })}
          />
        </div>
        <p className="hint">
          Posortowane od najbliższych kamery. Kliknięcie podświetla voba w świecie, podwójne skacze do niego. „Ukryj” chowa go tylko u Ciebie (nic nie jest zapisywane).
          {hiddenCount > 0 && (
            <>
              {' '}
              <button className="btn btn--small" onClick={showAllPlaced}>
                Pokaż wszystkie schowane ({hiddenCount})
              </button>
            </>
          )}
        </p>
      </div>
      {!p.loaded ? (
        <div className="empty">Ładowanie…</div>
      ) : (
        <VirtualList
          items={rows}
          rowHeight={44}
          selectedIndex={selectedIndex}
          scrollSignal={0}
          onSelect={({ row }) => selectPlaced(row.id)}
          onActivate={({ row }) => gotoPlaced(row.id)}
          empty={p.rows.length === 0 ? 'W świecie nie ma jeszcze postawionych vobów.' : 'Nic nie pasuje do frazy.'}
          renderRow={({ row, d }, _i, isSelected) => {
            const [base, ext] = splitName(row.visual);
            return (
              <div className={`row row--placed ${isSelected ? 'is-selected' : ''} ${row.hidden ? 'is-hidden' : ''}`}>
                <Thumb name={row.visual} className="thumb--row" />
                <span className="row__id">#{row.id}</span>
                <span className="row__name" title={categoryTitle(categoryIdOf(row.visual))}>
                  {base}
                  <span className="dim">{ext}</span>
                </span>
                <span className="row__dist">{d < 1000 ? `${d.toFixed(0)} m` : `${(d / 1000).toFixed(1)} km`}</span>
                <span className="row__actions" onClick={(e) => e.stopPropagation()} onDoubleClick={(e) => e.stopPropagation()}>
                  <button className="btn btn--small" onClick={() => gotoPlaced(row.id)}>
                    Skocz
                  </button>
                  <button className="btn btn--small" onClick={() => grabPlaced(row.id)}>
                    Przenieś
                  </button>
                  <button className="btn btn--small" title="Chowa voba tylko u Ciebie (H w świecie)" onClick={() => hidePlaced(row.id)}>
                    {row.hidden ? 'Pokaż' : 'Ukryj'}
                  </button>
                  <button
                    className={`btn btn--small btn--danger ${armed === row.id ? 'is-armed' : ''}`}
                    onClick={() => {
                      if (armed === row.id) {
                        setArmed(-1);
                        removePlaced(row.id);
                      } else setArmed(row.id);
                    }}
                  >
                    {armed === row.id ? 'Na pewno?' : 'Usuń'}
                  </button>
                </span>
              </div>
            );
          }}
        />
      )}
      <div className="pager">
        <span>
          {rows.length} z {p.rows.length} vobów
        </span>
      </div>
    </div>
  );
}
