import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { Btn, Icon } from './ui';
import { act, heroOf, lobbyStore, type HeroInfo, type Mode, type RoomInfo, type RoomPlayer, type RoomRow } from './lobbyStore';

const MODE_NAME: Record<Mode, string> = { bots: 'Z botami', normal: 'Z graczami', ranked: 'Rankingowa' };
const STATE_NAME: Record<RoomRow['state'], string> = { room: 'otwarta', countdown: 'start…', running: 'trwa', finished: 'koniec' };

const joinable = (r: RoomRow): boolean => r.state === 'room' || r.state === 'countdown';

const clock = (seconds: number): string => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;

const MODES: { id: Mode; name: string; tag: string; text: string; meta: string; art: string; icon: 'bot' | 'swords' | 'crown' }[] = [
  {
    id: 'bots',
    name: 'Z botami',
    tag: 'Trening',
    text: 'Ty i przyjaciele przeciw falom minionów i wieżom prowadzonym przez AI.',
    meta: 'Do 3 graczy · nagroda ×½',
    art: 'thorus',
    icon: 'bot',
  },
  {
    id: 'normal',
    name: 'Z graczami',
    tag: 'Zwykła',
    text: 'Własny pokój: niebiescy kontra czerwoni. Niszczcie wieże wroga.',
    meta: 'Do 3 na 3 · pełna nagroda',
    art: 'lee',
    icon: 'swords',
  },
  {
    id: 'ranked',
    name: 'Rankingowa',
    tag: 'Wkrótce',
    text: 'Kolejka graczy o zbliżonym LP. Wygrana daje punkty, przegrana je zabiera.',
    meta: 'W trakcie tworzenia',
    art: 'xardas',
    icon: 'crown',
  },
];

export function PlayTab() {
  const s = useStore(lobbyStore);
  return s.room ? <RoomView room={s.room} heroes={s.heroes} /> : <GameList />;
}

function GameList() {
  const s = useStore(lobbyStore);
  const [mode, setMode] = useState<Mode>('bots');
  const [size, setSize] = useState(1);
  const queue = s.queue;
  const selected = s.rooms.find((r) => r.id === s.selectedRoom);
  const [now, setNow] = useState(0);
  useEffect(() => {
    if (!queue.active) return;
    const timer = window.setInterval(() => setNow((n) => n + 1), 1000);
    return () => window.clearInterval(timer);
  }, [queue.active]);
  const waited = queue.since + now;
  const loaded = !!s.profile?.loaded;

  const pick = (m: Mode) => {
    setMode(m);
    if (m === 'bots') setSize((n) => Math.min(n, 3));
    else if (m === 'normal') setSize((n) => (n < 2 ? 2 : n));
  };
  const sizes = [1, 2, 3];
  const create = () => act('room.create', { mode, teamSize: Math.min(size, 3) });

  return (
    <main className="lb-play">
      <section className="lb-play__main">
        <h2 className="lb-title">
          <span>Wybierz tryb gry</span>
        </h2>
        <div className="lb-modes">
          {MODES.map((m) => (
            <button key={m.id} className={`lb-mode lb-mode--${m.id} ${mode === m.id ? 'is-active' : ''}`} onClick={() => pick(m.id)}>
              <span className="lb-mode__in">
                <img className="lb-mode__art" src={PORTRAIT[m.art]?.card} alt="" draggable={false} />
                <span className="lb-mode__shade" />
                <span className="lb-mode__body">
                  <span className="lb-mode__icon">
                    <Icon name={m.icon} size="1.7rem" />
                  </span>
                  <small>{m.tag}</small>
                  <b>{m.name}</b>
                  <em>{m.text}</em>
                  <span className="lb-mode__meta">{m.meta}</span>
                </span>
              </span>
            </button>
          ))}
        </div>

        <div className="lb-launch">
          {mode !== 'ranked' && !queue.active && (
            <div className="lb-size">
              <span>{mode === 'bots' ? 'Graczy' : 'W drużynie'}</span>
              <div>
                {sizes.map((n) => (
                  <button key={n} className={size === n ? 'is-active' : ''} onClick={() => setSize(n)}>
                    {n}
                  </button>
                ))}
              </div>
            </div>
          )}
          {queue.active ? (
            <div className="lb-launch__row">
              <button className="lb-cancel" title="Anuluj szukanie" onClick={() => act('queue.leave')}>
                <Icon name="close" size="1.1rem" />
              </button>
              <Btn xl variant="primary" className="is-pulsing" disabled>
                W kolejce · {clock(waited)}
              </Btn>
            </div>
          ) : mode === 'ranked' ? (
            <div className="lb-launch__locked">
              <Btn xl variant="primary" disabled>
                W trakcie tworzenia
              </Btn>
              <p>Gry rankingowe są w trakcie tworzenia. Na razie zagraj z botami albo z graczami.</p>
            </div>
          ) : (
            <Btn xl variant="primary" disabled={!loaded} onClick={create}>
              Stwórz pokój
            </Btn>
          )}
        </div>
      </section>

      <aside className="lb-play__side">
        {queue.active && (
          <section className="lb-queue">
            <small>Szukanie rozgrywki</small>
            <div className="lb-queue__time">{clock(waited)}</div>
            <p>
              W kolejce: <b>{queue.waiting}</b>. Gra złoży się z graczy o zbliżonym LP, gdy będzie ich co najmniej dwóch (do sześciu).
            </p>
          </section>
        )}

        <section className="lb-panel lb-rooms">
          <h3 className="lb-panel__title">
            Trwające gry <span>{s.rooms.length}</span>
          </h3>
          <div className="lb-rooms__list">
            {s.rooms.length === 0 && <div className="lb-empty">Cisza przed bitwą. Nie trwa żadna gra — stwórz własną.</div>}
            {s.rooms.map((r) => (
              <div
                key={r.id}
                className={`lb-room-row lb-room-row--${r.mode} ${s.selectedRoom === r.id ? 'is-selected' : ''}`}
                onClick={() => lobbyStore.set({ selectedRoom: r.id })}
                onDoubleClick={() => joinable(r) && act('room.join', { id: r.id })}
              >
                <span className="lb-room-row__dot" />
                <span className="lb-room-row__title">
                  <b>{r.title}</b>
                  <small>
                    {MODE_NAME[r.mode]} · gospodarz {r.host}
                  </small>
                </span>
                <span className="lb-room-row__count">
                  <b>
                    {r.players}/{r.max}
                  </b>
                  <small className={joinable(r) ? 'is-open' : ''}>
                    {r.state === 'running' ? clock(r.seconds) : STATE_NAME[r.state]}
                    {!!r.bots && ` · +${r.bots} bot.`}
                  </small>
                </span>
              </div>
            ))}
          </div>
          <div className="lb-panel__foot">
            <Btn small variant="ghost" disabled={!selected || !joinable(selected)} onClick={() => selected && act('room.join', { id: selected.id })}>
              Dołącz do gry
            </Btn>
            <span className="lb-hint">
              <kbd>2×</kbd> klik = dołącz
            </span>
          </div>
        </section>
      </aside>
    </main>
  );
}

const faceOf = (hero: string): string | undefined => PORTRAIT[hero]?.face;
const cardOf = (hero: string): string | undefined => PORTRAIT[hero]?.card;

function Banner({ p, heroes, side, removable }: { p: RoomPlayer; heroes: HeroInfo[]; side: 'blue' | 'red'; removable: boolean }) {
  const hero = heroOf(p.hero, heroes);
  const card = cardOf(p.hero);
  const face = faceOf(p.hero);
  return (
    <div className={`lb-banner lb-banner--${side} ${p.you ? 'is-you' : ''} ${p.bot ? 'is-bot' : ''}`}>
      <div className="lb-banner__in">
        {card && <img className="lb-banner__art" src={card} alt="" draggable={false} />}
        <span className="lb-banner__shade" />
        <span className="lb-banner__flags">
          {p.host && (
            <span className="lb-banner__crown" title="Gospodarz">
              <Icon name="crown" size="1.05rem" filled />
            </span>
          )}
          {p.bot && <span className="lb-banner__bot">BOT</span>}
          {p.bot && removable && (
            <button className="lb-banner__x" title="Usuń bota" onClick={() => act('bot.remove', { id: p.id })}>
              <Icon name="close" size="0.8rem" />
            </button>
          )}
        </span>
        <span className="lb-banner__body">
          <span className="lb-ring">{face ? <img src={face} alt="" draggable={false} /> : <Icon name="heroes" size="2.2rem" />}</span>
          <b className="lb-banner__name">{p.name}</b>
          <span className="lb-banner__hero">{hero?.name ?? p.hero}</span>
          <small>{hero?.role ?? ''}</small>
          {!p.bot && (
            <span className="lb-banner__lp">
              <Icon name="shard" size="0.85rem" /> {p.lp} LP
            </span>
          )}
        </span>
      </div>
    </div>
  );
}

function EmptySlot({ team, heroes, canAdd }: { team: 'blue' | 'red'; heroes: HeroInfo[]; canAdd: boolean }) {
  const [open, setOpen] = useState(false);
  return (
    <div className={`lb-banner lb-banner--${team} is-empty ${canAdd ? 'can-add' : ''} ${open ? 'is-open' : ''}`}>
      <div className="lb-banner__in">
        {open ? (
          <div className="lb-picker">
            <small>Wybierz bohatera bota</small>
            <div className="lb-picker__list">
              {heroes.map((h) => (
                <button
                  key={h.key}
                  onClick={() => {
                    act('bot.add', { team, hero: h.key });
                    setOpen(false);
                  }}
                >
                  <span className="lb-picker__face">{faceOf(h.key) && <img src={faceOf(h.key)} alt="" draggable={false} />}</span>
                  <span>{h.name}</span>
                </button>
              ))}
            </div>
            <button className="lb-picker__close" onClick={() => setOpen(false)}>
              Anuluj
            </button>
          </div>
        ) : (
          <button className="lb-portal" disabled={!canAdd} onClick={() => setOpen(true)} title={canAdd ? 'Dodaj bota' : 'Wolne miejsce'}>
            <span className="lb-portal__ring">{canAdd && <Icon name="plus" size="1.6rem" />}</span>
            <small>{canAdd ? 'Dodaj bota' : 'Wolne miejsce'}</small>
          </button>
        )}
      </div>
    </div>
  );
}

function Team({ side, players, size, room, heroes, removable }: { side: 'blue' | 'red'; players: RoomPlayer[]; size: number; room: RoomInfo; heroes: HeroInfo[]; removable: boolean }) {
  const empty = Math.max(0, size - players.length);
  return (
    <section className={`lb-team lb-team--${side}`}>
      <h4>{side === 'blue' ? 'Niebiescy' : 'Czerwoni'}</h4>
      <div className="lb-team__slots">
        {players.map((p) => (
          <Banner key={p.bot ? `bot${p.id}` : p.name} p={p} heroes={heroes} side={side} removable={removable} />
        ))}
        {Array.from({ length: empty }, (_, i) => (
          <EmptySlot key={`e${i}`} team={side} heroes={heroes} canAdd={removable && !!room.botSlots?.[side]} />
        ))}
      </div>
    </section>
  );
}

function RoomView({ room, heroes }: { room: RoomInfo; heroes: HeroInfo[] }) {
  const s = useStore(lobbyStore);
  const owned = new Set(s.profile?.owned ?? []);
  const mine = heroes.filter((h) => owned.has(h.key));
  const [tick, setTick] = useState(() => Date.now());
  useEffect(() => {
    if (room.state !== 'countdown') return;
    const timer = window.setInterval(() => setTick(Date.now()), 250);
    return () => window.clearInterval(timer);
  }, [room.state]);
  const countdown = Math.max(0, room.countdown - Math.floor((tick - s.roomAt) / 1000));
  const inLobbyPhase = room.state === 'room' || room.state === 'countdown';
  const botEditable = room.isHost && inLobbyPhase && room.mode !== 'ranked';
  const size = Math.max(room.teamSize, 1);
  const current = heroOf(s.profile?.hero, heroes);

  return (
    <main className="lb-room">
      <header className="lb-room__head">
        <small>
          {MODE_NAME[room.mode]} · {room.mode === 'bots' ? `${room.teamSize} graczy` : `${room.teamSize} w drużynie`}
        </small>
        <h2>{room.title}</h2>
        {room.state === 'countdown' ? (
          <div className="lb-countdown" aria-live="polite">
            <span>Start za</span>
            <b>{clock(countdown)}</b>
          </div>
        ) : (
          room.mode === 'bots' && <p>Czerwoni: fale minionów i wieże prowadzone przez AI.</p>
        )}
      </header>

      <div className="lb-room__teams">
        <Team side="blue" players={room.blue} size={size} room={room} heroes={heroes} removable={botEditable} />
        <div className="lb-vs" aria-hidden>
          <i />
          <b>VS</b>
          <i />
        </div>
        <Team side="red" players={room.red} size={size} room={room} heroes={heroes} removable={botEditable} />
      </div>

      {inLobbyPhase && (
        <div className="lb-tray">
          <div className="lb-tray__label">
            <small>Twój bohater</small>
            <b>{current?.name ?? '—'}</b>
          </div>
          <div className="lb-tray__list">
            {mine.map((h) => (
              <button key={h.key} className={`lb-tray__hero ${s.profile?.hero === h.key ? 'is-active' : ''}`} title={`${h.name} — ${h.role}`} onClick={() => act('hero.select', { key: h.key })}>
                {faceOf(h.key) ? <img src={faceOf(h.key)} alt={h.name} draggable={false} /> : <Icon name="heroes" />}
              </button>
            ))}
          </div>
        </div>
      )}
      {!inLobbyPhase && <div className="lb-tray lb-tray--text">Gra trwa.</div>}

      <footer className="lb-room__actions">
        <Btn variant="danger" onClick={() => act('room.leave')}>
          Opuść pokój
        </Btn>
        {inLobbyPhase && room.mode !== 'ranked' ? (
          <Btn xl variant="primary" disabled={!room.canStart} onClick={() => act('room.start')}>
            {room.canStart ? (room.state === 'countdown' ? 'Start teraz' : 'Start') : 'Czekaj na gospodarza'}
          </Btn>
        ) : (
          <span />
        )}
        {room.state === 'room' && room.mode === 'normal' ? (
          <Btn variant="ghost" onClick={() => act('room.team')}>
            Zmień drużynę
          </Btn>
        ) : (
          <span />
        )}
      </footer>
    </main>
  );
}
