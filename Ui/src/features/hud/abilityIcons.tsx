import type { ReactElement } from 'react';

const STEEL = (id: string) => (
  <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
    <stop offset="0" stopColor="#f2f5f7" />
    <stop offset="0.35" stopColor="#9aa6b0" />
    <stop offset="0.7" stopColor="#4c5660" />
    <stop offset="1" stopColor="#1f252b" />
  </linearGradient>
);

const WOOD = (id: string) => (
  <linearGradient id={id} x1="0" y1="0" x2="1" y2="0">
    <stop offset="0" stopColor="#2a160a" />
    <stop offset="0.45" stopColor="#7a4a24" />
    <stop offset="1" stopColor="#3a210f" />
  </linearGradient>
);

const BACK = (id: string, tint: string) => (
  <radialGradient id={id} cx="0.5" cy="0.42" r="0.75">
    <stop offset="0" stopColor={tint} />
    <stop offset="0.55" stopColor="#120f0b" />
    <stop offset="1" stopColor="#050403" />
  </radialGradient>
);

const GLOW = (id: string, blur = 1.6) => (
  <filter id={id} x="-50%" y="-50%" width="200%" height="200%">
    <feGaussianBlur stdDeviation={blur} result="b" />
    <feMerge>
      <feMergeNode in="b" />
      <feMergeNode in="b" />
      <feMergeNode in="SourceGraphic" />
    </feMerge>
  </filter>
);

const BLUE_FIRE = (id: string) => (
  <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
    <stop offset="0" stopColor="#1b3fd8" />
    <stop offset="0.55" stopColor="#4f9dff" />
    <stop offset="1" stopColor="#e4f3ff" />
  </linearGradient>
);

function Axe({ p, edgeGlow }: { p: string; edgeGlow?: boolean }) {
  return (
    <g>
      <rect x="29" y="9" width="5.5" height="52" rx="1.6" fill={`url(#${p}-wood)`} stroke="#120a04" strokeWidth="0.8" />
      {[40, 45, 50].map((y) => (
        <path key={y} d={`M28.6 ${y}l6.3 -2.2v2.2l-6.3 2.2z`} fill="#1b120a" opacity="0.85" />
      ))}
      <rect x="28.2" y="57" width="7" height="4.5" rx="1" fill="#5d6670" stroke="#15191d" strokeWidth="0.7" />
      <path d="M29 15.5 15.5 18.5 29 22z" fill={`url(#${p}-steel)`} stroke="#15191d" strokeWidth="0.8" />
      <path
        d="M34 11.5C41 11 47 8 51.5 3.5 57.5 11 59.5 21 55 31.5 51 28 45 25.5 40 25.5 38 25.5 36 26 34 26.5z"
        fill={`url(#${p}-steel)`}
        stroke="#15191d"
        strokeWidth="0.9"
      />
      <path d="M51.5 3.5C57.5 11 59.5 21 55 31.5" fill="none" stroke="#ffffff" strokeWidth="1.1" opacity="0.75" />
      {edgeGlow && (
        <path d="M51.5 3.5C57.5 11 59.5 21 55 31.5" fill="none" stroke="#6fb6ff" strokeWidth="2.6" filter={`url(#${p}-glow)`} opacity="0.95" />
      )}
      <rect x="27.6" y="10" width="8.4" height="17.5" rx="1.4" fill="#3b434b" stroke="#111518" strokeWidth="0.8" />
      <circle cx="31.8" cy="14" r="1.2" fill="#c9d1d8" />
      <circle cx="31.8" cy="23.5" r="1.2" fill="#c9d1d8" />
    </g>
  );
}

function MercenaryHead() {
  const p = 'ab-glowa';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#14305f')}
        {STEEL(`${p}-steel`)}
        {WOOD(`${p}-wood`)}
        {GLOW(`${p}-glow`, 1.8)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g transform="translate(33 32) rotate(32) scale(0.9) translate(-37.5 -32)">
        <Axe p={p} edgeGlow />
      </g>
      <g filter={`url(#${p}-glow)`} fill="#bfe0ff">
        <circle cx="55" cy="14" r="1" />
        <circle cx="50" cy="35" r="0.8" />
        <circle cx="59" cy="25" r="0.7" />
      </g>
      <g stroke="#0b1730" strokeWidth="0.7">
        <path d="M8 49l3.2 3.2L8 55.4 4.8 52.2z" fill="#6fb6ff" />
        <path d="M8 41l3.2 3.2L8 47.4 4.8 44.2z" fill="#4f8fe0" />
        <path d="M8 33l3.2 3.2L8 39.4 4.8 36.2z" fill="#2f63b8" />
      </g>
    </svg>
  );
}

function Momentum() {
  const p = 'ab-rozped';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#123a5c')}
        {BLUE_FIRE(`${p}-fire`)}
        {GLOW(`${p}-glow`, 1.6)}
        <linearGradient id={`${p}-leather`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#9a6a3c" />
          <stop offset="0.5" stopColor="#5b3a1d" />
          <stop offset="1" stopColor="#24150a" />
        </linearGradient>
        {STEEL(`${p}-steel`)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g stroke="#7fb8ff" strokeLinecap="round" opacity="0.55">
        <path d="M4 20h14" strokeWidth="1.4" />
        <path d="M2 28h12" strokeWidth="1.1" />
        <path d="M6 36h9" strokeWidth="0.9" />
      </g>
      <g filter={`url(#${p}-glow)`}>
        <path d="M27 50C19 49 12 45 6 46 12 41 19 41 25 43 18 38 14 33 9 33 16 29 23 33 28 39z" fill={`url(#${p}-fire)`} opacity="0.95" />
        <path d="M27 49C21 48 16 46 12 46 17 43 22 44 26 45z" fill="#eaf6ff" opacity="0.9" />
      </g>
      <g transform="translate(36 33) rotate(-8) scale(0.86) translate(-42 -29)">
        <path
          d="M29 8h15l-.5 25c1.5 4.5 6 7 11 8.5 3.5 1 4.5 3.5 3.5 7H27.5c-2 0-3-1.2-2.6-3.2L28 33z"
          fill={`url(#${p}-leather)`}
          stroke="#140b04"
          strokeWidth="1"
        />
        <path d="M28 8h17l-.6 5.5H28.6z" fill="#3a2410" stroke="#140b04" strokeWidth="0.8" />
        <path d="M28.3 19h15.4M28.2 26h15.2" stroke="#1d1209" strokeWidth="2.4" />
        <rect x="38" y="17.6" width="3.4" height="2.8" fill="none" stroke={`url(#${p}-steel)`} strokeWidth="1" />
        <rect x="38" y="24.6" width="3.4" height="2.8" fill="none" stroke={`url(#${p}-steel)`} strokeWidth="1" />
        <path d="M25.6 46.5h33" stroke="#0f0904" strokeWidth="3" strokeLinecap="round" />
        <path d="M52 41.5c3.4 1 5.5 2.6 6 4.5" fill="none" stroke={`url(#${p}-steel)`} strokeWidth="2.2" />
        <path d="M31 11v20" stroke="#d9a873" strokeWidth="1" opacity="0.45" />
      </g>
    </svg>
  );
}

function Curse() {
  const p = 'ab-klatwa';
  const flames = [12, 18, 25, 32, 39, 46, 52];
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1d1f5e')}
        {BLUE_FIRE(`${p}-fire`)}
        {GLOW(`${p}-glow`, 1.5)}
        <linearGradient id={`${p}-skin`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#e7b98e" />
          <stop offset="0.6" stopColor="#a8714a" />
          <stop offset="1" stopColor="#5a3820" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <ellipse cx="32" cy="50" rx="24" ry="8" fill="#1a3a9a" opacity="0.25" />
      <ellipse cx="32" cy="50" rx="24" ry="8" fill="none" stroke="#5aa8ff" strokeWidth="2" filter={`url(#${p}-glow)`} />
      <ellipse cx="32" cy="50" rx="17" ry="5.4" fill="none" stroke="#3f7fe0" strokeWidth="0.9" strokeDasharray="2.5 2" />
      <g stroke="#cfe6ff" strokeWidth="0.9" fill="none" filter={`url(#${p}-glow)`}>
        <path d="M14 49.5l2-2.5 1 3" />
        <path d="M47 49l1.5-2.6 1.5 2.6" />
        <path d="M31 56.5v-2.8l2 1.4" />
      </g>
      <g filter={`url(#${p}-glow)`}>
        {flames.map((x, i) => {
          const h = 7 + ((i * 5) % 6);
          const y = 50 + 8 * Math.sqrt(Math.max(0, 1 - ((x - 32) / 24) ** 2));
          return <path key={x} d={`M${x - 2.2} ${y}Q${x} ${y - h} ${x + 0.6} ${y - h - 3}Q${x + 2.6} ${y - h + 2} ${x + 2.2} ${y}z`} fill={`url(#${p}-fire)`} opacity="0.9" />;
        })}
      </g>
      <g>
        <path
          d="M24 39V22.5c0-1.6 2.6-1.6 2.6 0V31h.8V14.5c0-1.7 2.8-1.7 2.8 0V30h.8V12.5c0-1.8 2.9-1.8 2.9 0V30h.8V15c0-1.7 2.8-1.7 2.8 0v19l2.6-4.3c1-1.6 3.4-.6 2.6 1.2L40 42c-1.6 2.6-4 4-7 4h-2.6C26.6 46 24 43 24 39z"
          fill={`url(#${p}-skin)`}
          stroke="#2a170a"
          strokeWidth="0.9"
        />
        <path d="M25 41c2 3 5 4.5 8.5 4.5 3 0 5.5-1.6 7-4" fill="none" stroke="#7fbaff" strokeWidth="1.4" opacity="0.8" />
        <path d="M24.5 44.5h15l-1 6h-13z" fill="#3a2410" stroke="#140b04" strokeWidth="0.8" />
      </g>
    </svg>
  );
}

function Rampage() {
  const p = 'ab-pogrom';
  const spikes = Array.from({ length: 16 }, (_, i) => {
    const a = (i / 16) * Math.PI * 2;
    const r = i % 2 === 0 ? 30 : 16;
    return `${32 + Math.cos(a) * r},${30 + Math.sin(a) * r}`;
  }).join(' ');
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#3a1d5c')}
        {STEEL(`${p}-steel`)}
        {WOOD(`${p}-wood`)}
        {GLOW(`${p}-glow`, 2)}
        <radialGradient id={`${p}-burst`} cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#eaf2ff" />
          <stop offset="0.35" stopColor="#7d9bff" />
          <stop offset="0.75" stopColor="#5a2fc0" />
          <stop offset="1" stopColor="#5a2fc0" stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`${p}-gold`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#fff1b8" />
          <stop offset="0.5" stopColor="#d9a63c" />
          <stop offset="1" stopColor="#6b4512" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <polygon points={spikes} fill={`url(#${p}-burst)`} opacity="0.9" filter={`url(#${p}-glow)`} />
      <g transform="translate(32 33) scale(0.92) translate(-34 -34)">
        <Axe p={p} edgeGlow />
      </g>
      <g fill={`url(#${p}-gold)`} stroke="#3a2306" strokeWidth="0.8">
        <path d="M7 30l5-6 5 6h-3v4h-4v-4z" />
        <path d="M7 41l5-6 5 6h-3v4h-4v-4z" opacity="0.7" />
        <path d="M47 30l5-6 5 6h-3v4h-4v-4z" />
        <path d="M47 41l5-6 5 6h-3v4h-4v-4z" opacity="0.7" />
      </g>
    </svg>
  );
}

const AMBER = (id: string) => (
  <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
    <stop offset="0" stopColor="#b8320c" />
    <stop offset="0.5" stopColor="#ff9a2e" />
    <stop offset="1" stopColor="#fff0c0" />
  </linearGradient>
);

function Arrow({ p, x1, x2, y, glow }: { p: string; x1: number; x2: number; y: number; glow?: boolean }) {
  return (
    <g>
      <path d={`M${x1} ${y}H${x2 - 6}`} stroke="#2a1608" strokeWidth="3.2" strokeLinecap="round" />
      <path d={`M${x1} ${y}H${x2 - 6}`} stroke="#a8723c" strokeWidth="1.8" strokeLinecap="round" />
      <path d={`M${x1} ${y}l-4 -3.5h5l3 3.5-3 3.5h-5z`} fill="#d8cbb0" stroke="#3a2a14" strokeWidth="0.6" />
      <path d={`M${x2 - 7} ${y - 3.6}L${x2} ${y}l-7 3.6z`} fill={`url(#${p}-steel)`} stroke="#15191d" strokeWidth="0.7" />
      {glow && <path d={`M${x2 - 7} ${y - 3.6}L${x2} ${y}l-7 3.6z`} fill="#ffb84a" opacity="0.85" filter={`url(#${p}-glow)`} />}
    </g>
  );
}

function Hunter() {
  const p = 'ab-mysliwy';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a2a0c')}
        {STEEL(`${p}-steel`)}
        {WOOD(`${p}-wood`)}
        {GLOW(`${p}-glow`, 1.6)}
        <radialGradient id={`${p}-target`} cx="0.45" cy="0.4" r="0.6">
          <stop offset="0" stopColor="#d9b98a" />
          <stop offset="1" stopColor="#7a5530" />
        </radialGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <circle cx="28" cy="34" r="21" fill={`url(#${p}-target)`} stroke="#2a1608" strokeWidth="1.4" />
      <circle cx="28" cy="34" r="15" fill="none" stroke="#9c2a1a" strokeWidth="3" />
      <circle cx="28" cy="34" r="8.5" fill="none" stroke="#9c2a1a" strokeWidth="2.6" />
      <circle cx="28" cy="34" r="3.4" fill="#c23a20" />
      <g transform="rotate(-24 28 34)"><Arrow p={p} x1={36} x2={60} y={30} /></g>
      <g transform="rotate(14 28 34)"><Arrow p={p} x1={34} x2={58} y={38} /></g>
      <g filter={`url(#${p}-glow)`}>
        <path d="M28 34L52 10" stroke="#ffd27a" strokeWidth="2.4" strokeLinecap="round" />
        <path d="M52 10l-6 1.2 4.8 4.8z" fill="#fff0c0" />
        <path d="M48.5 13.5l4.5-.5-.5 4.5" fill="none" stroke="#ffb84a" strokeWidth="1.2" />
      </g>
      <circle cx="28" cy="34" r="5" fill="none" stroke="#ffe2a0" strokeWidth="1.2" filter={`url(#${p}-glow)`} />
      <g stroke="#2a1606" strokeWidth="0.6">
        <path d="M6 9l3 3-3 3-3-3z" fill="#ffbf4a" />
        <path d="M13 9l3 3-3 3-3-3z" fill="#ffbf4a" />
        <path d="M20 9l3 3-3 3-3-3z" fill="#ff7a2a" />
      </g>
    </svg>
  );
}

function Roll() {
  const p = 'ab-przewrot';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#4a2a10')}
        {AMBER(`${p}-amber`)}
        {GLOW(`${p}-glow`, 1.4)}
        <linearGradient id={`${p}-cloth`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#c0392b" />
          <stop offset="1" stopColor="#4a0e0a" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M12 44A22 22 0 1 1 46 52" fill="none" stroke={`url(#${p}-amber)`} strokeWidth="3" strokeLinecap="round" filter={`url(#${p}-glow)`} />
      <path d="M46 52l-1-7.5 7 3.5z" fill="#ffd27a" filter={`url(#${p}-glow)`} />
      <g transform="rotate(-30 32 32)">
        <path d="M22 32c0-7 5-12 11-12s10 5 10 10c0 7-5 11-11 11-3 0-5-1-6.5-2.6" fill={`url(#${p}-cloth)`} stroke="#1a0604" strokeWidth="1" />
        <circle cx="25" cy="25" r="4.6" fill="#c99a6a" stroke="#2a1608" strokeWidth="0.9" />
        <path d="M21.5 22.5c1.5-2.5 5-3 7-1" fill="none" stroke="#1a0e06" strokeWidth="2" />
        <path d="M30 36c3 2.5 7 2.5 9.5-.5" fill="none" stroke="#1d1209" strokeWidth="2.4" strokeLinecap="round" />
        <path d="M33 28l6 2" stroke="#d9b98a" strokeWidth="1.2" opacity="0.6" />
      </g>
      <g fill="#8a7a64" opacity="0.6">
        <circle cx="10" cy="52" r="3.5" />
        <circle cx="16" cy="55" r="2.5" />
        <circle cx="6" cy="56" r="2" />
      </g>
    </svg>
  );
}

function PiercingArrow() {
  const p = 'ab-przeszywajaca';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5c3a0a')}
        {STEEL(`${p}-steel`)}
        {WOOD(`${p}-wood`)}
        {GLOW(`${p}-glow`, 1.6)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g fill="none" stroke="#ffb84a" filter={`url(#${p}-glow)`}>
        <ellipse cx="40" cy="32" rx="4" ry="13" strokeWidth="2" opacity="0.9" />
        <ellipse cx="30" cy="32" rx="3" ry="10" strokeWidth="1.5" opacity="0.6" />
        <ellipse cx="21" cy="32" rx="2.2" ry="7" strokeWidth="1.1" opacity="0.4" />
      </g>
      <g transform="rotate(-8 32 32)">
        <Arrow p={p} x1={8} x2={58} y={32} glow />
      </g>
      <path d="M4 34h10" stroke="#ffd27a" strokeWidth="1.2" opacity="0.6" />
      <g fill="#ffe08a" filter={`url(#${p}-glow)`}>
        <path d="M50 10l1.3 3 3.2.3-2.4 2.1.7 3.1-2.8-1.6-2.8 1.6.7-3.1-2.4-2.1 3.2-.3z" />
        <path d="M14 46l1 2.2 2.4.2-1.8 1.6.5 2.3-2.1-1.2-2.1 1.2.5-2.3-1.8-1.6 2.4-.2z" />
      </g>
    </svg>
  );
}

function GreatHunter() {
  const p = 'ab-wielkilowca';
  const rays = Array.from({ length: 14 }, (_, i) => {
    const a = (i / 14) * Math.PI * 2;
    const r = i % 2 === 0 ? 30 : 17;
    return `${32 + Math.cos(a) * r},${32 + Math.sin(a) * r}`;
  }).join(' ');
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a1c08')}
        {STEEL(`${p}-steel`)}
        {WOOD(`${p}-wood`)}
        {GLOW(`${p}-glow`, 2)}
        <radialGradient id={`${p}-burst`} cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" stopColor="#fff2c8" />
          <stop offset="0.4" stopColor="#ffa53a" />
          <stop offset="0.8" stopColor="#a5280c" />
          <stop offset="1" stopColor="#a5280c" stopOpacity="0" />
        </radialGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <polygon points={rays} fill={`url(#${p}-burst)`} opacity="0.85" filter={`url(#${p}-glow)`} />
      <path d="M18 8C34 16 34 48 18 56" fill="none" stroke={`url(#${p}-wood)`} strokeWidth="4" strokeLinecap="round" />
      <path d="M18 8C34 16 34 48 18 56" fill="none" stroke="#2a1608" strokeWidth="0.8" />
      <rect x="25.6" y="28" width="4.4" height="8" rx="1" fill="#3a2410" stroke="#140b04" strokeWidth="0.6" />
      <path d="M18 8L10 32 18 56" fill="none" stroke="#efe3c4" strokeWidth="0.9" />
      <Arrow p={p} x1={11} x2={58} y={32} glow />
      <g filter={`url(#${p}-glow)`}>
        <path d="M40 15c3.5-4 9.5-4 13 0-3.5 4-9.5 4-13 0z" fill="#2a0e04" stroke="#ffd27a" strokeWidth="1" />
        <circle cx="46.5" cy="15" r="2" fill="#ffb84a" />
      </g>
    </svg>
  );
}

const BONE = (id: string) => (
  <linearGradient id={id} x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stopColor="#f3ecd6" />
    <stop offset="0.6" stopColor="#b9ae8c" />
    <stop offset="1" stopColor="#6e6548" />
  </linearGradient>
);

function SummonSkeleton() {
  const p = 'ab-szkielet';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1f4a2a')}
        {BONE(`${p}-bone`)}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <ellipse cx="32" cy="54" rx="24" ry="6.5" fill="none" stroke="#7dff9a" strokeWidth="1.4" filter={`url(#${p}-glow)`} opacity="0.9" />
      <ellipse cx="32" cy="54" rx="16" ry="4" fill="none" stroke="#3fbf62" strokeWidth="0.9" opacity="0.8" />
      <g fill="#7dff9a" opacity="0.75" filter={`url(#${p}-glow)`}>
        <path d="M12 54c-2-6 3-8 1-14 4 3 6 8 4 14z" />
        <path d="M52 54c2-6-3-8-1-14-4 3-6 8-4 14z" />
      </g>
      <path d="M32 9C20 9 13 17 13 27c0 6 3 9.5 7 11.5V46h24v-7.5c4-2 7-5.500 7-11.500C51 17 44 9 32 9z" fill={`url(#${p}-bone)`} stroke="#1a150b" strokeWidth="1.2" />
      <path d="M23 46v-5m6 5v-5m6 5v-5m6 5v-5" stroke="#3a3220" strokeWidth="1.1" />
      <ellipse cx="25.500" cy="28" rx="5" ry="5.800" fill="#0a0f08" />
      <ellipse cx="38.500" cy="28" rx="5" ry="5.800" fill="#0a0f08" />
      <g filter={`url(#${p}-glow)`}>
        <circle cx="25.500" cy="28.500" r="2.200" fill="#9bff5a" />
        <circle cx="38.500" cy="28.500" r="2.200" fill="#9bff5a" />
      </g>
      <path d="M32 33l-2.500 5h5z" fill="#0a0f08" />
    </svg>
  );
}

function ThunderStrike() {
  const p = 'ab-piorun';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#2a2a6a')}
        {GLOW(`${p}-glow`, 2.200)}
        <linearGradient id={`${p}-cloud`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#6a6f93" />
          <stop offset="1" stopColor="#2a2d45" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M10 22c-4 0-6-5-3-8 2-3 6-3 8-1 1-5 7-8 12-6 4-3 11-2 13 3 5-2 11 2 9 7 3 1 4 5 1 7-1 1-2 1-3 1z" fill={`url(#${p}-cloud)`} stroke="#12142a" strokeWidth="1" transform="translate(2 -2)" />
      <g filter={`url(#${p}-glow)`}>
        <path d="M36 18L24 36h8l-6 15 18-22h-9l7-11z" fill="#fff6a8" stroke="#ffd84a" strokeWidth="1.200" strokeLinejoin="round" />
      </g>
      <g fill="#0b0d1a" stroke="#8fa8ff" strokeWidth="0.800" opacity="0.95">
        <circle cx="20" cy="46" r="3.500" />
        <path d="M13 60c0-7 3-10 7-10s7 3 7 10z" />
      </g>
      <path d="M14 36l3 2m-5 6l3 1m33-9l-3 2m5 6l-3 1" stroke="#cfe0ff" strokeWidth="1.200" strokeLinecap="round" />
    </svg>
  );
}

function Escape() {
  const p = 'ab-ucieczka';
  const body = 'M14 50c0-9 3-14 8-14s8 5 8 14z';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#3a2a6a')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g fill="none" stroke="#b18cff" strokeWidth="1.400" filter={`url(#${p}-glow)`} opacity="0.9">
        <ellipse cx="22" cy="50" rx="14" ry="4" />
        <ellipse cx="22" cy="50" rx="8" ry="2.200" />
      </g>
      <g opacity="0.45" fill="#c9b6ff" stroke="#e5dcff" strokeWidth="0.800">
        <circle cx="22" cy="30" r="5" />
        <path d={`${body}`} transform="translate(0 -4)" />
      </g>
      <g fill="#150f2a" stroke="#d8c8ff" strokeWidth="1" transform="translate(22 0)">
        <circle cx="16" cy="26" r="5" />
        <path d="M8 46l6-14 8 4 8-8 3 3-9 12-8-3-4 9z" />
        <path d="M2 30h9m-12 6h8m-5 6h7" stroke="#b18cff" strokeWidth="1.600" strokeLinecap="round" fill="none" />
      </g>
    </svg>
  );
}

function SummonGolem() {
  const p = 'ab-golem';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a3a1a')}
        {GLOW(`${p}-glow`, 2)}
        <linearGradient id={`${p}-stone`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#a8a399" />
          <stop offset="0.5" stopColor="#6c685f" />
          <stop offset="1" stopColor="#2d2b27" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M2 60V46l10-6 8 6v14zM62 60V46l-10-6-8 6v14z" fill={`url(#${p}-stone)`} stroke="#14120f" strokeWidth="1" />
      <path d="M20 10l24 0 6 8v16l-6 8H20l-6-8V18z" fill={`url(#${p}-stone)`} stroke="#14120f" strokeWidth="1.200" />
      <path d="M20 10l8 10 8-10m-14 36l6-8 4 6 6-6" fill="none" stroke="#14120f" strokeWidth="1" opacity="0.7" />
      <path d="M24 26l-4-2m24 2l4-2" stroke="#14120f" strokeWidth="1.600" strokeLinecap="round" />
      <g filter={`url(#${p}-glow)`}>
        <path d="M24 28h6v4h-6zM34 28h6v4h-6z" fill="#ff8a2a" />
        <path d="M25 29h4v2h-4zM35 29h4v2h-4z" fill="#fff0b0" />
      </g>
      <path d="M44 14l-4 8 4 4-3 8" fill="none" stroke="#2a2823" strokeWidth="1" />
      <path d="M28 40h8" stroke="#14120f" strokeWidth="1.600" />
    </svg>
  );
}

const BEER = (id: string) => (
  <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
    <stop offset="0" stopColor="#9a4a08" />
    <stop offset="0.6" stopColor="#f0a020" />
    <stop offset="1" stopColor="#fff0b8" />
  </linearGradient>
);

function BeerMug() {
  const p = 'ab-piwosz';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a3408')}
        {BEER(`${p}-beer`)}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 1.6)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M44 22h6c5 0 7 4 7 8s-2 8-7 8h-6" fill="none" stroke={`url(#${p}-steel)`} strokeWidth="4" strokeLinecap="round" />
      <path d="M13 17h31v37c0 2-1.500 3-3 3H16c-1.500 0-3-1-3-3z" fill={`url(#${p}-beer)`} stroke="#2a1608" strokeWidth="1.400" />
      <path d="M13 17h31v6H13z" fill="#fff6dc" stroke="#2a1608" strokeWidth="1" />
      <g fill="#fff6dc" stroke="#2a1608" strokeWidth="0.800">
        <circle cx="19" cy="15" r="5" />
        <circle cx="28" cy="12.500" r="6" />
        <circle cx="38" cy="15" r="5" />
      </g>
      <g fill="#fff0b8" opacity="0.85">
        <circle cx="21" cy="40" r="1.300" />
        <circle cx="30" cy="34" r="1.700" />
        <circle cx="35" cy="46" r="1.200" />
        <circle cx="24" cy="50" r="1" />
      </g>
      <g filter={`url(#${p}-glow)`}>
        <path d="M50 10v10m-5-5h10" stroke="#7dff8a" strokeWidth="3" strokeLinecap="round" />
      </g>
    </svg>
  );
}

function BarrelThrow() {
  const p = 'ab-beczka';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a1a40')}
        {WOOD(`${p}-wood`)}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M6 52C14 14 40 8 52 26" fill="none" stroke="#ffb0dc" strokeWidth="1.600" strokeDasharray="3 3" opacity="0.8" />
      <g transform="rotate(-24 36 22)">
        <path d="M24 8c-5 8-5 22 0 30h24c5-8 5-22 0-30z" fill={`url(#${p}-wood)`} stroke="#140b04" strokeWidth="1.400" />
        <path d="M23 14h26M22 23h28M23 32h26" stroke={`url(#${p}-steel)`} strokeWidth="3" />
        <path d="M30 9v28m6-28v28m6-28v28" stroke="#140b04" strokeWidth="0.600" opacity="0.6" />
      </g>
      <g fill="#ff8ccb" filter={`url(#${p}-glow)`}>
        <circle cx="50" cy="48" r="5" />
        <circle cx="42" cy="53" r="3" />
        <circle cx="57" cy="40" r="2.500" />
        <circle cx="46" cy="40" r="1.800" />
        <circle cx="54" cy="55" r="2" />
      </g>
      <ellipse cx="48" cy="54" rx="13" ry="3.500" fill="#ff5aa8" opacity="0.6" />
    </svg>
  );
}

function ThickSkull() {
  const p = 'ab-mocnaglowa';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#2a4a6a')}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 1.800)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M12 38C12 20 21 10 32 10s20 10 20 28v12H12z" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1.400" />
      <path d="M32 10v40" stroke="#10151a" strokeWidth="1.200" opacity="0.7" />
      <path d="M14 30h36" stroke="#10151a" strokeWidth="1" opacity="0.6" />
      <path d="M17 38h30v5H17z" fill="#0a0d10" />
      <g fill="#8fd0ff"><circle cx="25" cy="40.500" r="1.600" /><circle cx="39" cy="40.500" r="1.600" /></g>
      <path d="M13 24C6 22 4 14 6 8c5 4 10 8 12 14zM51 24c7-2 9-10 7-16-5 4-10 8-12 14z" fill="#d8cbb0" stroke="#3a2a14" strokeWidth="1" />
      <g filter={`url(#${p}-glow)`} fill="#ffe27a">
        <path d="M9 52l2-4 2 4-4-2zM52 54l2-4 2 4-4-2zM32 58l1.500-3 1.500 3-3-1.500z" />
        <circle cx="8" cy="46" r="1.600" />
        <circle cx="57" cy="47" r="1.600" />
      </g>
    </svg>
  );
}

function ShadowBeast() {
  const p = 'ab-cieniostwor';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#3a2a5a')}
        {GLOW(`${p}-glow`, 2)}
        <linearGradient id={`${p}-hide`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#4a4558" />
          <stop offset="1" stopColor="#14121a" />
        </linearGradient>
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <path d="M32 20c-3-8-1-14 3-17 0 6 1 11 3 17z" fill="#d8cfbc" stroke="#2a2418" strokeWidth="1" />
      <path d="M10 22l10 4 12-6 12 6 10-4-2 18-8 8 3 8H17l3-8-8-8z" fill={`url(#${p}-hide)`} stroke="#0a0810" strokeWidth="1.400" />
      <path d="M20 26l8 8m16-8l-8 8" stroke="#0a0810" strokeWidth="1.200" />
      <g filter={`url(#${p}-glow)`}>
        <path d="M19 31l9 3-2 4-9-2zM45 31l-9 3 2 4 9-2z" fill="#ffb84a" />
        <path d="M23 33l3 1m15-1l-3 1" stroke="#fff0b0" strokeWidth="1.400" />
      </g>
      <path d="M24 49l2 6 2-6m8 0l2 6 2-6" fill="#f2ecd8" stroke="#2a2418" strokeWidth="0.800" />
    </svg>
  );
}

const BLOOD = (id: string) => (
  <linearGradient id={id} x1="0" y1="1" x2="0" y2="0">
    <stop offset="0" stopColor="#5a0610" />
    <stop offset="0.55" stopColor="#d01a2a" />
    <stop offset="1" stopColor="#ffb0a0" />
  </linearGradient>
);

function SpinBlade() {
  const p = 'ab-obrot';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#4a1a1a')}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="none" stroke="#ff7a5a" strokeWidth="2.600" strokeLinecap="round">
        <path d="M32 8a24 24 0 0 1 22 14" />
        <path d="M54 36a24 24 0 0 1-30 19" />
        <path d="M13 44A24 24 0 0 1 12 20" />
      </g>
      <path d="M54 22l-1 7-6-4zM24 55l7-1-3-7zM12 20l2-7 5 5z" fill="#ffb09a" />
      <g transform="rotate(-38 32 32)">
        <path d="M30 8h4l2 30h-8z" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1" />
        <rect x="23" y="38" width="18" height="4" rx="1.500" fill="#6a1018" stroke="#10151a" strokeWidth="0.800" />
        <rect x="30" y="42" width="4" height="12" rx="1.200" fill="#2a160a" stroke="#10151a" strokeWidth="0.800" />
        <circle cx="32" cy="56" r="2.500" fill="#8a96a0" stroke="#10151a" strokeWidth="0.800" />
      </g>
    </svg>
  );
}

function Bloodlust() {
  const p = 'ab-zadzakrwi';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a0a14')}
        {BLOOD(`${p}-blood`)}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`}>
        <path d="M32 6C24 20 14 28 14 40a18 18 0 0 0 36 0C50 28 40 20 32 6z" fill={`url(#${p}-blood)`} stroke="#2a0408" strokeWidth="1.400" />
      </g>
      <path d="M24 38c0 6 4 10 9 10" fill="none" stroke="#ffd0c4" strokeWidth="2" strokeLinecap="round" opacity="0.8" />
      <path d="M8 56L40 24l4 4L12 60z" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1" />
      <path d="M52 18c4 6 4 14-2 20M50 38l-1-6 6 2" fill="none" stroke="#7dff8a" strokeWidth="2.200" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function PullIn() {
  const p = 'ab-przyciagniecie';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#4a2a1a')}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 1.800)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="none" stroke="#ff9a5a" strokeWidth="2.600" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6 12l12 10-4 2M58 12L46 22l4 2" />
        <path d="M6 52l12-10-4-2M58 52L46 42l4-2" />
      </g>
      <circle cx="32" cy="22" r="6" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1" />
      <path d="M22 50c0-12 4-18 10-18s10 6 10 18z" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1" />
      <path d="M22 36l-10 8m30-8l10 8" stroke="#d8c4a8" strokeWidth="3.600" strokeLinecap="round" />
    </svg>
  );
}

function ExecuteStrike() {
  const p = 'ab-egzekucja';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#5a0a0a')}
        {STEEL(`${p}-steel`)}
        {BLOOD(`${p}-blood`)}
        {GLOW(`${p}-glow`, 2.200)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill={`url(#${p}-blood)`} opacity="0.95">
        <path d="M32 40l-9 4 6-7-8-5 10 1 1-9 3 9 9-5-5 9 8 6-10-1-1 9z" />
      </g>
      <g filter={`url(#${p}-glow)`}>
        <path d="M30 2h4l2 28h-8z" fill="#ff6a5a" opacity="0.9" />
      </g>
      <path d="M30 2h4l2 28h-8z" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="0.800" opacity="0.8" />
      <rect x="23" y="30" width="18" height="4" rx="1.500" fill="#6a1018" stroke="#10151a" strokeWidth="0.800" />
      <ellipse cx="32" cy="56" rx="16" ry="4" fill="#2a0408" stroke="#d01a2a" strokeWidth="1.200" />
    </svg>
  );
}

function BeliarTongue() {
  const p = 'ab-jezyk';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1a0a26')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`}>
        <ellipse cx="32" cy="42" rx="26" ry="14" fill="#050008" stroke="#a050e0" strokeWidth="2" />
        <path d="M24 40c2-18 6-26 8-30 2 4 6 12 8 30-4 4-12 4-16 0z" fill="#2a0a3a" stroke="#c070ff" strokeWidth="1.600" strokeLinejoin="round" />
        <path d="M32 14v22" stroke="#c070ff" strokeWidth="1.400" strokeLinecap="round" />
      </g>
    </svg>
  );
}

function Servant() {
  const p = 'ab-sluga';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1a0a26')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="#0a0410" stroke="#c070ff" strokeWidth="1.600" strokeLinejoin="round">
        <circle cx="32" cy="18" r="8" />
        <path d="M16 56c0-14 6-24 16-24s16 10 16 24z" />
      </g>
      <path d="M26 17h4M34 17h4" stroke="#e8b0ff" strokeWidth="2.200" strokeLinecap="round" />
      <path d="M10 14l6 6M54 14l-6 6" stroke="#a050e0" strokeWidth="2" strokeLinecap="round" opacity="0.8" />
    </svg>
  );
}

function Flank() {
  const p = 'ab-okrazenie';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1a0a26')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <circle cx="32" cy="32" r="9" fill="#0a0410" stroke="#8a8aa0" strokeWidth="1.800" />
      <g filter={`url(#${p}-glow)`} fill="none" stroke="#c070ff" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
        <path d="M10 44c6-22 28-30 44-12" />
        <path d="M46 24l8 8-10 2" />
      </g>
    </svg>
  );
}

function BeliarMight() {
  const p = 'ab-silabeliara';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1a0a26')}
        {STEEL(`${p}-steel`)}
        {GLOW(`${p}-glow`, 2.400)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="none" stroke="#c070ff" strokeWidth="2.200" strokeLinecap="round">
        <ellipse cx="32" cy="52" rx="26" ry="7" />
        <ellipse cx="32" cy="52" rx="16" ry="4" />
      </g>
      <path d="M32 4l7 12v28H25V16z" fill="#14081e" stroke="#c070ff" strokeWidth="1.600" strokeLinejoin="round" />
      <path d="M32 8v34" stroke="#7a3aa8" strokeWidth="1.400" />
      <rect x="20" y="44" width="24" height="5" rx="1.500" fill={`url(#${p}-steel)`} stroke="#10151a" strokeWidth="1" />
    </svg>
  );
}

function BigFireball() {
  const p = 'ab-wielkakula';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#2a0e04')}
        {GLOW(`${p}-glow`, 2.400)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`}>
        <path d="M6 14c10 2 16 8 20 14" fill="none" stroke="#ff9a2a" strokeWidth="3" strokeLinecap="round" opacity="0.7" />
        <path d="M4 26c10 0 16 4 20 8" fill="none" stroke="#ffb84a" strokeWidth="2.200" strokeLinecap="round" opacity="0.6" />
        <circle cx="38" cy="38" r="17" fill="#e8481a" stroke="#ffc060" strokeWidth="1.800" />
        <circle cx="38" cy="38" r="10" fill="#ff9a2a" />
        <circle cx="35" cy="35" r="4.500" fill="#fff0b0" />
      </g>
    </svg>
  );
}

function TribeProgress() {
  const p = 'ab-postep';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#2a1a04')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="#d8cfa8" stroke="#3a2a10" strokeWidth="1.200">
        <circle cx="20" cy="46" r="8" />
        <circle cx="38" cy="46" r="8" />
        <circle cx="29" cy="32" r="8" />
      </g>
      <g fill="#2a1a04">
        <circle cx="17" cy="45" r="1.800" /><circle cx="23" cy="45" r="1.800" />
        <circle cx="35" cy="45" r="1.800" /><circle cx="41" cy="45" r="1.800" />
        <circle cx="26" cy="31" r="1.800" /><circle cx="32" cy="31" r="1.800" />
      </g>
      <path d="M52 40V14M44 22l8-9 8 9" fill="none" stroke="#7ad06a" strokeWidth="3.200" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function TribalTotem() {
  const p = 'ab-totem';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#2a1a08')}
        {GLOW(`${p}-glow`, 2)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`}>
        <path d="M32 2c4 5 5 9 0 13-5-4-4-8 0-13z" fill="#ff9a2a" stroke="#ffd070" strokeWidth="1" />
      </g>
      <path d="M24 16h16l3 10-3 4v22H24V30l-3-4z" fill="#6a4a26" stroke="#2a1808" strokeWidth="1.600" strokeLinejoin="round" />
      <circle cx="28" cy="25" r="2.600" fill="#ffb84a" /><circle cx="36" cy="25" r="2.600" fill="#ffb84a" />
      <path d="M27 34h10M27 40h10M27 46h10" stroke="#2a1808" strokeWidth="2" strokeLinecap="round" />
      <path d="M14 58h36" stroke="#2a1808" strokeWidth="4" strokeLinecap="round" />
    </svg>
  );
}

function TribeGuard() {
  const p = 'ab-plemie';
  return (
    <svg viewBox="0 0 64 64" className="ab-icon" aria-hidden>
      <defs>
        {BACK(`${p}-bg`, '#1a2a10')}
        {GLOW(`${p}-glow`, 2.400)}
      </defs>
      <rect width="64" height="64" fill={`url(#${p}-bg)`} />
      <g filter={`url(#${p}-glow)`} fill="none" stroke="#9adf6a" strokeWidth="2.400">
        <circle cx="32" cy="32" r="26" />
        <circle cx="32" cy="32" r="21" opacity="0.5" />
      </g>
      <path d="M20 30c0-9 5-15 12-15s12 6 12 15c0 5-2 8-5 10v6H25v-6c-3-2-5-5-5-10z" fill="#d8cfa8" stroke="#3a2a10" strokeWidth="1.400" strokeLinejoin="round" />
      <circle cx="26" cy="30" r="3.400" fill="#1a1208" /><circle cx="38" cy="30" r="3.400" fill="#1a1208" />
      <path d="M30 37l2-3 2 3zM27 46v-5M32 46v-5M37 46v-5" stroke="#1a1208" strokeWidth="1.600" strokeLinecap="round" fill="#1a1208" />
      <path d="M17 38l-5 6M47 38l5 6" stroke="#e8e0c0" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

export const ABILITY_ICON: Record<string, () => ReactElement> = {
  glowa: MercenaryHead,
  rozped: Momentum,
  klatwa: Curse,
  pogrom: Rampage,
  mysliwy: Hunter,
  przewrot: Roll,
  przeszywajaca: PiercingArrow,
  wielkilowca: GreatHunter,
  szkielet: SummonSkeleton,
  piorun: ThunderStrike,
  ucieczka: Escape,
  golem: SummonGolem,
  piwosz: BeerMug,
  beczka: BarrelThrow,
  mocnaglowa: ThickSkull,
  cieniostwor: ShadowBeast,
  obrot: SpinBlade,
  zadzakrwi: Bloodlust,
  przyciagniecie: PullIn,
  egzekucja: ExecuteStrike,
  jezyk: BeliarTongue,
  sluga: Servant,
  okrazenie: Flank,
  silabeliara: BeliarMight,
  wielkakula: BigFireball,
  postep: TribeProgress,
  totem: TribalTotem,
  plemie: TribeGuard,
};
