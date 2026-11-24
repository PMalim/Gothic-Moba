import type { ReactNode } from 'react';

const base = { viewBox: '0 0 24 24', fill: 'none', strokeWidth: 1.8, strokeLinecap: 'round', strokeLinejoin: 'round' } as const;

export type StatKey = 'ad' | 'ap' | 'crit' | 'armor' | 'resist' | 'cdr' | 'regen';

const AdIcon = () => (
  <svg {...base} stroke="#f2a03f" aria-hidden>
    <path d="M6 18 18.5 5.5" strokeWidth={2.6} />
    <path d="M7 12.5 11.5 17" />
    <path d="M4.5 19.5l2-2" strokeWidth={2.4} />
  </svg>
);

const ApIcon = () => (
  <svg {...base} stroke="#b98cf0" aria-hidden>
    <path d="M12 12.2a1.4 1.4 0 1 1 1.4-1.4 3.6 3.6 0 1 1-3.6 3.6 6 6 0 1 1 6 6" />
    <path d="M19 7.5l1.5-1.5M5 5l1.5 1.5" strokeWidth={1.4} />
  </svg>
);

const CritIcon = () => (
  <svg {...base} stroke="#f0604f" aria-hidden>
    <circle cx="12" cy="12" r="4.5" />
    <path d="M12 2.5v4M12 17.5v4M2.5 12h4M17.5 12h4" />
    <circle cx="12" cy="12" r="0.9" fill="#f0604f" />
  </svg>
);

const ArmorIcon = () => (
  <svg {...base} stroke="#e4b758" aria-hidden>
    <path d="M12 3 19 6v5.2c0 4.6-3 8-7 9.8-4-1.8-7-5.2-7-9.8V6z" fill="rgba(228,183,88,0.18)" />
    <path d="M12 7.5v9" strokeWidth={1.4} />
  </svg>
);

const ResistIcon = () => (
  <svg {...base} stroke="#5cc9f2" aria-hidden>
    <circle cx="12" cy="12" r="8" />
    <circle cx="12" cy="12" r="4" />
    <circle cx="12" cy="12" r="0.9" fill="#5cc9f2" />
  </svg>
);

const CdrIcon = () => (
  <svg {...base} stroke="#d8c08a" aria-hidden>
    <path d="M6.5 3h11M6.5 21h11" strokeWidth={2.2} />
    <path d="M7.5 3c0 4.5 4.5 6 4.5 9s-4.5 4.5-4.5 9M16.5 3c0 4.5-4.5 6-4.5 9s4.5 4.5 4.5 9" />
    <path d="M9.6 6.8h4.8L12 9.6z" fill="#f2cf7a" stroke="none" />
    <path d="M12 12.6v4.2" stroke="#f2cf7a" strokeWidth={1.2} />
    <path d="M8.8 19.6c1-1.6 2-2.3 3.2-2.3s2.2.7 3.2 2.3z" fill="#f2cf7a" stroke="none" />
  </svg>
);

const RegenIcon = () => (
  <svg {...base} stroke="#6fd36a" aria-hidden>
    <path d="M12 20s-7-4.4-7-9.6A3.9 3.9 0 0 1 12 8a3.9 3.9 0 0 1 7 2.4C19 15.6 12 20 12 20z" fill="rgba(111,211,106,0.18)" />
    <path d="M12 16.5v-6M9.6 12.8 12 10.4l2.4 2.4" strokeWidth={1.6} />
  </svg>
);

export const KillIcon = () => (
  <svg {...base} stroke="#f2a03f" aria-hidden>
    <path d="M5 19 17 7M14 5.5 18.5 5.5 18.5 10" />
    <path d="M19 19 7 7M10 5.5 5.5 5.5 5.5 10" />
  </svg>
);

export const DeathIcon = () => (
  <svg {...base} stroke="#d8d2c2" aria-hidden>
    <path d="M12 3.5c-4 0-6.5 2.6-6.5 6 0 2.2 1 3.7 2.5 4.6V17h8v-2.9c1.5-.9 2.5-2.4 2.5-4.6 0-3.4-2.5-6-6.5-6z" />
    <circle cx="9.5" cy="10.5" r="1.2" fill="#d8d2c2" />
    <circle cx="14.5" cy="10.5" r="1.2" fill="#d8d2c2" />
    <path d="M10.5 17v3M13.5 17v3" />
  </svg>
);

export const MinionIcon = () => (
  <svg {...base} stroke="#8fd16a" aria-hidden>
    <path d="M7 8 4.5 4.5M17 8l2.5-3.5" />
    <circle cx="12" cy="13" r="6.2" />
    <path d="M9.5 12.2h.01M14.5 12.2h.01" strokeWidth={2.6} />
    <path d="M9.5 16c1.5 1 3.5 1 5 0" />
  </svg>
);

export const AssistIcon = () => (
  <svg {...base} stroke="#e4c46a" aria-hidden>
    <path d="M12 3.5l2.3 5.2 5.7.5-4.3 3.8 1.3 5.5L12 15.6 7 18.5l1.3-5.5L4 9.2l5.7-.5z" fill="rgba(228,196,106,0.2)" />
  </svg>
);

export const STAT_ICON: Record<StatKey, () => ReactNode> = { ad: AdIcon, ap: ApIcon, crit: CritIcon, armor: ArmorIcon, resist: ResistIcon, cdr: CdrIcon, regen: RegenIcon };

export const STAT_LABEL: Record<StatKey, string> = {
  ad: 'Siła ataku (AD) — zwiększa obrażenia zdolności fizycznych bohaterów',
  ap: 'Moc magii (AP) — zwiększa obrażenia zdolności magicznych bohaterów',
  crit: 'Szansa na cios krytyczny (×1,5 obrażeń)',
  armor: 'Pancerz — zmniejsza obrażenia fizyczne (ciosy, miniony, boty)',
  resist: 'Odporność na magię — zmniejsza obrażenia magiczne (zdolności magów, wieże)',
  cdr: 'Skrócenie odnowień — o tyle procent krótsze odnowienia zdolności (najwyżej 60%)',
  regen: 'Odnawianie zdrowia — HP na 5 sekund (rośnie z poziomem)',
};

const EMBLEM: Record<string, ReactNode> = {
  rycerz: (
    <>
      <path d="M5 15c0-5.2 3-8.5 7-8.5s7 3.3 7 8.5v4.5H5z" />
      <path d="M5 14.5h14M12 6.5V14" />
      <path d="M9 17.2h6" strokeWidth={1.4} />
    </>
  ),
  lowca: (
    <>
      <path d="M7 3.5c7.5 3 7.5 14 0 17" />
      <path d="M7 3.5v17" strokeWidth={1.2} />
      <path d="M9 12h12M17.5 8.5 21 12l-3.5 3.5" />
    </>
  ),
  mag: (
    <>
      <path d="M12 2.8c1 4.2 5.2 5.4 5.2 10.4a5.2 5.2 0 0 1-10.4 0c0-2.2 1.1-3.5 2.2-4.6.1 2 1 3 2 3.1-.6-3.6-.3-6.3 1-8.9z" />
    </>
  ),
  zabojca: (
    <>
      <path d="M12 2.5 14.6 13H9.4z" />
      <path d="M6.5 13h11M12 13v6.5M10 21h4" />
    </>
  ),
  kaplan: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 6v12M8 10h8" />
    </>
  ),
  barbarzynca: (
    <>
      <path d="M12 4v17" />
      <path d="M11 6.5c-4.6 0-7 3-7 6.5 3.4-.8 5.7-2.7 7-6.5z" />
      <path d="M13 6.5c4.6 0 7 3 7 6.5-3.4-.8-5.7-2.7-7-6.5z" />
    </>
  ),
};

export function HeroEmblem({ hero }: { hero: string }) {
  return (
    <svg {...base} stroke="#fff3d0" className="hh__emblem" aria-hidden>
      {EMBLEM[hero] ?? <circle cx="12" cy="12" r="6" />}
    </svg>
  );
}
