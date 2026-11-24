import { useEffect, useLayoutEffect, useRef, useState } from 'react';

interface Props<T> {
  items: T[];
  rowHeight: number;
  selectedIndex: number;
  scrollSignal: number;
  renderRow: (item: T, index: number, selected: boolean) => React.ReactNode;
  onSelect: (item: T, index: number) => void;
  onActivate: (item: T, index: number) => void;
  onKeyDown?: (e: React.KeyboardEvent<HTMLDivElement>) => void;
  empty?: React.ReactNode;
}

export function VirtualList<T>({ items, rowHeight, selectedIndex, scrollSignal, renderRow, onSelect, onActivate, onKeyDown, empty }: Props<T>) {
  const ref = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [height, setHeight] = useState(300);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setHeight(el.clientHeight);
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const el = ref.current;
    if (!el || selectedIndex < 0) return;
    const top = selectedIndex * rowHeight;
    if (top < el.scrollTop) el.scrollTop = top;
    else if (top + rowHeight > el.scrollTop + el.clientHeight) el.scrollTop = top + rowHeight - el.clientHeight;
  }, [scrollSignal]);

  const overscan = 6;
  const first = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
  const last = Math.min(items.length, Math.ceil((scrollTop + height) / rowHeight) + overscan);

  return (
    <div
      ref={ref}
      className="vlist"
      tabIndex={0}
      onScroll={(e) => setScrollTop(e.currentTarget.scrollTop)}
      onKeyDown={onKeyDown}
    >
      {items.length === 0 ? (
        <div className="vlist__empty">{empty}</div>
      ) : (
        <div style={{ height: items.length * rowHeight, position: 'relative' }}>
          {items.slice(first, last).map((item, offset) => {
            const index = first + offset;
            return (
              <div
                key={index}
                className="vlist__row"
                style={{ top: index * rowHeight, height: rowHeight }}
                onClick={() => onSelect(item, index)}
                onDoubleClick={() => onActivate(item, index)}
              >
                {renderRow(item, index, index === selectedIndex)}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
