import { useEffect, useRef } from 'react';
import { useStore } from '../../state/store';
import { lobbyStore } from '../lobby/lobbyStore';
import { KINDS, MAX_MESSAGE, MAX_TITLE, MIN_MESSAGE, PRIORITIES, feedbackStore, newFeedback, problemOf, sendFeedback, type FeedbackKind } from './feedbackStore';
import './feedback.css';

const KIND_PATH: Record<FeedbackKind, string> = {
  bug: 'M9 8a3 3 0 0 1 6 0v1H9zM7 12H3M21 12h-4M7 17l-3 2M17 17l3 2M7 7L4 5M17 7l3-2M7 9h10v5a5 5 0 0 1-10 0z M12 9v10',
  complaint: 'M5 21V4M5 5h13l-3 4 3 4H5',
  idea: 'M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.9c.6.5 1 1.2 1 2.1h5c0-.9.4-1.6 1-2.1A6 6 0 0 0 12 3',
  balance: 'M12 4v16M6 20h12M4 8h16M4 8l-2 7a3 3 0 0 0 6 0zM20 8l-2 7a3 3 0 0 0 6 0z',
  other: 'M4 5h16v11H9l-5 4z',
};

function KindIcon({ kind }: { kind: FeedbackKind }) {
  return (
    <svg viewBox="0 0 24 24" width="1.7rem" height="1.7rem" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={KIND_PATH[kind]} />
    </svg>
  );
}

export function FeedbackForm({ onBack }: { onBack: () => void }) {
  const s = useStore(feedbackStore);
  const { phase, match } = useStore(lobbyStore);
  const titleRef = useRef<HTMLInputElement>(null);
  const kind = KINDS.find((k) => k.key === s.kind) ?? KINDS[0];
  const problem = problemOf(s.title, s.message);
  const busy = s.status === 'sending';
  const where = phase === 'lobby' ? 'lobby' : `gra #${match}`;

  useEffect(() => {
    if (s.status === 'edit') titleRef.current?.focus();
  }, [s.status]);

  if (s.status === 'sent') {
    return (
      <section className="fb fb--done" role="dialog" aria-label="Zgłoszenie wysłane">
        <div className="fb__seal">
          <svg viewBox="0 0 24 24" width="2.6rem" height="2.6rem" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
            <path d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
        </div>
        <h2>Dziękujemy!</h2>
        <p>
          Zgłoszenie <b>#{s.number}</b> trafiło do bazy. Przeczytamy je, a jeśli to błąd albo dobry pomysł — zajmiemy się nim w kolejnych wersjach.
        </p>
        <div className="fb__actions fb__actions--center">
          <button className="fb-btn" onClick={newFeedback}>
            Wyślij kolejne
          </button>
          <button className="fb-btn fb-btn--primary" onClick={onBack}>
            Wróć do menu
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="fb" role="dialog" aria-label="Skarga, błąd, innowacja">
      <header className="fb__head">
        <div>
          <h2>Skarga · Błąd · Innowacja</h2>
          <p>Napisz, co poprawić. Zgłoszenie trafia prosto do bazy zespołu.</p>
        </div>
        <button className="fb__close" onClick={onBack} aria-label="Wróć do menu" title="Wróć do menu">
          <svg viewBox="0 0 24 24" width="1.1rem" height="1.1rem" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden>
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </header>

      <div className="fb__label">Rodzaj zgłoszenia</div>
      <div className="fb__kinds" role="radiogroup" aria-label="Rodzaj zgłoszenia">
        {KINDS.map((k) => (
          <button key={k.key} type="button" role="radio" aria-checked={s.kind === k.key} className={`fb-kind fb-kind--${k.key} ${s.kind === k.key ? 'is-on' : ''}`} onClick={() => feedbackStore.set({ kind: k.key })}>
            <KindIcon kind={k.key} />
            <b>{k.label}</b>
            <small>{k.hint}</small>
          </button>
        ))}
      </div>

      <div className="fb__row">
        <label className="fb__field fb__field--grow">
          <span className="fb__label">Tytuł</span>
          <input ref={titleRef} type="text" value={s.title} maxLength={MAX_TITLE} placeholder="Jednym zdaniem: czego dotyczy?" onChange={(e) => feedbackStore.set({ title: e.target.value })} />
        </label>
        <div className="fb__field">
          <span className="fb__label">Priorytet</span>
          <div className="fb__prio" role="radiogroup" aria-label="Priorytet">
            {PRIORITIES.map((p) => (
              <button key={p.value} type="button" role="radio" aria-checked={s.priority === p.value} className={`fb-prio fb-prio--${p.value} ${s.priority === p.value ? 'is-on' : ''}`} onClick={() => feedbackStore.set({ priority: p.value })}>
                {p.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      <label className="fb__field">
        <span className="fb__label">
          Opis
          <em className={s.message.trim().length < MIN_MESSAGE ? '' : 'is-ok'}>
            {s.message.length} / {MAX_MESSAGE}
          </em>
        </span>
        <textarea value={s.message} maxLength={MAX_MESSAGE} rows={7} placeholder={kind.placeholder} onChange={(e) => feedbackStore.set({ message: e.target.value })} />
      </label>

      <footer className="fb__foot">
        <span className="fb__where" title="Miejsce zapisze się razem ze zgłoszeniem">
          Zapisze się: konto i miejsce — <b>{where}</b>
        </span>
        {s.error && <span className="fb__error" role="alert">{s.error}</span>}
        <div className="fb__actions">
          <button className="fb-btn" onClick={onBack} disabled={busy}>
            Wróć
          </button>
          <button className="fb-btn fb-btn--primary" onClick={sendFeedback} disabled={busy || problem !== ''} title={problem || 'Wyślij zgłoszenie'}>
            {busy ? 'Wysyłanie…' : 'Wyślij zgłoszenie'}
          </button>
        </div>
      </footer>
      {problem && !busy && <p className="fb__problem">{problem}</p>}
    </section>
  );
}
