import { useEffect, useRef } from 'react';
import { on } from '../../bridge/bridge';
import type { WorldEntity } from '../../bridge/protocol';
import { createStore, useStore } from '../../state/store';
import { uiStore } from '../../state/stores';
import './world.css';

const entities = createStore({ list: [] as WorldEntity[] });

interface Node {
  root: HTMLElement;
  fill: HTMLElement;
  trail: HTMLElement;
  value: HTMLElement | null;
  entity: WorldEntity;
  visible: boolean;
  hp: number;
}
const nodes = new Map<number, Node>();

let focusId = -1;
let focusLocked = false;

function paintFocus(): void {
  for (const [id, node] of nodes) {
    node.root.classList.toggle('is-focus', id === focusId);
    node.root.classList.toggle('is-locked', id === focusId && focusLocked);
  }
}

export function wireWorld(): void {
  on('world.add', (e) => entities.set((s) => ({ list: [...s.list.filter((x) => x.id !== e.id), e] })));
  on('world.remove', ({ id }) => entities.set((s) => ({ list: s.list.filter((x) => x.id !== id) })));
  on('world.frame', ({ f }) => applyFrame(f));
  on('world.focus', ({ id, locked }) => {
    focusId = id;
    focusLocked = locked;
    paintFocus();
  });
}

const clamp = (v: number, lo: number, hi: number): number => Math.min(hi, Math.max(lo, v));

function applyFrame(f: number[]): void {
  const k = window.innerWidth / uiStore.get().resX;
  const seen = new Set<number>();
  for (let i = 0; i + 4 < f.length; i += 5) {
    const node = nodes.get(f[i]);
    if (!node) continue;
    seen.add(f[i]);
    const [x, y, hp, dist] = [f[i + 1] * k, f[i + 2] * k, f[i + 3], f[i + 4]];
    const tower = node.entity.kind === 'tower';
    const scale = tower ? clamp(1.4 - dist / 7000, 0.55, 1.25) : clamp(1.15 - dist / 3200, 0.6, 1);
    node.root.style.transform = `translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, 0) translate(-50%, -100%) scale(${scale.toFixed(3)})`;
    if (!node.visible) {
      node.visible = true;
      node.root.classList.remove('is-hidden');
    }
    if (hp !== node.hp) {
      node.hp = hp;
      const ratio = clamp(hp / Math.max(1, node.entity.maxHp), 0, 1);
      node.fill.style.width = `${ratio * 100}%`;
      node.trail.style.width = `${ratio * 100}%`;
      node.root.classList.toggle('is-low', ratio < 0.3);
      if (node.value) node.value.textContent = `${hp} / ${node.entity.maxHp}`;
    }
  }
  for (const [id, node] of nodes) {
    if (!seen.has(id) && node.visible) {
      node.visible = false;
      node.root.classList.add('is-hidden');
    }
  }
}

export function WorldLabels() {
  const { list } = useStore(entities);
  return (
    <div className="world" aria-hidden="true">
      {list.map((e) => (
        <Label key={e.id} entity={e} />
      ))}
    </div>
  );
}

function Label({ entity }: { entity: WorldEntity }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const root = ref.current!;
    nodes.set(entity.id, {
      root,
      fill: root.querySelector<HTMLElement>('.bar__fill')!,
      trail: root.querySelector<HTMLElement>('.bar__trail')!,
      value: root.querySelector<HTMLElement>('.bar__value'),
      entity,
      visible: false,
      hp: -1,
    });
    paintFocus();
    return () => {
      nodes.delete(entity.id);
    };
  }, [entity]);

  const team = entity.team === 0 ? 'blue' : entity.team === 1 ? 'red' : 'neutral';
  const bar = (
    <div className="bar">
      <div className="bar__trail" />
      <div className="bar__fill" />
      {entity.kind === 'tower' && <div className="bar__ticks" />}
      {entity.kind === 'tower' && <span className="bar__value" />}
    </div>
  );

  if (entity.kind === 'tower') {
    return (
      <div ref={ref} className={`label label--tower team--${team} is-hidden`}>
        <div className="focus-mark" />
        <div className="tower__crest" />
        <div className="tower__name">{entity.name}</div>
        <div className="tower__frame">{bar}</div>
      </div>
    );
  }
  return (
    <div ref={ref} className={`label label--${entity.kind} team--${team} is-hidden`}>
      <div className="focus-mark" />
      <div className="label__name">{entity.name}</div>
      {bar}
    </div>
  );
}
