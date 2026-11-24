import { useEffect, useRef } from 'react';

type IconName = 'play' | 'heroes' | 'shop' | 'ranking' | 'profile' | 'crown' | 'coin' | 'shard' | 'plus' | 'close' | 'bot' | 'skull' | 'tower' | 'swords' | 'flame';

const PATHS: Record<IconName, string> = {
  play: 'M5 19L19 5M6 14l4 4M19 19L5 5M18 14l-4 4M19 5h-4M19 5v4M5 5h4M5 5v4',
  swords: 'M5 19L19 5M6 14l4 4M19 19L5 5M18 14l-4 4M19 5h-4M19 5v4M5 5h4M5 5v4',
  heroes: 'M5 15a7 7 0 0 1 14 0v5H5zM5 15h14M12 8v7M9 20v-3M15 20v-3',
  shop: 'M6 8h12l1 12H5zM9 8a3 3 0 0 1 6 0M9 13h6',
  ranking: 'M4 18h16M4 18L3 7l5 4 4-6 4 6 5-4-1 11',
  profile: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8M4 21c0-4 4-6 8-6s8 2 8 6',
  crown: 'M4 18h16M4 18L3 8l5 4 4-7 4 7 5-4-1 10z',
  coin: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18M12 7v10M9 10c0-1.5 6-1.5 6 0s-6 2-6 3.5 6 1.5 6 0',
  shard: 'M12 2l6 8-6 12-6-12z M6 10h12 M12 2v20',
  plus: 'M12 5v14M5 12h14',
  close: 'M6 6l12 12M18 6L6 18',
  bot: 'M6 8h12v10H6zM12 4v4M9 12h.01M15 12h.01M9 15h6M3 12v3M21 12v3',
  skull: 'M12 3a8 8 0 0 0-5 14v3h10v-3a8 8 0 0 0-5-14M9 12h.01M15 12h.01M10 20v-2M14 20v-2',
  tower: 'M8 21V9L6 7V3h3v2h2V3h2v2h2V3h3v4l-2 2v12zM11 21v-4h2v4',
  flame: 'M12 3c1 4 5 5 5 10a5 5 0 0 1-10 0c0-2 1-3 2-4 0 2 1 3 2 3 0-3-1-6 1-9',
};

export function Icon({ name, size = '1.25rem', filled = false }: { name: IconName; size?: string; filled?: boolean }) {
  return (
    <svg className="lb-icon" viewBox="0 0 24 24" width={size} height={size} fill={filled ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d={PATHS[name]} />
    </svg>
  );
}

export function Sigil({ size = '2.6rem' }: { size?: string }) {
  return (
    <svg className="lb-sigil" viewBox="0 0 64 64" width={size} height={size} aria-hidden>
      <defs>
        <linearGradient id="sg-gold" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#f0d58a" />
          <stop offset="1" stopColor="#8a6a30" />
        </linearGradient>
        <radialGradient id="sg-fire" cx="0.5" cy="0.8" r="0.7">
          <stop offset="0" stopColor="#ffb25a" />
          <stop offset="1" stopColor="#a1252a" />
        </radialGradient>
      </defs>
      <polygon points="32,3 58,18 58,46 32,61 6,46 6,18" fill="#0b090e" stroke="url(#sg-gold)" strokeWidth="2.2" />
      <polygon points="32,9 52,21 52,43 32,55 12,43 12,21" fill="none" stroke="#5a4624" strokeWidth="1" />
      <path d="M32 14c2 8 10 10 10 20a10 10 0 0 1-20 0c0-4 2-6 4-8 0 4 2 6 4 6-1-6-2-12 2-18z" fill="url(#sg-fire)" opacity="0.95" />
      <path d="M32 18v30M27 40h10" stroke="url(#sg-gold)" strokeWidth="2.4" strokeLinecap="round" />
    </svg>
  );
}

type BtnVariant = 'default' | 'primary' | 'danger' | 'ghost';

interface BtnProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: BtnVariant;
  xl?: boolean;
  small?: boolean;
}

export function Btn({ variant = 'default', xl, small, className = '', children, ...rest }: BtnProps) {
  return (
    <button {...rest} className={`lb-btn lb-btn--${variant} ${xl ? 'lb-btn--xl' : ''} ${small ? 'lb-btn--sm' : ''} ${className}`}>
      <span>{children}</span>
    </button>
  );
}

export function Embers() {
  const ref = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const canvas = ref.current;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) return;
    let w = 0;
    let h = 0;
    let raf = 0;
    const resize = () => {
      w = canvas.width = window.innerWidth;
      h = canvas.height = window.innerHeight;
    };
    resize();
    window.addEventListener('resize', resize);

    interface P { x: number; y: number; r: number; vy: number; vx: number; ph: number; a: number; hue: number }
    const spawn = (initial: boolean): P => ({
      x: Math.random() * w,
      y: initial ? Math.random() * h : h + 12,
      r: 0.6 + Math.random() * 1.9,
      vy: 0.18 + Math.random() * 0.55,
      vx: (Math.random() - 0.5) * 0.25,
      ph: Math.random() * Math.PI * 2,
      a: 0.25 + Math.random() * 0.75,
      hue: Math.random() < 0.12 ? 205 : 12 + Math.random() * 28,
    });
    const parts = Array.from({ length: 64 }, () => spawn(true));
    let t = 0;

    const frame = () => {
      raf = requestAnimationFrame(frame);
      t += 0.016;
      ctx.clearRect(0, 0, w, h);
      ctx.globalCompositeOperation = 'lighter';
      for (let i = 0; i < parts.length; i++) {
        const p = parts[i];
        p.y -= p.vy;
        p.x += p.vx + Math.sin(t * 0.9 + p.ph) * 0.22;
        const fade = Math.max(0, p.y / h);
        const flick = 0.65 + 0.35 * Math.sin(t * 3 + p.ph * 3);
        const alpha = p.a * fade * flick;
        if (p.y < -10) {
          parts[i] = spawn(false);
          continue;
        }
        ctx.fillStyle = `hsla(${p.hue},100%,60%,${alpha * 0.12})`;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r * 4.5, 0, 6.2832);
        ctx.fill();
        ctx.fillStyle = `hsla(${p.hue},100%,${p.hue > 100 ? 78 : 68}%,${alpha})`;
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, 6.2832);
        ctx.fill();
      }
    };
    frame();
    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener('resize', resize);
    };
  }, []);
  return <canvas ref={ref} className="lb-embers" aria-hidden />;
}
