import { send } from '../../bridge/bridge';
import { builderStore } from '../../state/stores';
import { useStore } from '../../state/store';
import { builderUi, placedStore, type ManagerTab } from './builderUi';
import { categoryIdOf, categoryTitle, splitName } from './catalog';
import { GalleryTab, ListTab } from './CatalogTabs';
import { PlacedTab } from './Placed';
import { Backdrop, PreviewHole } from './Previews';
import { Transform } from './Transform';
import './builder.css';

export function Builder() {
  const b = useStore(builderStore);
  if (!b.on) return null;
  return b.panelFocus ? <Manager /> : <Hud />;
}

function SelectedName({ name }: { name: string }) {
  const parts = name ? splitName(name) : null;
  return (
    <div className="selected">
      <div className="selected__name" title={name}>
        {parts ? (
          <>
            {parts[0]}
            <span className="dim">{parts[1]}</span>
          </>
        ) : (
          <span className="dim">nic nie wybrano</span>
        )}
      </div>
      {name && <div className="selected__cat">{categoryTitle(categoryIdOf(name))}</div>}
    </div>
  );
}

function Hud() {
  const b = useStore(builderStore);
  const { selected } = useStore(builderUi);
  const grabbing = b.mode === 'grab';
  const holding = b.holding;
  const badge = b.locked ? 'Kamera zablokowana' : grabbing ? `Przenoszenie #${b.grabbed}` : holding ? 'Obiekt na środku' : 'Wolna kamera';
  return (
    <aside className="hud" aria-label="Budowniczy świata">
      <Backdrop />
      <header className="hud__head">
        <h2>Budowniczy</h2>
        <span className={`badge ${b.locked ? 'is-lock' : holding ? 'is-grab' : ''}`}>{badge}</span>
      </header>
      {holding && <PreviewHole className="preview-hole--small" />}
      {holding && <SelectedName name={grabbing ? '' : selected} />}
      {!holding && b.hoverId >= 0 && (
        <div className="hover">
          Pod celownikiem: <b>#{b.hoverId}</b> {b.hover} — <kbd>LPM</kbd> chwyć
        </div>
      )}
      {holding && (
        <dl className="readout">
          <div><dt>Obrót</dt><dd>{b.yaw.toFixed(1)}°</dd></div>
          <div><dt>Pochylenie</dt><dd>{b.pitch.toFixed(1)}°</dd></div>
          <div><dt>Przechylenie</dt><dd>{b.roll.toFixed(1)}°</dd></div>
          <div><dt>Skala</dt><dd>{Math.round(b.scale * 100)}%</dd></div>
          <div><dt>Wysokość</dt><dd>{Math.round(b.lift)} cm</dd></div>
          <div><dt>Kolizja</dt><dd>{b.collision ? 'tak' : 'nie'}</dd></div>
        </dl>
      )}
      <button className="btn btn--primary" onClick={() => send('builder.action', 'panel')}>
        Katalog i postawione <kbd>Tab</kbd>
      </button>
      {holding ? (
        <ul className="keys">
          <li>Latasz normalnie (mysz, <kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd>, <kbd>Spacja</kbd> / <kbd>X</kbd>, <kbd>Shift</kbd> szybciej) — obiekt wisi na środku ekranu, kółko = jak daleko</li>
          <li><kbd>Num 4</kbd><kbd>Num 6</kbd> obrót · <kbd>Num 8</kbd><kbd>Num 2</kbd> pochylenie · <kbd>Num 7</kbd><kbd>Num 9</kbd> przechylenie · <kbd>Num 5</kbd> zeruj · <kbd>Num +</kbd><kbd>Num −</kbd> skala (dotknięcie = malutki krok, <kbd>Shift</kbd> = więcej)</li>
          <li><kbd>CapsLock</kbd> {b.locked ? 'odblokuj kamerę (obiekt wraca na środek ekranu)' : 'zamroź kamerę i ruszaj tylko obiektem:'}</li>
          {b.locked && (
            <li><kbd>↑</kbd><kbd>↓</kbd><kbd>←</kbd><kbd>→</kbd> ruch po mapie · <kbd>PgUp</kbd><kbd>PgDn</kbd> wysokość · <kbd>LPM</kbd> przytrzymany + mysz przesuwa (<kbd>Shift</kbd> = szybciej)</li>
          )}
          <li><kbd>LPM</kbd>{b.locked ? <> / <kbd>Spacja</kbd></> : null} {grabbing ? 'zapisz' : 'postaw'} · <kbd>PPM</kbd> / <kbd>Esc</kbd> anuluj{grabbing ? <> · <kbd>Del</kbd> usuń</> : null} · <kbd>H</kbd> schowaj</li>
        </ul>
      ) : (
        <ul className="keys">
          <li>Mysz i <kbd>W</kbd><kbd>A</kbd><kbd>S</kbd><kbd>D</kbd> latasz, <kbd>Spacja</kbd> / <kbd>X</kbd> góra / dół, <kbd>Shift</kbd> szybciej</li>
          <li><kbd>LPM</kbd> na obrysowanym obiekcie — chwyć go i przenieś</li>
          <li><kbd>Tab</kbd> katalog: wybierz obiekt i kliknij „Buduj”, żeby wziąć nowy do ręki</li>
          <li><kbd>H</kbd> schowaj obiekt pod celownikiem · <kbd>Shift</kbd>+<kbd>H</kbd> pokaż wszystkie{b.hidden > 0 ? ` (schowane: ${b.hidden})` : ''}</li>
        </ul>
      )}
      <div className="hud__foot">
        {b.vobs} vobów · {b.storage || 'brak magazynu'} · <kbd>F8</kbd> wyjście
      </div>
      {b.note && <div className="toast">{b.note}</div>}
    </aside>
  );
}

const TABS: { id: ManagerTab; label: string }[] = [
  { id: 'gallery', label: 'Galeria 3D' },
  { id: 'list', label: 'Lista' },
  { id: 'placed', label: 'Postawione' },
];

function Manager() {
  const b = useStore(builderStore);
  const ui = useStore(builderUi);
  const placed = useStore(placedStore);

  return (
    <div className="manager" role="dialog" aria-label="Budowniczy świata">
      <Backdrop />
      <header className="manager__head">
        <h2>Budowniczy świata</h2>
        <nav className="tabs" aria-label="Zakładki">
          {TABS.map((t) => (
            <button key={t.id} className={`tab ${ui.tab === t.id ? 'is-active' : ''}`} onClick={() => builderUi.set({ tab: t.id })}>
              {t.label}
              {t.id === 'placed' && <span className="chip__count">{placed.rows.length || b.vobs}</span>}
            </button>
          ))}
        </nav>
        <span className="manager__hint">
          <kbd>Tab</kbd> / <kbd>Esc</kbd> – wróć do kamery
        </span>
      </header>
      <div className="manager__body">
        <main className="manager__main">
          {ui.tab === 'gallery' && <GalleryTab />}
          {ui.tab === 'list' && <ListTab />}
          {ui.tab === 'placed' && <PlacedTab />}
        </main>
        <aside className="manager__side">
          <PreviewHole />
          <SelectedName name={ui.selected} />
          <Transform />
          <div className="side__actions">
            {b.mode === 'grab' ? (
              <>
                <button className="btn btn--danger" onClick={() => send('builder.action', 'delete')}>
                  Usuń chwyconego <kbd>Del</kbd>
                </button>
                <button className="btn" onClick={() => send('builder.action', 'cancel')}>
                  Anuluj
                </button>
              </>
            ) : (
              <button className="btn" onClick={() => send('builder.action', 'reset')}>
                Zeruj obrót i skalę
              </button>
            )}
            <button className="btn" onClick={() => send('builder.action', 'lock')}>
              {b.locked ? 'Odblokuj kamerę' : 'Zablokuj kamerę'} <kbd>CapsLock</kbd>
            </button>
            <button className="btn btn--primary" disabled={!ui.selected} onClick={() => send('builder.action', 'look')}>
              Buduj <kbd>Tab</kbd>
            </button>
            <button className="btn btn--ghost" onClick={() => send('builder.action', 'exit')}>
              Zamknij <kbd>F8</kbd>
            </button>
          </div>
          {b.note && <div className="toast">{b.note}</div>}
        </aside>
      </div>
    </div>
  );
}
