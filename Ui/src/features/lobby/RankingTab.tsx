import { useStore } from '../../state/store';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { heroOf, lobbyStore, type RankingRow } from './lobbyStore';
import { Icon, Sigil } from './ui';

const pct = (wins: number, losses: number): number => (wins + losses > 0 ? Math.round((wins / (wins + losses)) * 100) : 0);

function WinRing({ value, size = '7.5rem' }: { value: number; size?: string }) {
  const r = 44;
  const c = 2 * Math.PI * r;
  return (
    <svg className="lb-winring" viewBox="0 0 100 100" width={size} height={size} aria-label={`${value}% wygranych`}>
      <circle cx="50" cy="50" r={r} fill="none" stroke="#1d1823" strokeWidth="7" />
      <circle cx="50" cy="50" r={r} fill="none" stroke="url(#wr)" strokeWidth="7" strokeLinecap="round" strokeDasharray={`${(c * value) / 100} ${c}`} transform="rotate(-90 50 50)" />
      <defs>
        <linearGradient id="wr" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#f0b05a" />
          <stop offset="1" stopColor="#b4361f" />
        </linearGradient>
      </defs>
      <text x="50" y="55" textAnchor="middle" className="lb-winring__num">
        {value}%
      </text>
    </svg>
  );
}

function Podium({ rows }: { rows: RankingRow[] }) {
  const order = [rows[1], rows[0], rows[2]];
  return (
    <div className="lb-podium">
      {order.map((r, i) =>
        r ? (
          <div key={r.rank} className={`lb-podium__slot lb-podium__slot--${r.rank} ${r.you ? 'is-you' : ''}`}>
            <span className="lb-podium__crown">
              <Icon name="crown" size="1.5rem" filled />
            </span>
            <span className="lb-podium__rank">{r.rank}</span>
            <b>{r.name}</b>
            <span className="lb-podium__lp">
              <Icon name="shard" size="0.95rem" /> {r.lp} LP
            </span>
            <small>
              {r.wins}W · {r.losses}L · {pct(r.wins, r.losses)}%
            </small>
          </div>
        ) : (
          <div key={`x${i}`} className="lb-podium__slot is-void" />
        ),
      )}
    </div>
  );
}

export function RankingTab() {
  const s = useStore(lobbyStore);
  const p = s.profile;
  const total = (p?.wins ?? 0) + (p?.losses ?? 0);
  const rest = s.ranking.slice(3);
  return (
    <main className="lb-split lb-split--rank">
      <section className="lb-split__main">
        <h2 className="lb-title">
          <span>Ranking LP</span>
        </h2>
        {s.ranking.length === 0 ? (
          <div className="lb-empty">Nikt jeszcze nie zagrał gry rankingowej. Bądź pierwszy.</div>
        ) : (
          <>
            <Podium rows={s.ranking.slice(0, 3)} />
            {rest.length > 0 && (
              <div className="lb-panel lb-table">
                <div className="lb-table__head">
                  <span>#</span>
                  <span>Gracz</span>
                  <span>LP</span>
                  <span>W</span>
                  <span>L</span>
                  <span>Wygr.</span>
                </div>
                <div className="lb-table__rows">
                  {rest.map((r) => (
                    <div key={r.rank} className={`lb-table__row ${r.you ? 'is-you' : ''}`}>
                      <span>{r.rank}</span>
                      <b>{r.name}</b>
                      <span>{r.lp}</span>
                      <span>{r.wins}</span>
                      <span>{r.losses}</span>
                      <span>{pct(r.wins, r.losses)}%</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </section>
      <aside className="lb-panel lb-detail lb-detail--me">
        <div className="lb-me-card">
          <span className="lb-me-card__sigil">
            <Sigil size="4.2rem" />
          </span>
          <h3>{p?.name ?? 'Ty'}</h3>
          <small>Miejsce {p?.rank ? `#${p.rank}` : '—'}</small>
        </div>
        <div className="lb-detail__scroll">
          <div className="lb-stats">
            <div className="lb-stat">
              <span>LP</span>
              <div>
                <i style={{ width: `${Math.min(100, ((p?.lp ?? 0) / 500) * 100)}%` }} />
              </div>
              <output>{p?.lp ?? 0}</output>
            </div>
          </div>
          <p className="lb-lore">LP zdobywasz w grach rankingowych: wygrana ze silniejszymi graczami daje więcej, przegrana zabiera tyle samo. Rangi nie ma — liczy się sam wynik.</p>
          <p className="lb-hint">
            Gier łącznie: <b>{total}</b> · rankingowych: <b>{p?.ranked ?? 0}</b>
          </p>
        </div>
      </aside>
    </main>
  );
}

function Tile({ label, value, icon }: { label: string; value: React.ReactNode; icon: 'coin' | 'shard' | 'crown' | 'swords' | 'skull' | 'heroes' }) {
  return (
    <div className="lb-tile">
      <span className="lb-tile__icon">
        <Icon name={icon} size="1.3rem" />
      </span>
      <b>{value}</b>
      <small>{label}</small>
    </div>
  );
}

export function ProfileTab() {
  const s = useStore(lobbyStore);
  const p = s.profile;
  const hero = heroOf(p?.hero, s.heroes);
  const total = (p?.wins ?? 0) + (p?.losses ?? 0);
  const ratio = pct(p?.wins ?? 0, p?.losses ?? 0);
  const card = p?.hero ? PORTRAIT[p.hero]?.card : undefined;
  return (
    <main className="lb-split">
      <section className="lb-split__main">
        <h2 className="lb-title">
          <span>Karta gracza</span>
        </h2>
        <div className="lb-profile">
          <div className="lb-panel lb-profile__ring">
            <WinRing value={ratio} />
            <b>{p?.name}</b>
            <small>
              {p?.wins ?? 0} wygranych · {p?.losses ?? 0} przegranych
            </small>
          </div>
          <div className="lb-tiles">
            <Tile icon="shard" label="LP (ranking)" value={p?.lp ?? 0} />
            <Tile icon="crown" label="Miejsce w rankingu" value={p?.rank ? `#${p.rank}` : '—'} />
            <Tile icon="coin" label="Punkty na bohaterów" value={p?.points ?? 0} />
            <Tile icon="swords" label="Gry łącznie" value={total} />
            <Tile icon="skull" label="Gry rankingowe" value={p?.ranked ?? 0} />
            <Tile icon="heroes" label="Bohaterowie" value={`${p?.owned?.length ?? 0} / ${s.heroes.length}`} />
          </div>
        </div>
      </section>
      <aside className="lb-panel lb-detail">
        <div className="lb-detail__hero">
          {card && <img src={card} alt={hero?.name} draggable={false} />}
          <span className="lb-detail__fade" />
          <div className="lb-detail__name">
            <small>Wybrany bohater</small>
            <h3>{hero?.name ?? 'Bohater'}</h3>
          </div>
        </div>
        <div className="lb-detail__scroll">
          <p className="lb-lore">{hero?.lore}</p>
        </div>
      </aside>
    </main>
  );
}
