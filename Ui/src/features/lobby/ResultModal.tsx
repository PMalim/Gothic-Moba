import { useStore } from '../../state/store';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { ItemIcon } from '../shop/ItemIcon';
import { itemByKey, shopStore } from '../shop/shopStore';
import { Btn, Icon } from './ui';
import { act, heroOf, lobbyStore, type LpInfo, type ResultInfo, type ResultRow, type ResultTeam } from './lobbyStore';
import './gothic.css';
import './lobby.css';
import './result.css';

const SLOTS = 6;
const clock = (seconds: number): string => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
const num = (value: number): string => (value >= 10000 ? `${(value / 1000).toFixed(1)}k` : String(Math.round(value)));
const signed = (value: number): string => `${value > 0 ? '+' : ''}${value}`;
const MODE: Record<string, string> = { bots: 'Gra z botami', normal: 'Gra zwykła', ranked: 'Gra rankingowa' };

function Row({ row, maxDamage, won }: { row: ResultRow; maxDamage: number; won: boolean }) {
  const heroes = useStore(lobbyStore).heroes;
  const hero = heroOf(row.hero, heroes);
  const face = PORTRAIT[row.hero]?.face;
  const kda = row.deaths === 0 ? row.kills + row.assists : (row.kills + row.assists) / row.deaths;
  const share = maxDamage > 0 ? Math.max(row.dmgChampions > 0 ? 3 : 0, (row.dmgChampions / maxDamage) * 100) : 0;
  return (
    <div className={`rs-row rs-row--${row.team === 0 ? 'blue' : 'red'} ${row.you ? 'is-you' : ''}`}>
      <span className="rs-face">
        {face ? <img src={face} alt="" draggable={false} /> : <Icon name="heroes" size="1.6rem" />}
        <b className="rs-face__level">{row.level}</b>
      </span>

      <span className="rs-who">
        <b className="rs-who__name">
          {row.name}
          {row.you && <em>Ty</em>}
          {row.bot && <small className="rs-tag">BOT</small>}
          {row.left && <small className="rs-tag rs-tag--left">wyszedł</small>}
          {row.mvp && <small className={`rs-tag ${won ? 'rs-tag--mvp' : 'rs-tag--ace'}`}>{won ? 'MVP' : 'ACE'}</small>}
        </b>
        <small>{hero ? `${hero.name} · ${hero.role}` : row.hero}</small>
      </span>

      <span className="rs-kda" title={`Zabójstwa / śmierci / asysty · KDA ${kda.toFixed(1)}`}>
        <b>{row.kills}</b>
        <i>/</i>
        <b className="is-death">{row.deaths}</b>
        <i>/</i>
        <b className="is-assist">{row.assists}</b>
        <small>{kda.toFixed(1)} KDA</small>
      </span>

      <span className="rs-num" title="Zabite miniony i potwory (CS)">
        {row.cs}
      </span>
      <span className="rs-num rs-gold" title="Złoto zdobyte w grze">
        {num(row.gold)}
      </span>

      <span className="rs-dmg" title="Obrażenia zadane bohaterom wroga">
        <b>{num(row.dmgChampions)}</b>
        <i style={{ width: `${share}%` }} />
      </span>
      <span className="rs-num" title="Obrażenia w cele: wieże, inhibitory, nexus, smok, baron">
        {num(row.dmgObjectives)}
      </span>
      <span className="rs-num" title="Obrażenia w miniony i potwory">
        {num(row.dmgMinions)}
      </span>
      <span className="rs-num rs-taken" title="Obrażenia otrzymane">
        {num(row.taken)}
      </span>

      <span className="rs-items" aria-label="Przedmioty">
        {Array.from({ length: SLOTS }, (_, i) => {
          const key = row.items[i];
          const item = key ? itemByKey(key) : undefined;
          return (
            <span key={i} className={`rs-slot ${item ? `rs-slot--${item.group}` : 'is-empty'}`} title={item?.name}>
              {item && <ItemIcon itemKey={item.key} fallback={item.glyph} />}
            </span>
          );
        })}
      </span>
    </div>
  );
}

function Team({ side, rows, team, winner, maxDamage }: { side: 'blue' | 'red'; rows: ResultRow[]; team?: ResultTeam; winner: boolean; maxDamage: number }) {
  if (rows.length === 0) return null;
  const kills = team?.kills ?? rows.reduce((sum, r) => sum + r.kills, 0);
  const deaths = team?.deaths ?? rows.reduce((sum, r) => sum + r.deaths, 0);
  const assists = team?.assists ?? rows.reduce((sum, r) => sum + r.assists, 0);
  return (
    <section className={`rs-team rs-team--${side} ${winner ? 'is-winner' : ''}`}>
      <header>
        <h3>
          {side === 'blue' ? 'Niebiescy' : 'Czerwoni'}
          <em>{winner ? 'Zwycięstwo' : 'Porażka'}</em>
        </h3>
        <span className="rs-team__kda">
          {kills} / {deaths} / {assists}
        </span>
        {team && (
          <span className="rs-team__goals">
            <span title="Zniszczone wieże">
              <small>Wieże</small>
              <b>{team.towers}</b>
            </span>
            <span title="Zniszczone inhibitory">
              <small>Inhibitory</small>
              <b>{team.inhibitors}</b>
            </span>
            <span title="Zabite smoki">
              <small>Smoki</small>
              <b>{team.drakes}</b>
            </span>
            <span title="Zabite barony">
              <small>Barony</small>
              <b>{team.barons}</b>
            </span>
            <span title="Złoto drużyny">
              <small>Złoto</small>
              <b>{num(team.gold)}</b>
            </span>
            <span title="Obrażenia zadane bohaterom">
              <small>Obrażenia</small>
              <b>{num(team.damage)}</b>
            </span>
          </span>
        )}
      </header>
      <div className="rs-team__rows">
        {rows.map((r) => (
          <Row key={`${r.bot ? 'b' : 'p'}${r.id}`} row={r} maxDamage={maxDamage} won={winner} />
        ))}
      </div>
    </section>
  );
}

function LpPanel({ result, info }: { result: ResultInfo; info: LpInfo }) {
  const gain = result.lpChange >= 0;
  const score = result.won ? 1 : 0;
  const diff = info.own - info.other;
  return (
    <div className={`rs-lp ${gain ? 'is-gain' : 'is-loss'}`}>
      <div className="rs-lp__big">
        <small>Punkty rankingowe</small>
        <b>
          {signed(result.lpChange)}
          <span>LP</span>
        </b>
        <small>
          razem <strong>{result.lp}</strong> LP
        </small>
      </div>
      <ol className="rs-lp__steps">
        <li>
          <span>Średnie LP drużyn</span>
          <b>
            {info.own} vs {info.other}
            <small>
              {diff === 0 ? 'równi' : diff > 0 ? `twoja drużyna wyżej o ${diff}` : `twoja drużyna niżej o ${-diff}`}
            </small>
          </b>
        </li>
        <li>
          <span>Szansa na wygraną twojej drużyny</span>
          <b>{info.chance}%</b>
        </li>
        <li>
          <span>
            Zmiana = {info.k} × ({result.won ? 'wygrana 1' : 'przegrana 0'} − {(info.chance / 100).toFixed(2)})
          </span>
          <b>
            {info.k} × {(score - info.chance / 100).toFixed(2)} = {signed(info.raw)}
          </b>
        </li>
        <li>
          <span>Przedział dla {result.won ? 'wygranej' : 'porażki'} (od {signed(info.min)} do {signed(info.max)})</span>
          <b>{signed(info.limited)}</b>
        </li>
        {info.applied !== info.limited && (
          <li>
            <span>LP nie spada poniżej 0</span>
            <b>{signed(info.applied)}</b>
          </li>
        )}
      </ol>
    </div>
  );
}

export function ResultModal() {
  const { result } = useStore(lobbyStore);
  useStore(shopStore);
  if (!result) return null;
  const close = () => {
    lobbyStore.set({ result: null });
    act('room.leave');
  };
  const rows = result.rows ?? [];
  const blue = rows.filter((r) => r.team === 0);
  const red = rows.filter((r) => r.team === 1);
  const teams = result.teams ?? [];
  const maxDamage = rows.reduce((max, r) => Math.max(max, r.dmgChampions), 0);
  const winnerSide = result.winner === 'blue' ? 'blue' : 'red';
  const blueScore = teams.find((t) => t.team === 0)?.kills ?? blue.reduce((s, r) => s + r.kills, 0);
  const redScore = teams.find((t) => t.team === 1)?.kills ?? red.reduce((s, r) => s + r.kills, 0);
  return (
    <div className="rs-backdrop" role="dialog" aria-label="Wynik gry">
      <section className={`rs ${result.won ? 'is-win' : 'is-loss'}`}>
        <header className="rs-head">
          <div className="rs-head__title">
            <b>{result.won ? 'Zwycięstwo' : 'Porażka'}</b>
            <small>
              {winnerSide === 'blue' ? 'Wygrali niebiescy' : 'Wygrali czerwoni'} · {clock(result.seconds)} · {MODE[result.mode ?? ''] ?? (result.ranked ? MODE.ranked : 'Gra')}
            </small>
          </div>
          <div className="rs-head__score" aria-label="Wynik zabójstw">
            <span className="rs-score rs-score--blue">{blueScore}</span>
            <i>:</i>
            <span className="rs-score rs-score--red">{redScore}</span>
          </div>
        </header>

        <div className="rs-cols" aria-hidden>
          <span />
          <span>Gracz</span>
          <span>Z / Ś / A</span>
          <span>CS</span>
          <span>Złoto</span>
          <span>Obrażenia (bohaterowie)</span>
          <span>Cele</span>
          <span>Miniony</span>
          <span>Otrzymane</span>
          <span>Przedmioty</span>
        </div>

        <div className="rs-body">
          <Team side="blue" rows={blue} team={teams.find((t) => t.team === 0)} winner={winnerSide === 'blue'} maxDamage={maxDamage} />
          <Team side="red" rows={red} team={teams.find((t) => t.team === 1)} winner={winnerSide === 'red'} maxDamage={maxDamage} />
          {rows.length === 0 && <div className="rs-empty">Brak szczegółowych statystyk tej gry.</div>}
        </div>

        <footer className="rs-foot">
          <div className="rs-reward">
            <div>
              <small>Twoje punkty</small>
              <b className="gain">+{result.points} pkt</b>
            </div>
            <div>
              <small>Twoje Z / Ś / A</small>
              <b>
                {result.kills} / {result.deaths} / {result.assists}
              </b>
            </div>
            <div>
              <small>Zabite moby</small>
              <b>{result.botKills}</b>
            </div>
          </div>
          {result.ranked && result.lpInfo ? (
            <LpPanel result={result} info={result.lpInfo} />
          ) : result.ranked ? (
            <p className="rs-lp-line">
              LP: <span className={result.lpChange >= 0 ? 'gain' : 'loss'}>{signed(result.lpChange)}</span> (razem {result.lp})
            </p>
          ) : (
            <p className="rs-lp-line is-muted">Gra nierankingowa — LP bez zmian.</p>
          )}
          <div className="rs-act">
            <Btn xl variant="primary" onClick={close}>
              Wróć do lobby
            </Btn>
          </div>
        </footer>
      </section>
    </div>
  );
}
