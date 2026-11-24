import { useEffect, useState } from 'react';
import { send } from '../../bridge/bridge';
import { menuStore } from '../../state/stores';
import { act, lobbyStore } from '../lobby/lobbyStore';
import { useStore } from '../../state/store';
import { FeedbackForm } from '../feedback/FeedbackForm';
import './menu.css';

export function GameMenu() {
  const { open } = useStore(menuStore);
  const { phase, room, queue, auth } = useStore(lobbyStore);
  const [armed, setArmed] = useState(false);
  const [view, setView] = useState<'menu' | 'feedback'>('menu');

  useEffect(() => {
    if (!open) {
      setArmed(false);
      setView('menu');
    }
  }, [open]);
  useEffect(() => {
    if (!armed) return;
    const timer = window.setTimeout(() => setArmed(false), 3000);
    return () => window.clearTimeout(timer);
  }, [armed]);

  if (!open) return null;
  const lobby = phase === 'lobby';
  if (view === 'feedback') {
    return (
      <div className="menu-backdrop">
        <FeedbackForm onBack={() => setView('menu')} />
      </div>
    );
  }
  const exit = (
    <button className={`${lobby ? 'pbtn pbtn--danger' : 'btn btn--danger'} menu__button ${armed ? 'is-armed' : ''}`} onClick={() => (armed ? send('menu.exit') : setArmed(true))}>
      {armed ? 'Na pewno wyjść?' : 'Wyjdź z gry'}
    </button>
  );

  if (lobby) {
    return (
      <div className="menu-backdrop">
        <section className="menu menu--lobby" role="dialog" aria-label="Menu lobby">
          <h2>Lobby</h2>
          <p className="menu__sub">Menu</p>
          <button className="pbtn menu__button" autoFocus onClick={() => send('menu.close')}>
            Wróć <kbd>Esc</kbd>
          </button>
          {queue.active && (
            <button
              className="pbtn pbtn--ghost menu__button"
              onClick={() => {
                act('queue.leave');
                send('menu.close');
              }}
            >
              Anuluj szukanie gry
            </button>
          )}
          {room && (
            <button
              className="pbtn pbtn--ghost menu__button"
              onClick={() => {
                act('room.leave');
                send('menu.close');
              }}
            >
              Opuść pokój
            </button>
          )}
          {auth.status === 'ok' && (
            <button
              className="pbtn pbtn--ghost menu__button"
              onClick={() => {
                act('auth.logout');
                send('menu.close');
              }}
            >
              Wyloguj ({auth.name})
            </button>
          )}
          <button className="pbtn pbtn--ghost menu__button" onClick={() => setView('feedback')}>
            Skarga / Błąd / Innowacja
          </button>
          {exit}
          <p className="menu__hint">
            <kbd>1</kbd>–<kbd>5</kbd> zakładki lobby
          </p>
        </section>
      </div>
    );
  }

  return (
    <div className="menu-backdrop">
      <section className="menu" role="dialog" aria-label="Menu">
        <h2>Menu</h2>
        <button className="btn btn--primary menu__button" autoFocus onClick={() => send('menu.close')}>
          Wznów grę <kbd>Esc</kbd>
        </button>
        <button
          className="btn menu__button"
          onClick={() => {
            act('room.leave');
            send('menu.close');
          }}
        >
          Opuść grę (do lobby)
        </button>
        <button className="btn menu__button" onClick={() => setView('feedback')}>
          Skarga / Błąd / Innowacja
        </button>
        {exit}
        <p className="menu__hint">
          <kbd>Enter</kbd> czat · <kbd>F5</kbd> lista graczy
        </p>
      </section>
    </div>
  );
}
