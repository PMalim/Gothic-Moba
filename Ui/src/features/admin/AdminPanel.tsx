import { useEffect, useState } from 'react';
import { send } from '../../bridge/bridge';
import { AdminAction, type AdminPlayer } from '../../bridge/protocol';
import { adminStore, flyStore } from '../../state/stores';
import { useStore } from '../../state/store';
import './admin.css';

interface ActionDef {
  label: string;
  action: number;
  tone?: 'danger' | 'warn';
}

const PLAYER_ACTIONS: ActionDef[] = [
  { label: 'Wyrzuć', action: AdminAction.Kick, tone: 'danger' },
  { label: 'Ban', action: AdminAction.Ban, tone: 'danger' },
  { label: 'Zabij', action: AdminAction.Kill, tone: 'danger' },
  { label: 'Ulecz', action: AdminAction.Heal },
  { label: 'Odrodź', action: AdminAction.Respawn },
  { label: 'Idź do', action: AdminAction.Goto },
  { label: 'Przywołaj', action: AdminAction.Bring },
  { label: 'Zamroź', action: AdminAction.Freeze, tone: 'warn' },
  { label: 'Odmroź', action: AdminAction.Unfreeze },
];

const GLOBAL_ACTIONS: ActionDef[] = [
  { label: 'Fala minionów', action: AdminAction.Wave },
  { label: 'Zabij moby', action: AdminAction.KillMobs, tone: 'warn' },
  { label: 'Reset areny', action: AdminAction.Reset, tone: 'danger' },
];

export function AdminPanel() {
  const admin = useStore(adminStore);
  const fly = useStore(flyStore);
  const [armed, setArmed] = useState<number | null>(null);
  useEffect(() => {
    if (armed === null) return;
    const timer = window.setTimeout(() => setArmed(null), 3000);
    return () => window.clearTimeout(timer);
  }, [armed]);
  if (!admin.open) return null;

  const target = admin.players.find((p) => p.id === admin.selected);
  const press = (def: ActionDef, global: boolean) => {
    if (!global && !target) return;
    if (def.tone === 'danger' && armed !== def.action) {
      setArmed(def.action);
      return;
    }
    setArmed(null);
    send('admin.action', def.action, global ? -1 : target!.id);
  };

  return (
    <div className="admin-backdrop">
      <section className="admin" role="dialog" aria-label="Panel admina">
        <header className="admin__head">
          <div>
            <h2>Panel admina</h2>
            <p>
              Graczy: {admin.players.length} · Minionów: {admin.minions} · Wybrany: <b>{target ? target.name : '—'}</b>
            </p>
          </div>
          <button className="btn btn--ghost" onClick={() => send('admin.close')}>
            Zamknij <kbd>F6</kbd>
          </button>
        </header>

        <div className="admin__table" role="table">
          <div className="admin__row admin__row--head" role="row">
            <span>ID</span>
            <span>Gracz</span>
            <span>Zdrowie</span>
            <span>Ping</span>
            <span>Status</span>
          </div>
          <div className="admin__rows">
            {admin.players.length === 0 && <div className="admin__empty">Brak graczy.</div>}
            {admin.players.map((p) => (
              <PlayerRow key={p.id} player={p} selected={p.id === admin.selected} onPick={() => adminStore.set({ selected: p.id === admin.selected ? -1 : p.id })} />
            ))}
          </div>
        </div>

        <div className="admin__actions">
          <div className="admin__group">
            <h3>Na graczu</h3>
            <div className="admin__buttons">
              {PLAYER_ACTIONS.map((def) => (
                <button key={def.action} className={`btn ${def.tone ? `btn--${def.tone}` : ''} ${armed === def.action ? 'is-armed' : ''}`} disabled={!target} onClick={() => press(def, false)}>
                  {armed === def.action ? 'Na pewno?' : def.label}
                </button>
              ))}
            </div>
          </div>
          <div className="admin__group">
            <h3>Globalne</h3>
            <div className="admin__buttons">
              {GLOBAL_ACTIONS.map((def) => (
                <button key={def.action} className={`btn ${def.tone ? `btn--${def.tone}` : ''} ${armed === def.action ? 'is-armed' : ''}`} onClick={() => press(def, true)}>
                  {armed === def.action ? 'Na pewno?' : def.label}
                </button>
              ))}
              <button className={`btn ${fly.on ? 'btn--active' : ''}`} onClick={() => send('fly.toggle')}>
                Latanie <kbd>F7</kbd>
              </button>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

function PlayerRow({ player, selected, onPick }: { player: AdminPlayer; selected: boolean; onPick: () => void }) {
  const ratio = player.maxHp > 0 ? Math.max(0, Math.min(1, player.hp / player.maxHp)) : 0;
  return (
    <div className={`admin__row ${selected ? 'is-selected' : ''} ${player.dead ? 'is-dead' : ''}`} role="row" onClick={onPick}>
      <span className="admin__id">{player.id}</span>
      <span className="admin__name">{player.name}</span>
      <span className="admin__hp">
        <span className="admin__bar">
          <span style={{ width: `${ratio * 100}%` }} />
        </span>
        <small>
          {player.hp}/{player.maxHp}
        </small>
      </span>
      <span className="admin__ping">{player.ping} ms</span>
      <span className="admin__flags">
        {player.dead && <i className="chip chip--dead">nie żyje</i>}
        {player.ghost && <i className="chip chip--ghost">duch</i>}
        {player.admin && <i className="chip chip--admin">admin</i>}
      </span>
    </div>
  );
}
