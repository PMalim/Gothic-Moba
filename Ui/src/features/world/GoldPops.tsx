import { useEffect, useState } from 'react';
import { on } from '../../bridge/bridge';
import { uiStore } from '../../state/stores';

interface Pop {
  id: number;
  x: number;
  y: number;
  amount: number;
}

let nextId = 1;
const LIFE_MS = 1300;

export function GoldPops() {
  const [pops, setPops] = useState<Pop[]>([]);

  useEffect(
    () =>
      on('gold.pop', ({ x, y, amount }) => {
        const k = window.innerWidth / uiStore.get().resX;
        const id = nextId++;
        setPops((list) => [...list.slice(-11), { id, x: x * k, y: y * k, amount }]);
        window.setTimeout(() => setPops((list) => list.filter((p) => p.id !== id)), LIFE_MS);
      }),
    [],
  );

  return (
    <div className="goldpops" aria-hidden="true">
      {pops.map((p) => (
        <div key={p.id} className="goldpop" style={{ left: p.x, top: p.y }}>
          <span className="goldpop__coin" />
          <b>+{p.amount}</b>
        </div>
      ))}
    </div>
  );
}
