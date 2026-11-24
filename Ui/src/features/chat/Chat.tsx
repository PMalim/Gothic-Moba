import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { send } from '../../bridge/bridge';
import type { ChatCommand, Rgb } from '../../bridge/protocol';
import { chatStore, uiStore, type ChatLine } from '../../state/stores';
import { useStore } from '../../state/store';
import './chat.css';

const VISIBLE_MS = 11_000;
const FADE_MS = 1_800;
const CLOSED_LINES = 8;
const MAX_INPUT = 220;
const HISTORY_LIMIT = 40;

const rgb = (c: Rgb): string => `rgb(${c[0]}, ${c[1]}, ${c[2]})`;

function useNow(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), intervalMs);
    return () => window.clearInterval(id);
  }, [intervalMs]);
  return now;
}

type ChatFit = { width?: number; scale: number; bottom?: number };

const CHAT_MAX = 34 * 16;
const CHAT_MIN = 15 * 16;
const CHAT_GAP = 18;

function useChatFit(section: React.RefObject<HTMLElement>): ChatFit {
  const [fit, setFit] = useState<ChatFit>({ scale: 1 });
  useEffect(() => {
    const measure = () => {
      const el = section.current;
      const hud = document.querySelector('.hh__stats') ?? document.querySelector('.hh');
      const panel = document.querySelector('.hh');
      if (!el || !hud || !panel) {
        setFit((f) => (f.width === undefined && f.bottom === undefined && f.scale === 1 ? f : { scale: 1 }));
        return;
      }
      const left = el.getBoundingClientRect().left;
      const room = Math.floor(hud.getBoundingClientRect().left - left - CHAT_GAP);
      let next: ChatFit;
      if (room >= CHAT_MIN) {
        const width = Math.min(CHAT_MAX, Math.floor(window.innerWidth * 0.42), room);
        next = { width, scale: Math.max(0.9, Math.min(1, width / CHAT_MAX + 0.35)) };
      } else {
        const top = panel.getBoundingClientRect().top;
        next = { scale: 0.9, bottom: Math.ceil(window.innerHeight - top + CHAT_GAP) };
      }
      setFit((f) => (f.width === next.width && f.scale === next.scale && f.bottom === next.bottom ? f : next));
    };
    measure();
    const id = window.setInterval(measure, 400);
    window.addEventListener('resize', measure);
    return () => {
      window.clearInterval(id);
      window.removeEventListener('resize', measure);
    };
  }, [section]);
  return fit;
}

function matchCommands(commands: ChatCommand[], draft: string, admin: boolean): ChatCommand[] {
  if (!draft.startsWith('/') || draft.includes(' ')) return [];
  const query = draft.slice(1).toLowerCase();
  return commands
    .filter((c) => (admin || !c.admin) && [c.name, ...c.aliases].some((n) => n.toLowerCase().startsWith(query)))
    .slice(0, 7);
}

function currentCommand(commands: ChatCommand[], draft: string): ChatCommand | undefined {
  if (!draft.startsWith('/')) return undefined;
  const word = draft.slice(1).split(' ')[0].toLowerCase();
  if (!word) return undefined;
  return commands.find((c) => [c.name, ...c.aliases].some((n) => n.toLowerCase() === word));
}

export function Chat() {
  const sectionRef = useRef<HTMLElement>(null);
  const fit = useChatFit(sectionRef);
  const chat = useStore(chatStore);
  const ui = useStore(uiStore);
  const now = useNow(250);

  const [draft, setDraft] = useState('');
  const [hint, setHint] = useState(0);
  const history = useRef<string[]>([]);
  const historyAt = useRef(-1);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const stickToBottom = useRef(true);
  const openedAt = useRef(0);

  useEffect(() => {
    if (!chat.open) {
      inputRef.current?.blur();
      return;
    }
    openedAt.current = Date.now();
    setDraft(chat.prefill);
    setHint(0);
    historyAt.current = -1;
    stickToBottom.current = true;
    const focus = () => {
      window.focus();
      const el = inputRef.current;
      if (!el) return;
      el.focus();
      el.setSelectionRange(el.value.length, el.value.length);
    };
    focus();
    const frame = requestAnimationFrame(focus);
    const early = window.setTimeout(focus, 60);
    const late = window.setTimeout(focus, 250);
    return () => {
      cancelAnimationFrame(frame);
      window.clearTimeout(early);
      window.clearTimeout(late);
    };
  }, [chat.open, chat.openCount, chat.prefill]);

  const visibleLines = useMemo(() => {
    if (chat.open) return chat.lines;
    return chat.lines.filter((l) => now - l.at < VISIBLE_MS + FADE_MS).slice(-CLOSED_LINES);
  }, [chat.lines, chat.open, now]);

  useLayoutEffect(() => {
    const el = listRef.current;
    if (el && stickToBottom.current) el.scrollTop = el.scrollHeight;
  }, [visibleLines, chat.open]);

  const hints = matchCommands(chat.commands, draft, ui.admin);
  const active = currentCommand(chat.commands, draft);

  const close = () => {
    setDraft('');
    chatStore.set({ open: false });
    send('chat.closed');
  };

  const submit = () => {
    const text = draft.trim();
    if (text) {
      send('chat.send', text);
      history.current = [text, ...history.current.filter((h) => h !== text)].slice(0, HISTORY_LIMIT);
    }
    close();
  };

  const complete = (command: ChatCommand) => {
    setDraft(`/${command.name} `);
    setHint(0);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    switch (e.key) {
      case 'Enter':
        e.preventDefault();
        if (Date.now() - openedAt.current > 180) submit();
        break;
      case 'Escape':
        e.preventDefault();
        close();
        break;
      case 'Tab':
        e.preventDefault();
        if (hints[hint]) complete(hints[hint]);
        break;
      case 'ArrowUp':
        e.preventDefault();
        if (hints.length) setHint((h) => (h + hints.length - 1) % hints.length);
        else if (historyAt.current + 1 < history.current.length) setDraft(history.current[++historyAt.current]);
        break;
      case 'ArrowDown':
        e.preventDefault();
        if (hints.length) setHint((h) => (h + 1) % hints.length);
        else if (historyAt.current > 0) setDraft(history.current[--historyAt.current]);
        else {
          historyAt.current = -1;
          setDraft('');
        }
        break;
    }
  };

  return (
    <section
      ref={sectionRef}
      className={`chat ${chat.open ? 'chat--open' : ''}`}
      aria-label="Czat"
      style={{
        width: fit.width,
        bottom: fit.bottom,
        fontSize: fit.scale !== 1 ? `calc(0.95rem * ${fit.scale})` : undefined,
      }}
    >
      <div
        ref={listRef}
        className="chat__lines"
        onScroll={(e) => {
          const el = e.currentTarget;
          stickToBottom.current = el.scrollTop + el.clientHeight >= el.scrollHeight - 6;
        }}
      >
        {visibleLines.map((line) => (
          <ChatRow key={line.id} line={line} now={now} open={chat.open} />
        ))}
      </div>

      {chat.open && (
        <div className="chat__composer">
          {hints.length > 0 && (
            <ul className="chat__hints" role="listbox">
              {hints.map((c, i) => (
                <li key={c.name} className={i === hint ? 'is-active' : ''} onMouseDown={(e) => (e.preventDefault(), complete(c))}>
                  <span className="chat__hint-name">/{c.name}</span>
                  {c.aliases.length > 0 && <span className="chat__hint-alias">{c.aliases.map((a) => `/${a}`).join(' ')}</span>}
                  {c.admin && <span className="chat__hint-badge">admin</span>}
                  <span className="chat__hint-desc">{c.description}</span>
                </li>
              ))}
            </ul>
          )}
          {hints.length === 0 && active && (
            <div className="chat__usage">
              <b>/{active.name}</b> {active.description}
            </div>
          )}
          <div className="chat__input-row">
            <span className="chat__channel">Wszyscy</span>
            <input
              ref={inputRef}
              className="chat__input"
              value={draft}
              maxLength={MAX_INPUT}
              placeholder="Napisz wiadomość… (/ – komendy)"
              spellCheck={false}
              autoComplete="off"
              onChange={(e) => {
                setDraft(e.target.value);
                setHint(0);
              }}
              onKeyDown={onKeyDown}
            />
            <span className="chat__counter">{MAX_INPUT - draft.length}</span>
          </div>
          <div className="chat__tips">Enter – wyślij · Esc – zamknij · Tab – dokończ komendę · ↑↓ – historia</div>
        </div>
      )}
    </section>
  );
}

function ChatRow({ line, now, open }: { line: ChatLine; now: number; open: boolean }) {
  const age = now - line.at;
  const fading = !open && age > VISIBLE_MS;
  const style = fading ? { opacity: Math.max(0, 1 - (age - VISIBLE_MS) / FADE_MS) } : undefined;
  return (
    <div className={`chat__line chat__line--${line.kind}`} style={style}>
      <span className="chat__time">{line.time}</span>
      {line.kind === 'player' && line.author ? (
        <>
          <span className="chat__author" style={{ color: line.authorColor ? rgb(line.authorColor) : undefined }}>
            {line.author}
          </span>
          <span className="chat__sep">:</span>
        </>
      ) : null}
      <span className="chat__text" style={{ color: rgb(line.color) }}>
        {line.text}
      </span>
    </div>
  );
}
