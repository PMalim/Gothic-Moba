import type { Inbound, Outbound } from './protocol';

declare global {
  interface Window {
    squirrel?: { call: (fn: string, ...args: unknown[]) => unknown };
    uiReceive?: (name: string, json: string) => void;
  }
}

type Listener<K extends keyof Inbound> = (payload: Inbound[K]) => void;

const listeners = new Map<string, Set<Listener<never>>>();
let outboundSink: ((name: string, args: unknown[]) => void) | null = null;

export const inGame = (): boolean => typeof window.squirrel?.call === 'function';

export function on<K extends keyof Inbound>(name: K, listener: Listener<K>): () => void {
  let set = listeners.get(name);
  if (!set) listeners.set(name, (set = new Set()));
  set.add(listener as Listener<never>);
  return () => set!.delete(listener as Listener<never>);
}

export function send<K extends keyof Outbound>(name: K, ...args: Outbound[K]): void {
  const wire = args.map((value) => (typeof value === 'boolean' ? (value ? 1 : 0) : value));
  try {
    if (inGame()) window.squirrel!.call('cefEvent', name, ...wire);
    else outboundSink?.(name, args);
  } catch (error) {
    console.error('[bridge] send failed', name, error);
  }
}

export function deliver<K extends keyof Inbound>(name: K, payload: Inbound[K]): void {
  listeners.get(name)?.forEach((listener) => {
    try {
      (listener as Listener<K>)(payload);
    } catch (error) {
      console.error('[bridge] listener failed', name, error);
    }
  });
}

export function setOutboundSink(sink: (name: string, args: unknown[]) => void): void {
  outboundSink = sink;
}

export function connect(): void {
  window.uiReceive = (name: string, json: string) => {
    let payload: unknown = {};
    try {
      payload = json ? JSON.parse(json) : {};
    } catch (error) {
      console.error('[bridge] bad json for', name, error);
      return;
    }
    deliver(name as keyof Inbound, payload as never);
  };

  const NON_TEXT = new Set(['range', 'checkbox', 'radio', 'button', 'submit', 'reset', 'color', 'file']);
  const isText = (el: EventTarget | null): boolean =>
    (el instanceof HTMLInputElement && !NON_TEXT.has(el.type)) ||
    el instanceof HTMLTextAreaElement ||
    (el instanceof HTMLElement && el.isContentEditable);
  window.addEventListener('focusin', (e) => isText(e.target) && send('ui.typing', true));
  window.addEventListener('focusout', (e) => isText(e.target) && send('ui.typing', false));

  send('ready');
}
