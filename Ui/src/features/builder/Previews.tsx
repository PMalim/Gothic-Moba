import thumbs from '../../data/thumbs.json';
import { useStore } from '../../state/store';
import { builderUi } from './builderUi';

interface ThumbIndex {
  tile: number;
  cols: number;
  sheets: number;
  items: Record<string, [sheet: number, col: number, row: number, w: number, d: number, h: number]>;
}

const index = thumbs as unknown as ThumbIndex;
const ROWS = index.cols;

export interface Dimensions {
  w: number;
  d: number;
  h: number;
}

export const dimensionsOf = (name: string): Dimensions | null => {
  const t = index.items[name];
  return t ? { w: t[3], d: t[4], h: t[5] } : null;
};

export function formatDimensions(dim: Dimensions): string {
  const fmt = (cm: number): string => (cm >= 100 ? (cm / 100).toFixed(1).replace('.', ',') : String(cm));
  const big = Math.max(dim.w, dim.d, dim.h) >= 100;
  return `${fmt(dim.w)} × ${fmt(dim.d)} × ${fmt(dim.h)} ${big ? 'm' : 'cm'}`;
}

export function Thumb({ name, className = '' }: { name: string; className?: string }) {
  const t = index.items[name];
  if (!t) {
    return (
      <span className={`thumb thumb--none ${className}`} aria-hidden>
        {name.slice(0, 1)}
      </span>
    );
  }
  const [sheet, col, row] = t;
  const last = index.cols - 1;
  return (
    <span
      className={`thumb ${className}`}
      aria-hidden
      style={{
        backgroundImage: `url(BUILDER_T${String(sheet).padStart(2, '0')}.JPG)`,
        backgroundSize: `${index.cols * 100}% ${ROWS * 100}%`,
        backgroundPosition: `${last > 0 ? (col / last) * 100 : 0}% ${last > 0 ? (row / last) * 100 : 0}%`,
      }}
    />
  );
}

export function PreviewHole({ className = '' }: { className?: string }) {
  const { selected } = useStore(builderUi);
  const dim = selected ? dimensionsOf(selected) : null;
  return (
    <div className={`preview-hole ${className}`} aria-label="Podgląd obiektu">
      {selected ? <Thumb name={selected} className="thumb--big" /> : <span className="preview-hole__empty">nic nie wybrano</span>}
      {dim && <span className="preview-hole__dim">{formatDimensions(dim)}</span>}
    </div>
  );
}

export function Backdrop({ className = '' }: { className?: string }) {
  return <div className={`backdrop ${className}`} />;
}
