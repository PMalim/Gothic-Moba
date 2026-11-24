import { useEffect, useMemo, useState } from 'react';
import { send } from '../../bridge/bridge';
import { meStore } from '../lobby/lobbyStore';
import { useStore } from '../../state/store';
import { GROUPS, buyItem, buyState, closeShop, filledSlots, itemByKey, moveItem, nextPurchase, priceFor, sellItem, shopStore, treeOf, type ItemStats, type ShopItem } from './shopStore';
import { buildsInto, type RecipeNode } from './recipe';
import { ItemIcon } from './ItemIcon';
import './shop.css';

const STAT_LABELS: [keyof ItemStats, string, string][] = [
  ['ad', 'Siła ataku', ''],
  ['ap', 'Moc magii', ''],
  ['health', 'Zdrowie', ''],
  ['mana', 'Mana', ''],
  ['armor', 'Pancerz', ''],
  ['resist', 'Odporność', ''],
  ['crit', 'Szansa na kryt', '%'],
  ['critDmg', 'Obrażenia krytyka', '%'],
  ['as', 'Szybkość ataku (wkrótce)', '%'],
  ['cdr', 'Skrócenie odnowień', '%'],
  ['ms', 'Szybkość ruchu (wkrótce)', '%'],
  ['ls', 'Kradzież życia', '%'],
  ['ov', 'Omnivamp', '%'],
  ['leth', 'Przebicie pancerza', ''],
  ['armPen', 'Przebicie pancerza', '%'],
  ['magPen', 'Przebicie odporności', ''],
  ['magPenPct', 'Przebicie odporności', '%'],
  ['ten', 'Odporność na ogłuszenia', '%'],
  ['healPower', 'Moc leczenia', '%'],
  ['manaRegen', 'Regeneracja many', '%'],
  ['hpRegen', 'Odn. zdrowia', ' / 5 s'],
];

const statLines = (s: ItemStats): { label: string; value: string }[] =>
  STAT_LABELS.filter(([k]) => (s[k] ?? 0) > 0).map(([k, label, unit]) => ({ label, value: `+${s[k]}${unit}` }));

function useText(item: ShopItem): string {
  const u = item.use!;
  if (u.kind === 'active') return `Aktywny (klawisz slotu 4–9) · odnowienie ${u.cooldown ?? 0} s`;
  if (u.kind === 'heal') return `Leczy ${u.amount} HP przez ${u.seconds} s · do ${u.max} sztuk w slocie`;
  if (u.kind === 'mana') return `Przywraca ${u.amount} many · do ${u.max} sztuk w slocie`;
  return `Wzmocnienie na ${u.seconds} s: ${statLines(u.boost).map((l) => `${l.value} ${l.label}`).join(', ')}`;
}

function Tile({ item, owned, gold, selected }: { item: ShopItem; owned: string[]; gold: number; selected: boolean }) {
  const have = owned.includes(item.key);
  const price = priceFor(owned, item);
  const afford = gold >= price;
  return (
    <button
      type="button"
      className={`rec-tile item--${item.group} ${selected ? 'is-selected' : ''} ${have ? 'is-own' : afford ? 'is-afford' : 'is-locked'}`}
      onClick={() => shopStore.set({ selected: item.key })}
      onDoubleClick={() => buyItem(item.key)}
      title={`${item.name} — ${have ? 'masz' : `${price} złota`} (podwójne kliknięcie: kup)`}
    >
      <span className="rec-tile__icon" aria-hidden>
        <ItemIcon itemKey={item.key} fallback={item.glyph} />
        {have && <b className="rec-tile__check">✓</b>}
      </span>
      <span className="rec-tile__name">{item.name}</span>
      <span className="rec-tile__price">{have ? 'masz' : price}</span>
    </button>
  );
}

function Recommended({ gold }: { gold: number }) {
  const s = useStore(shopStore);
  const advice = s.advice;
  if (!advice) return <div className="shop__empty">Brak podpowiedzi — wybierz bohatera.</div>;
  const next = nextPurchase(s.owned, gold, advice);
  const done = !next.buy && !next.saving;
  return (
    <section className="shop__rec" aria-label="Polecane przedmioty">
      <div className={`rec-next ${next.buy ? 'is-ready' : ''}`}>
        {next.buy ? (
          <>
            <span className="rec-next__icon" aria-hidden>
              <ItemIcon itemKey={next.buy.key} fallback={next.buy.glyph} />
            </span>
            <div className="rec-next__text">
              <small>Kup teraz</small>
              <b>{next.buy.name}</b>
              {next.saving && <span>składnik: {next.saving.name} (do niego brakuje {next.missing} złota)</span>}
            </div>
            <button className="btn btn--primary" onClick={() => buyItem(next.buy!.key)}>
              Kup za {priceFor(s.owned, next.buy)}
            </button>
          </>
        ) : next.saving ? (
          <>
            <span className="rec-next__icon" aria-hidden>
              <ItemIcon itemKey={next.saving.key} fallback={next.saving.glyph} />
            </span>
            <div className="rec-next__text">
              <small>Oszczędzasz na</small>
              <b>{next.saving.name}</b>
              <span>brakuje {next.missing} złota</span>
            </div>
          </>
        ) : (
          <div className="rec-next__text">
            <small>{done ? 'Ścieżka skończona' : ''}</small>
            <b>Masz wszystkie polecane przedmioty — zajrzyj do „Sytuacyjne”.</b>
          </div>
        )}
        <p className="rec-next__who">
          Ścieżka dla: <b>{advice.hero}</b> · {advice.role}
        </p>
      </div>

      {advice.sections.map((section) => (
        <div key={section.id} className={`rec-section rec-section--${section.id}`}>
          <header>
            <h3>{section.title}</h3>
            <p>{section.hint}</p>
          </header>
          <div className="rec-section__items">
            {section.items.map((key) => {
              const item = itemByKey(key);
              return item ? <Tile key={key} item={item} owned={s.owned} gold={gold} selected={s.selected === key} /> : null;
            })}
          </div>
        </div>
      ))}
    </section>
  );
}

const NODE_TITLE: Record<RecipeNode['state'], string> = {
  root: '',
  own: 'masz — zalicza się do ceny',
  covered: 'w składniku, który masz',
  missing: 'do dokupienia',
};

function TreeNode({ node }: { node: RecipeNode }) {
  const { item, state } = node;
  const stats = statLines(item.stats).map((l) => `${l.value} ${l.label}`).join(', ');
  return (
    <li>
      <button
        type="button"
        className={`rtree__node item--${item.group} is-${state}`}
        onClick={() => shopStore.set({ selected: item.key })}
        onDoubleClick={() => buyItem(item.key)}
        title={`${item.name} — ${item.cost} złota${NODE_TITLE[state] ? ` (${NODE_TITLE[state]})` : ''}${stats ? `\n${stats}` : ''}`}
      >
        <span className="rtree__icon" aria-hidden>
          <ItemIcon itemKey={item.key} fallback={item.glyph} />
          {state === 'own' && <b className="rtree__check">✓</b>}
        </span>
        <span className="rtree__price">{item.cost}</span>
      </button>
      {node.children.length > 0 && (
        <ul>
          {node.children.map((child, i) => (
            <TreeNode key={`${child.item.key}-${i}`} node={child} />
          ))}
        </ul>
      )}
    </li>
  );
}

function RecipeTree({ item }: { item: ShopItem }) {
  const s = useStore(shopStore);
  const tree = treeOf(item, s.owned);
  const into = buildsInto(item.key, s.catalog);
  if (!tree.children.length && !into.length) return null;
  return (
    <div className="recipe">
      {tree.children.length > 0 && (
        <>
          <h4>Receptura</h4>
          <ul className="rtree">
            <TreeNode node={tree} />
          </ul>
        </>
      )}
      {into.length > 0 && (
        <>
          <h4>Buduje się w</h4>
          <div className="recipe__into">
            {into.map((target) => (
              <button
                key={target.key}
                type="button"
                className={`rtree__node item--${target.group} ${s.owned.includes(target.key) ? 'is-own' : ''}`}
                onClick={() => shopStore.set({ selected: target.key })}
                onDoubleClick={() => buyItem(target.key)}
                title={`${target.name} — ${priceFor(s.owned, target)} złota dla ciebie`}
              >
                <span className="rtree__icon" aria-hidden>
                  <ItemIcon itemKey={target.key} fallback={target.glyph} />
                </span>
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function BuyButton({ item }: { item: ShopItem }) {
  const s = useStore(shopStore);
  const me = useStore(meStore);
  const state = buyState(item, s.owned, s.charges, me.gold, s.slots);
  return (
    <div className="shop__buy">
      <button className="btn btn--primary" disabled={!state.can} onClick={() => buyItem(item.key)} title={state.reason || 'Kup (lub podwójne kliknięcie na kafelku)'}>
        Kup za {state.price}
      </button>
      <small>{state.can ? `po zakupie zostanie ${me.gold - state.price} złota` : state.reason}</small>
    </div>
  );
}

function Slots() {
  const s = useStore(shopStore);
  const [picked, setPicked] = useState<number | null>(null);
  const [drag, setDrag] = useState<{ from: number; x: number; y: number; over: number } | null>(null);

  const slotAt = (x: number, y: number): number => {
    const el = document.elementFromPoint(x, y)?.closest('[data-slot]') as HTMLElement | null;
    return el ? Number(el.dataset.slot) : -1;
  };

  const begin = (i: number, e: React.MouseEvent) => {
    if (e.button !== 0 || (e.target as HTMLElement).closest('.slot__sell')) return;
    const key = s.owned[i];
    if (!key) {
      if (picked !== null) {
        moveItem(picked, i);
        setPicked(null);
      }
      return;
    }
    const x0 = e.clientX;
    const y0 = e.clientY;
    const held = picked;
    let moved = false;
    const onMove = (ev: MouseEvent) => {
      if (!moved && Math.hypot(ev.clientX - x0, ev.clientY - y0) < 6) return;
      moved = true;
      setDrag({ from: i, x: ev.clientX, y: ev.clientY, over: slotAt(ev.clientX, ev.clientY) });
    };
    const onUp = (ev: MouseEvent) => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
      setDrag(null);
      if (moved) {
        const to = slotAt(ev.clientX, ev.clientY);
        if (to >= 0 && to !== i) moveItem(i, to);
        setPicked(null);
        return;
      }
      shopStore.set({ selected: key });
      if (held !== null && held !== i) {
        moveItem(held, i);
        setPicked(null);
      } else setPicked(held === i ? null : i);
    };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  const dragged = drag ? itemByKey(s.owned[drag.from]) : undefined;
  return (
    <>
      <div className="shop__slots">
        {Array.from({ length: s.slots }, (_, i) => {
          const key = s.owned[i];
          const item = key ? itemByKey(key) : undefined;
          const stack = item?.use && item.use.kind !== 'active' ? s.charges[i] ?? 1 : 1;
          const sell = item ? Math.round(item.cost * s.sellRatio * stack) : 0;
          const state = `${picked === i || drag?.from === i ? 'is-picked' : ''} ${drag && drag.over === i && drag.from !== i ? 'is-over' : ''} ${picked !== null && !item ? 'is-target' : ''}`;
          return (
            <div
              key={i}
              data-slot={i}
              className={`slot ${item ? `slot--${item.group}` : 'is-empty'} ${state}`}
              onMouseDown={(e) => begin(i, e)}
              title={item ? `${item.name} — klawisz ${i + 4}, sprzedaj za ${sell}. Przeciągnij na inny slot, żeby przenieść.` : `Slot ${i + 1} (klawisz ${i + 4}) — pusty`}
            >
              <kbd className="slot__key">{i + 4}</kbd>
              {item && (
                <>
                  <span className="slot__glyph">
                    <ItemIcon itemKey={item.key} fallback={item.glyph} />
                  </span>
                  {item.use && item.use.kind !== 'active' && <b className="slot__charges">{s.charges[i] ?? 1}</b>}
                  <button className="slot__sell" onClick={() => sellItem(i)} aria-label={`Sprzedaj ${item.name}`}>
                    {sell}
                  </button>
                </>
              )}
            </div>
          );
        })}
      </div>
      {drag && dragged && (
        <div className="slot-ghost" style={{ left: drag.x, top: drag.y }} aria-hidden>
          <ItemIcon itemKey={dragged.key} fallback={dragged.glyph} />
        </div>
      )}
    </>
  );
}

export function Shop() {
  const s = useStore(shopStore);
  const me = useStore(meStore);

  useEffect(() => {
    send('shop.cursor', s.open);
  }, [s.open]);

  const shown = useMemo(() => {
    const words = s.query.toLowerCase().split(/\s+/).filter(Boolean);
    return s.catalog
      .map((item, index) => ({ item, price: s.prices[index] ?? item.cost }))
      .filter(({ item }) => (s.group === 'all' || item.group === s.group) && words.every((w) => item.name.toLowerCase().includes(w)));
  }, [s.catalog, s.prices, s.group, s.query]);

  if (!s.open) return null;
  const recommended = s.view === 'recommended';
  const firstAdvice = s.advice?.sections[0]?.items[0];
  const selected = s.catalog.find((i) => i.key === s.selected) ?? (recommended && firstAdvice ? itemByKey(firstAdvice) : shown[0]?.item);

  return (
    <div className="shop" role="dialog" aria-label="Sklep przedmiotów">
      <header className="shop__head">
        <h2>Sklep</h2>
        <div className="shop__gold" title="Złoto">
          <span className="shop__coin" />
          <b>{me.gold}</b>
        </div>
        <nav className="shop__views" role="tablist" aria-label="Widok sklepu">
          <button role="tab" aria-selected={recommended} className={`shop__view ${recommended ? 'is-active' : ''}`} onClick={() => shopStore.set({ view: 'recommended' })}>
            Polecane
          </button>
          <button role="tab" aria-selected={!recommended} className={`shop__view ${!recommended ? 'is-active' : ''}`} onClick={() => shopStore.set({ view: 'all' })}>
            Wszystkie przedmioty
          </button>
        </nav>
        <input
          className="shop__search"
          type="search"
          placeholder="Szukaj przedmiotu…"
          value={s.query}
          spellCheck={false}
          onChange={(e) => shopStore.set({ query: e.target.value, view: e.target.value ? 'all' : s.view })}
        />
        <button className="shop__close" onClick={closeShop} aria-label="Zamknij sklep">
          <kbd>P</kbd> / <kbd>Esc</kbd>
        </button>
      </header>

      {!recommended && (
        <nav className="shop__tabs" aria-label="Kategorie">
          {GROUPS.map((g) => (
            <button key={g.id} className={`shop__tab ${s.group === g.id ? 'is-active' : ''}`} onClick={() => shopStore.set({ group: g.id as typeof s.group })}>
              {g.label}
            </button>
          ))}
        </nav>
      )}

      <div className="shop__body">
        {recommended ? (
          <Recommended gold={me.gold} />
        ) : (
        <section className="shop__items" aria-label="Przedmioty">
          {shown.map(({ item, price }) => {
            const state = buyState(item, s.owned, s.charges, me.gold, s.slots);
            const afford = state.can;
            const free = true;
            return (
              <article
                key={item.key}
                className={`item item--${item.group} ${selected?.key === item.key ? 'is-selected' : ''} ${afford && free ? '' : 'is-locked'}`}
                onClick={() => shopStore.set({ selected: item.key })}
                onDoubleClick={() => buyItem(item.key)}
              >
                <div className="item__icon" aria-hidden>
                  <ItemIcon itemKey={item.key} fallback={item.glyph} />
                </div>
                <div className="item__main">
                  <h3>{item.name}</h3>
                  <p className="item__stats">{item.use && item.use.kind !== 'active' ? useText(item) : statLines(item.stats).map((l) => `${l.value} ${l.label}`).join(' · ')}</p>
                  {item.builds.length > 0 && (
                    <p className="item__builds">
                      {item.builds.map((b, i) => {
                        const own = s.owned.includes(b);
                        return (
                          <span key={i} className={own ? 'is-own' : ''}>
                            {itemByKey(b)?.name ?? b}
                          </span>
                        );
                      })}
                    </p>
                  )}
                </div>
                <div className="item__buy">
                  <span className="item__price">
                    {price < item.cost && <s>{item.cost}</s>}
                    <b>{price}</b>
                  </span>
                  <button className="btn btn--primary btn--small" disabled={!afford} onClick={(e) => { e.stopPropagation(); buyItem(item.key); }} title={state.reason || 'Kup (lub podwójne kliknięcie)'}>
                    Kup
                  </button>
                </div>
              </article>
            );
          })}
          {shown.length === 0 && <div className="shop__empty">Nic nie pasuje.</div>}
        </section>
        )}

        <aside className="shop__side">
          <h3>Ekwipunek <span>{filledSlots(s.owned)}/{s.slots}</span></h3>
          <Slots />
          <p className="shop__hint">
            Przeciągnij przedmiot na inny slot (albo kliknij go, a potem slot docelowy) — klawisz użycia to numer slotu. Kliknij cenę pod przedmiotem, żeby go sprzedać ({Math.round(s.sellRatio * 100)}% wartości). Poza aktywacjami (przedmioty z klawiszem 4–9) efekty specjalne z opisów nie są jeszcze aktywne — działają statystyki.
          </p>

          {selected && (
            <div className="shop__detail">
              <h3>{selected.name}</h3>
              <p className="shop__meta">{GROUPS.find((g) => g.id === selected.group)?.label}</p>
              <RecipeTree item={selected} />
              <BuyButton item={selected} />
              {selected.use && <p className="shop__use">{useText(selected)}</p>}
              {selected.desc && <p className="shop__desc">{selected.desc}</p>}
              <ul>
                {statLines(selected.use && selected.use.kind !== 'active' ? selected.use.boost : selected.stats).map((l) => (
                  <li key={l.label}>
                    <b>{l.value}</b> {l.label}
                  </li>
                ))}
              </ul>
              {selected.builds.length > 0 && <p>Posiadane składniki (także z głębi drzewka) zaliczają się do ceny i znikają przy zakupie.</p>}
            </div>
          )}

          <dl className="shop__totals">
            <div title={me.aaMagic ? 'Zwykły cios maga: część z AD, część z AP; magiczny (broni go odporność)' : 'Zwykły cios: 100% siły ataku, fizyczny'}><dt>Cios {me.aaMagic ? '(magiczny)' : '(fizyczny)'}</dt><dd>{me.aa}</dd></div>
            <div><dt>Siła ataku</dt><dd>{me.ad}</dd></div>
            <div><dt>Moc magii</dt><dd>{me.ap}</dd></div>
            <div><dt>Pancerz</dt><dd>{me.armor}</dd></div>
            <div><dt>Odporność</dt><dd>{me.resist}</dd></div>
            <div><dt>Kryt</dt><dd>{me.crit}% (×{(me.critMult / 100).toFixed(2)})</dd></div>
            <div><dt>Odnowienia</dt><dd>−{me.cdr}%</dd></div>
          </dl>
        </aside>
      </div>
    </div>
  );
}
