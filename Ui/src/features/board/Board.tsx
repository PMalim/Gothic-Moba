import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { act, heroOf, lobbyStore } from '../lobby/lobbyStore';
import { Icon } from '../lobby/ui';
import { ItemIcon } from '../shop/ItemIcon';
import { itemByKey, shopStore } from '../shop/shopStore';
import { boardStore, type BoardRow, type BoardTimer } from './boardStore';
import './board.css';

const SLOTS = 6;
const clock = (seconds: number): string => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

function Row({ row }: { row: BoardRow }) {
  const heroes = useStore(lobbyStore).heroes;
  useStore(shopStore);
  const hero = heroOf(row.hero, heroes);
  const face = PORTRAIT[row.hero]?.face;
  const kda = row.deaths === 0 ? row.kills + row.assists : (row.kills + row.assists) / row.deaths;
  return (
    <div className={`bd-row bd-row--${row.team === 0 ? 'blue' : 'red'} ${row.you ? 'is-you' : ''} ${row.alive ? '' : 'is-dead'}`}>
      <span className="bd-face">
        {face ? <img src={face} alt="" draggable={false} /> : <Icon name="heroes" size="1.6rem" />}
        {!row.alive && (
          <span className="bd-face__dead">
            <Icon name="skull" size="1.3rem" />
          </span>
        )}
        <b className="bd-face__level">{row.level}</b>
      </span>

      <span className="bd-who">
        <b className="bd-who__name">
          {row.name}
          {row.you && <em>Ty</em>}
          {row.bot ? <small className="bd-tag">BOT</small> : <small className="bd-id">#{row.id}</small>}
        </b>
        <small>{hero ? `${hero.name} · ${hero.role}` : row.hero}</small>
      </span>

      <span className="bd-lp" title="Punkty rankingowe (LP)">
        {row.bot ? (
          <i>—</i>
        ) : (
          <>
            <Icon name="shard" size="0.95rem" />
            <b>{row.lp}</b>
          </>
        )}
      </span>

      <span className="bd-kda" title={`Zabójstwa / śmierci / asysty · KDA ${kda.toFixed(1)}`}>
        <b>{row.kills}</b>
        <i>/</i>
        <b className="is-death">{row.deaths}</b>
        <i>/</i>
        <b className="is-assist">{row.assists}</b>
      </span>

      <span className="bd-cs" title="Zabite miniony (CS)">
<b>{row.cs}</b>
      </span>

      <span className="bd-stats" aria-label="Statystyki">
        <span title="Siła ataku" className="is-ad">
          <small>AD</small>
          <b>{row.ad}</b>
        </span>
        <span title="Moc magii" className="is-ap">
          <small>AP</small>
          <b>{row.ap}</b>
        </span>
        <span title="Pancerz" className="is-armor">
          <small>PAN</small>
          <b>{row.armor}</b>
        </span>
        <span title="Odporność na magię" className="is-resist">
          <small>OD</small>
          <b>{row.resist}</b>
        </span>
      </span>

      <span className="bd-items" aria-label="Przedmioty">
        {Array.from({ length: SLOTS }, (_, i) => {
          const key = row.items[i];
          const item = key ? itemByKey(key) : undefined;
          return (
            <span key={i} className={`bd-slot ${item ? `bd-slot--${item.group}` : 'is-empty'}`} title={item?.name}>
              {item && <ItemIcon itemKey={item.key} fallback={item.glyph} />}
            </span>
          );
        })}
      </span>
    </div>
  );
}

function Team({ side, rows }: { side: 'blue' | 'red'; rows: BoardRow[] }) {
  const kills = rows.reduce((sum, r) => sum + r.kills, 0);
  const deaths = rows.reduce((sum, r) => sum + r.deaths, 0);
  const assists = rows.reduce((sum, r) => sum + r.assists, 0);
  return (
    <section className={`bd-team bd-team--${side}`}>
      <header>
        <h3>{side === 'blue' ? 'Niebiescy' : 'Czerwoni'}</h3>
        <span>
          {kills} / {deaths} / {assists}
        </span>
      </header>
      <div className="bd-team__rows">
        {rows.map((r) => (
          <Row key={`${r.bot ? 'b' : 'p'}${r.id}`} row={r} />
        ))}
        {rows.length === 0 && <div className="bd-empty">Brak graczy</div>}
      </div>
    </section>
  );
}

function Timers({ timers, elapsed }: { timers: BoardTimer[]; elapsed: number }) {
  if (timers.length === 0) return null;
  return (
    <div className="bd-timers" aria-label="Liczniki celów">
      {timers.map((t) => {
        const left = Math.max(0, t.left - elapsed);
        const value = t.state === 'alive' ? 'jest' : t.state === 'off' ? '—' : t.state === 'soon' || left === 0 ? 'wkrótce' : clock(left);
        const title =
          t.state === 'alive' ? `${t.name} czeka w jamie` : t.state === 'off' ? `${t.name}: brak w tej grze` : t.state === 'soon' ? `${t.name}: jeszcze go nie ma w grze` : `${t.name} pojawi się za ${clock(left)}`;
        return (
          <span key={t.key} className={`bd-timer bd-timer--${t.key} is-${t.state}`} title={title}>
            <small>{t.name}</small>
            <b>{value}</b>
          </span>
        );
      })}
    </div>
  );
}

export function Board() {
  const s = useStore(boardStore);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (!s.visible) return;
    act('board.get');
    const timer = window.setInterval(() => {
      act('board.get');
      setNow(Date.now());
    }, 1000);
    const tick = window.setInterval(() => setNow(Date.now()), 250);
    return () => {
      window.clearInterval(timer);
      window.clearInterval(tick);
    };
  }, [s.visible]);

  if (!s.visible) return null;
  const blue = s.rows.filter((r) => r.team === 0);
  const red = s.rows.filter((r) => r.team === 1);
  const elapsed = s.at > 0 ? Math.max(0, Math.floor((now - s.at) / 1000)) : 0;
  const seconds = s.seconds + elapsed;
  return (
    <div className="bd" role="dialog" aria-label="Tabela graczy">
      <div className="bd__panel">
        <header className="bd__head">
          <span className="bd__score bd__score--blue">{s.blue}</span>
          <div className="bd__time">
            <small>Czas gry</small>
            <b>{clock(seconds)}</b>
          </div>
          <span className="bd__score bd__score--red">{s.red}</span>
        </header>

        <div className="bd__cols" aria-hidden>
          <span />
          <span>Gracz</span>
          <span>LP</span>
          <span>Z / Ś</span>
          <span>CS</span>
          <span>Statystyki</span>
          <span>Przedmioty</span>
        </div>

        <Team side="blue" rows={blue} />
        <Team side="red" rows={red} />

        <Timers timers={s.timers} elapsed={elapsed} />

        <footer className="bd__hint">
          <kbd>Tab</kbd> przytrzymaj, żeby pokazać tabelę
        </footer>
      </div>
    </div>
  );
}
