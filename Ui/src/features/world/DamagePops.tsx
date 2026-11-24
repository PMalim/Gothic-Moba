import { useEffect, useState } from 'react';
import { on } from '../../bridge/bridge';
import { uiStore } from '../../state/stores';

interface Pop {
  id: number;
  x: number;
  y: number;
  amount: number;
  kind: number;
  taken: boolean;
  dx: number;
}

const KIND = ['phys', 'magic', 'true'] as const;
let nextId = 1;
const LIFE_MS = 950;
const MAX_POPS = 24;

export function DamagePops() {
  const [pops, setPops] = useState<Pop[]>([]);

  useEffect(
    () =>
      on('dmg.pop', ({ x, y, amount, kind, taken }) => {
        const k = window.innerWidth / uiStore.get().resX;
        const id = nextId++;
        const spread = taken ? 26 : 34;
        const pop: Pop = { id, x: x * k, y: y * k, amount, kind, taken, dx: (Math.random() - 0.5) * 2 * spread };
        setPops((list) => [...list.slice(-(MAX_POPS - 1)), pop]);
        window.setTimeout(() => setPops((list) => list.filter((p) => p.id !== id)), LIFE_MS);
      }),
    [],
  );

  return (
    <div className="dmgpops" aria-hidden="true">
      {pops.map((p) => (
        <span key={p.id} className={`dmgpop dmgpop--${KIND[p.kind] ?? 'phys'} ${p.taken ? 'is-taken' : ''}`} style={{ left: p.x + p.dx, top: p.y }}>
          {p.taken ? '−' : ''}
          {p.amount}
        </span>
      ))}
    </div>
  );
}
