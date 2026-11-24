import { useEffect } from 'react';
import { on } from '../../bridge/bridge';
import { createStore, useStore } from '../../state/store';
import { lobbyStore } from '../lobby/lobbyStore';
import { PORTRAIT } from '../../assets/heroes/portraits';
import mapImage from '../../assets/minimap/map.jpg';
import meta from '../../assets/minimap/map.json';
import './minimap.css';

export interface MinimapData {
  c: [number, number, number, number, number, string][];
  m: [number, number, number][];
  t: [number, number, number, number, number?][];
}

const empty: MinimapData = { c: [], m: [], t: [] };
export const minimapStore = createStore({
  data: empty,
  me: { x: 0, z: 0, ax: 0, az: 1, known: false },
  zoom: false,
});

const ZOOM = 3.2;

const toU = (x: number): number => (meta.maxX - x) / meta.size;
const toV = (z: number): number => (z - meta.minZ) / meta.size;

const TEAM = ['blue', 'red'] as const;
const STRUCTURE = ['turret', 'inhibitor', 'nexus'] as const;

const portraitStyle = (hero: string) => (PORTRAIT[hero] ? { backgroundImage: `url(${PORTRAIT[hero].face})` } : undefined);

export function Minimap() {
  const { phase } = useStore(lobbyStore);
  const { data, me, zoom } = useStore(minimapStore);

  useEffect(() => {
    const offData = on('srv', ({ name, data: payload }) => {
      if (name === 'minimap') minimapStore.set({ data: payload as MinimapData });
    });
    const offMe = on('mm.me', (p) => minimapStore.set({ me: { x: p.x, z: p.z, ax: p.ax, az: p.az, known: true } }));
    const offMode = on('mm.mode', () => minimapStore.set({ zoom: !minimapStore.get().zoom }));
    return () => {
      offData();
      offMe();
      offMode();
    };
  }, []);

  if (phase !== 'game') return null;

  const scale = zoom ? ZOOM : 1;
  const centerU = zoom && me.known ? toU(me.x) : 0.5;
  const centerV = zoom && me.known ? toV(me.z) : 0.5;
  const half = 0.5 / scale;
  const clamp = (value: number): number => Math.min(1 - half, Math.max(half, value));
  const shiftU = zoom ? 0.5 - clamp(centerU) * scale : 0;
  const shiftV = zoom ? 0.5 - clamp(centerV) * scale : 0;
  const pos = (x: number, z: number) => ({ left: `${toU(x) * 100}%`, top: `${toV(z) * 100}%` });
  const mine = data.c.find((entry) => entry[3] === 1);
  const heading = (Math.atan2(-me.ax, -me.az) * 180) / Math.PI;

  return (
    <div className={`minimap ${zoom ? 'is-zoom' : ''}`} aria-label="Minimapa" style={{ ['--inv' as string]: 1 / scale }}>
      <div
        className="minimap__layer"
        style={{ width: `${scale * 100}%`, height: `${scale * 100}%`, transform: `translate(${shiftU * 100 / scale}%, ${shiftV * 100 / scale}%)` }}
      >
        <img className="minimap__map" src={mapImage} alt="" draggable={false} />
        {data.t.map(([x, z, team, hp, type], i) => (
          <i key={`t${i}`} className={`minimap__tower minimap__tower--${TEAM[team] ?? 'blue'} minimap__tower--${STRUCTURE[type ?? 0] ?? 'turret'}`} style={{ ...pos(x, z), opacity: 0.45 + 0.55 * (hp / 100) }} />
        ))}
        {data.m.map(([x, z, team], i) => (
          <i key={`m${i}`} className={`minimap__minion minimap__minion--${TEAM[team] ?? 'blue'}`} style={pos(x, z)} />
        ))}
        {data.c.map(([x, z, team, self, , hero], i) =>
          self ? null : <i key={`c${i}`} className={`minimap__champ minimap__champ--${TEAM[team] ?? 'blue'}`} style={{ ...pos(x, z), ...portraitStyle(hero) }} />,
        )}
        {me.known && (
          <i className={`minimap__champ minimap__champ--me minimap__champ--${TEAM[mine?.[2] ?? 0] ?? 'blue'}`} style={{ ...pos(me.x, me.z), ...portraitStyle(mine?.[5] ?? '') }}>
            <svg className="minimap__pointer" viewBox="0 0 20 20" style={{ transform: `rotate(${heading}deg)` }} aria-hidden>
              <path d="M10 -1.5 15.5 4 4.5 4z" />
            </svg>
          </i>
        )}
      </div>
      <span className="minimap__key" title="Klawisz N: cała mapa / powiększenie">N</span>
    </div>
  );
}
