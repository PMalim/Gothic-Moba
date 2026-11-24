import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';
import { act } from '../lobby/lobbyStore';

export type FeedbackKind = 'bug' | 'complaint' | 'idea' | 'balance' | 'other';

export interface KindInfo {
  key: FeedbackKind;
  label: string;
  hint: string;
  placeholder: string;
}

export const KINDS: KindInfo[] = [
  { key: 'bug', label: 'Błąd', hint: 'Coś nie działa', placeholder: 'Co się stało, kiedy i jak to powtórzyć? Np. „Po śmierci w grze nie działa sklep”.' },
  { key: 'complaint', label: 'Skarga', hint: 'Gracz lub rozgrywka', placeholder: 'Na kogo lub na co się skarżysz i co się wydarzyło? Podaj nick gracza, jeśli dotyczy.' },
  { key: 'idea', label: 'Innowacja', hint: 'Nowy pomysł', placeholder: 'Co chciałbyś zobaczyć w grze: bohater, przedmiot, mapa, tryb? Opisz, jak miałoby działać.' },
  { key: 'balance', label: 'Balans', hint: 'Za mocne / za słabe', placeholder: 'Który bohater, przedmiot albo miniony jest za mocny lub za słaby i dlaczego?' },
  { key: 'other', label: 'Inne', hint: 'Pytanie, uwaga', placeholder: 'Napisz, w czym możemy pomóc.' },
];

export const PRIORITIES = [
  { value: 1, label: 'Niski' },
  { value: 2, label: 'Normalny' },
  { value: 3, label: 'Wysoki' },
];

export const MAX_TITLE = 100;
export const MAX_MESSAGE = 2000;
export const MIN_MESSAGE = 10;

export type FeedbackStatus = 'edit' | 'sending' | 'sent';

export const feedbackStore = createStore({
  kind: 'bug' as FeedbackKind,
  priority: 2,
  title: '',
  message: '',
  status: 'edit' as FeedbackStatus,
  number: 0,
  error: '',
});

export function problemOf(title: string, message: string): string {
  if (title.trim().length === 0) return 'Podaj krótki tytuł.';
  if (message.trim().length < MIN_MESSAGE) return `Opis: jeszcze ${MIN_MESSAGE - message.trim().length} znaków.`;
  return '';
}

export function sendFeedback(): void {
  const s = feedbackStore.get();
  if (s.status === 'sending' || problemOf(s.title, s.message)) return;
  feedbackStore.set({ status: 'sending', error: '' });
  act('feedback.send', { kind: s.kind, priority: s.priority, title: s.title.trim(), message: s.message.trim() });
}

export function newFeedback(): void {
  feedbackStore.set({ title: '', message: '', priority: 2, status: 'edit', number: 0, error: '' });
}

export function wireFeedback(): void {
  on('srv', ({ name, data }) => {
    if (name !== 'feedback.result') return;
    const d = data as { ok: boolean; id: number; error: string };
    if (d.ok) feedbackStore.set({ status: 'sent', number: d.id, error: '', title: '', message: '' });
    else feedbackStore.set({ status: 'edit', error: d.error || 'Nie udało się wysłać zgłoszenia.' });
  });
}
