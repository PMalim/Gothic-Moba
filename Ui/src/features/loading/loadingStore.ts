import { on } from '../../bridge/bridge';
import { createStore } from '../../state/store';

export const loadingStore = createStore({ visible: true, leaving: false, progress: 6, text: 'Wczytywanie interfejsu…' });

export const matchFadeStore = createStore({ visible: false, leaving: false });
export const MATCH_HOLD_MS = 2800;
export const MATCH_FADE_MS = 900;
let previousPhase: 'lobby' | 'game' = 'lobby';
let matchTimers: number[] = [];

function startMatchFade(): void {
  matchTimers.forEach((t) => window.clearTimeout(t));
  matchFadeStore.set({ visible: true, leaving: false });
  matchTimers = [
    window.setTimeout(() => matchFadeStore.set({ leaving: true }), MATCH_HOLD_MS),
    window.setTimeout(() => matchFadeStore.set({ visible: false, leaving: false }), MATCH_HOLD_MS + MATCH_FADE_MS),
  ];
}

const MIN_MS = 2600;
const MAX_MS = 20_000;
const FADE_MS = 650;
const startedAt = Date.now();
let finishing = false;

const step = (progress: number, text: string): void => {
  if (finishing) return;
  loadingStore.set((s) => (progress > s.progress ? { progress, text } : s));
};

function finish(): void {
  if (finishing) return;
  finishing = true;
  loadingStore.set({ progress: 100, text: 'Gotowe' });
  const wait = Math.max(0, MIN_MS - (Date.now() - startedAt));
  window.setTimeout(() => {
    loadingStore.set({ leaving: true });
    window.setTimeout(() => loadingStore.set({ visible: false }), FADE_MS);
  }, wait);
}

export function wireLoading(): void {
  on('init', () => step(30, 'Łączenie z serwerem…'));
  on('phase', () => step(58, 'Wczytywanie świata…'));
  on('phase', (p) => {
    if (p.phase === 'game' && previousPhase !== 'game') startMatchFade();
    previousPhase = p.phase;
  });
  on('srv', ({ name }) => {
    if (name === 'auth') {
      step(92, 'Wczytywanie danych gracza…');
      finish();
    }
  });
  const timer = window.setInterval(() => {
    if (finishing) return window.clearInterval(timer);
    loadingStore.set((s) => (s.progress < 84 ? { progress: s.progress + (84 - s.progress) * 0.03 } : s));
  }, 200);
  window.setTimeout(finish, MAX_MS);
}
