import navB64 from '../../data/navgrid.b64.txt?raw';

export interface NavGridData {
  cell: number;
  originX: number;
  originZ: number;
  width: number;
  height: number;
  mask: Uint8Array;
  floor: Int16Array;
}

export interface Stroke {
  pts: number[];
  r: number;
  cost: number;
}

export const COST = { normal: 0, path: 1, rough: 2, open: 254, blocked: 255 } as const;

let cached: NavGridData | null = null;

export function navGrid(): NavGridData {
  if (cached) return cached;
  const bytes = Uint8Array.from(atob(navB64.trim()), (c) => c.charCodeAt(0));
  const view = new DataView(bytes.buffer);
  const cell = view.getFloat32(4, true);
  const originX = view.getFloat32(8, true);
  const originZ = view.getFloat32(12, true);
  const width = view.getInt32(16, true);
  const height = view.getInt32(20, true);
  const count = width * height;
  const mask = bytes.slice(24 + count * 2, 24 + count * 3);
  const floorStart = 24 + count * 4;
  const floor = bytes.length >= floorStart + count * 2 ? new Int16Array(bytes.slice(floorStart, floorStart + count * 2).buffer) : new Int16Array(count).fill(-32768);
  cached = { cell, originX, originZ, width, height, mask, floor };
  return cached;
}

export function paintStroke(grid: NavGridData, cost: Uint8Array, stroke: Stroke): void {
  const p = stroke.pts;
  const r = stroke.r;
  const step = grid.cell * 0.5;
  const reach = Math.ceil(r / grid.cell) + 1;
  for (let n = 0; n + 1 < p.length; n += 2) {
    const x0 = p[n];
    const z0 = p[n + 1];
    const x1 = n + 3 < p.length ? p[n + 2] : x0;
    const z1 = n + 3 < p.length ? p[n + 3] : z0;
    const samples = Math.max(1, Math.floor(Math.hypot(x1 - x0, z1 - z0) / step) + 1);
    for (let s = 0; s <= samples; s++) {
      const t = s / samples;
      const x = x0 + (x1 - x0) * t;
      const z = z0 + (z1 - z0) * t;
      const ci = Math.floor((x - grid.originX) / grid.cell);
      const cj = Math.floor((z - grid.originZ) / grid.cell);
      for (let j = cj - reach; j <= cj + reach; j++) {
        for (let i = ci - reach; i <= ci + reach; i++) {
          if (i < 0 || j < 0 || i >= grid.width || j >= grid.height) continue;
          const dx = grid.originX + (i + 0.5) * grid.cell - x;
          const dz = grid.originZ + (j + 0.5) * grid.cell - z;
          if (dx * dx + dz * dz <= r * r) cost[j * grid.width + i] = stroke.cost;
        }
      }
    }
  }
}

export function costOf(grid: NavGridData, strokes: Stroke[]): Uint8Array {
  const cost = new Uint8Array(grid.width * grid.height);
  for (const stroke of strokes) paintStroke(grid, cost, stroke);
  return cost;
}
