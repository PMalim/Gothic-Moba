import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { flyStore } from '../../state/stores';
import { abilityStore, act, buffStore, heroOf, lobbyStore, manaStore, meStore, vitalStore } from '../lobby/lobbyStore';
import { ABILITY_ICON } from './abilityIcons';
import { abilityValues } from './abilityText';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { HeroEmblem, STAT_ICON, STAT_LABEL, type StatKey } from './icons';
import { itemByKey, itemCdStore, shopStore } from '../shop/shopStore';
import { ItemIcon } from '../shop/ItemIcon';
import './herohud.css';

const KEYS = ['Z', 'X', 'C', 'V'];
const GLYPH: Record<string, string> = {
  strike: '➶', nova: '✹', rain: '☄', dash: '➤', heal: '✚', shield: '⛨',
  empower: '⚒', haste: '»', curse: '◎', rampage: '♛',
  passive: '◈', roll: '↻', pierceshot: '➹', huntermode: '☉',
  summon: '☠', lightning: 'ϟ', decoy: '☗',
  brewer: '🍺', barrel: '◍', ironhead: '⛨', shadowform: '☠', spin: '↻', bloodlust: '♥', pull: '⇦', execute: '⚔', ignite: '🔥', tribeprogress: '✦', totem: '♜', tribeguard: '☠',
};
const ROLE_COLOR: Record<string, string> = {
  Tank: '#6d8cc4',
  Strzelec: '#6fae6a',
  Mag: '#c4574b',
  Zabójca: '#8a63b8',
  Wsparcie: '#d1b45a',
  Wojownik: '#c47f3d',
};

const fmt = (n: number): string => Math.max(0, Math.round(n)).toLocaleString('pl-PL');

export function HeroHud() {
  const { phase, heroes } = useStore(lobbyStore);
  const ability = useStore(abilityStore);
  const vital = useStore(vitalStore);
  const mana = useStore(manaStore);
  const me = useStore(meStore);
  const shop = useStore(shopStore);
  const itemCooldown = useStore(itemCdStore);
  const buffs = useStore(buffStore);
  const fly = useStore(flyStore);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (phase !== 'game') return;
    const timer = window.setInterval(() => setNow(Date.now()), 100);
    return () => window.clearInterval(timer);
  }, [phase]);

  const hero = heroOf(ability.hero, heroes);
  if (phase !== 'game' || !hero || fly.on) return null;

  const hpRatio = vital.maxHp > 0 ? Math.min(1, Math.max(0, vital.hp / vital.maxHp)) : 0;
  const currentMana = Math.min(mana.max, mana.current + (mana.regen * (now - mana.at)) / 1000);
  const manaRatio = mana.max > 0 ? Math.min(1, Math.max(0, currentMana / mana.max)) : 0;
  const xpSpan = Math.max(1, me.xpNext - me.xpFloor);
  const xpRatio = me.xpNext === me.xpFloor ? 1 : Math.min(1, Math.max(0, (me.xp - me.xpFloor) / xpSpan));
  const color = ROLE_COLOR[hero.role] ?? '#c9a85c';
  const portrait = PORTRAIT[hero.key];
  const since = now - buffs.at;
  const left = (ms: number) => Math.max(0, ms - since);
  const hasStacks = hero.abilities.some((a) => a.kind === 'empower');
  const hunter = hero.abilities.find((a) => a.kind === 'passive');
  const tribe = hero.abilities.some((a) => a.kind === 'tribeprogress');
  const comboNeed = hunter ? Math.max(2, Math.round(hunter.radius ?? 3)) : 3;
  const timers = [
    { key: 'primed', label: 'Wzmocniony cios', ms: left(buffs.primed), glyph: '⚒' },
    { key: 'haste', label: 'Rozpęd', ms: left(buffs.haste), glyph: '»' },
    { key: 'rampage', label: 'Pogrom', ms: left(buffs.rampage), glyph: '♛' },
    { key: 'pierce', label: 'Przeszywająca strzała napięta', ms: left(buffs.pierce), glyph: '➹' },
    { key: 'hunt', label: 'Wielki Łowca', ms: left(buffs.hunt), glyph: '☉' },
    { key: 'hidden', label: 'Niewidzialny', ms: left(buffs.hidden), glyph: '◌' },
    { key: 'iron', label: 'Mocna głowa: odporny na ogłuszenia i trucizny', ms: left(buffs.iron), glyph: '⛨' },
    { key: 'shadow', label: 'Cieniostwór', ms: left(buffs.shadow), glyph: '☠' },
    { key: 'blood', label: 'Żądza krwi: następny cios wysysa życie', ms: left(buffs.blood), glyph: '♥' },
    { key: 'fire', label: `Wielka Kula: płonę, ładunków podpalenia ${buffs.fireCharges}`, ms: left(buffs.fire), glyph: '🔥' },
    { key: 'guard', label: 'Plemię jest najważniejsze: niewrażliwy na obrażenia', ms: left(buffs.guard), glyph: '☠' },
    { key: 'bleed', label: `Krwawienie ×${buffs.bleedStacks}: obrażenia co sekundę`, ms: left(buffs.bleed), glyph: '❀' },
  ].filter((t) => t.ms > 0);

  const stats: { key: StatKey; value: string }[] = [
    { key: 'ad', value: String(me.ad) },
    { key: 'ap', value: String(me.ap) },
    { key: 'crit', value: `${me.crit}%` },
    { key: 'armor', value: String(me.armor) },
    { key: 'resist', value: String(me.resist) },
    { key: 'cdr', value: `${me.cdr}%` },
    { key: 'regen', value: `${me.regen}` },
  ];

  return (
    <div className="hh" aria-label="Panel bohatera">
      <div className="hh__stats" aria-label="Statystyki">
        {stats.map((s) => {
          const Icon = STAT_ICON[s.key];
          return (
            <div key={s.key} className="hh__stat" title={s.key === 'regen' ? `${STAT_LABEL.regen} (teraz +${me.regenHp.toLocaleString('pl-PL')} HP/s)` : STAT_LABEL[s.key]}>
              <Icon />
              <b>{s.value}</b>
            </div>
          );
        })}
      </div>
      <div className="hh__portrait" title={`${hero.name} — ${hero.role}`} style={{ ['--role' as string]: color, ['--xp' as string]: `${xpRatio * 360}deg` }}>
        <div className="hh__portrait-ring" />
        <div className="hh__portrait-face">
          {portrait ? <img className="hh__portrait-img" src={portrait.face} alt="" draggable={false} /> : <HeroEmblem hero={hero.key} />}
        </div>
        <span className="hh__level">{me.level}</span>
        {me.points > 0 && <span className="hh__points" title="Punkty umiejętności do wydania">+{me.points}</span>}
      </div>

      <div className="hh__main">
        {me.points > 0 && (
          <div className="hh__learn">
            Punkt umiejętności: <b>{me.points}</b> · <kbd>Shift</kbd> + <kbd>Z</kbd>/<kbd>X</kbd>/<kbd>C</kbd>/<kbd>V</kbd>
          </div>
        )}
        <div className="hh__slots">
          {hero.abilities.map((a, i) => {
            const left = Math.max(0, ability.readyAt[i] - now);
            const total = ability.total[i] || a.cooldown * 1000;
            const ratio = left > 0 ? left / total : 0;
            const skill = me.skills[i];
            const cost = skill?.cost ?? a.cost ?? 0;
            const short = currentMana < cost;
            const passive = a.kind === 'passive' || a.kind === 'lightning' || a.kind === 'brewer' || a.kind === 'servant';
            const rank = skill?.rank ?? (passive ? 0 : 1);
            const rankable = (skill?.max ?? 0) > 0;
            const locked = rankable && rank === 0;
            const upgradable = rankable && skill.can && me.points > 0;
            const values = abilityValues(a, rank, me.ad, me.ap);
            const scaling = values ? `\n${values}` : '';
            return (
              <div key={a.key} className="hh__cell">
                {upgradable && (
                  <button className="hh__plus" onClick={() => act('skill.up', { slot: i })} title={`Ulepsz: ${a.name} (Shift + ${KEYS[i]})`} aria-label={`Ulepsz ${a.name}`}>
                    +
                  </button>
                )}
              <div className={`hh__slot ${left > 0 ? 'is-cooling' : ''} ${short ? 'is-short' : ''} ${passive ? 'is-passive' : ''} ${locked ? 'is-locked' : ''} ${upgradable ? 'is-upgradable' : ''}`} title={passive ? `${a.name} (pasywna${rankable ? `, stopień ${rank} z ${skill.max}` : ''}): ${a.text}${scaling}${locked ? ' — nie działa, dopóki jej nie nauczysz' : ''}` : `${a.name}${rankable ? ` — stopień ${rank} z ${skill.max}` : ''} (${cost} many): ${a.text}${scaling}${locked && skill.need > 1 ? ` (odblokowanie od poziomu ${skill.need})` : ''}`}>
                {ABILITY_ICON[a.key] ? ABILITY_ICON[a.key]() : <span className="hh__slot-glyph">{GLYPH[a.kind] ?? '✦'}</span>}
                {left > 0 && (
                  <>
                    <span className="hh__slot-veil" style={{ background: `conic-gradient(rgba(0,0,0,0.78) ${ratio * 360}deg, transparent 0)` }} />
                    <span className="hh__slot-time">{left > 9900 ? Math.ceil(left / 1000) : (left / 1000).toFixed(1)}</span>
                  </>
                )}
                <span className="hh__slot-key">{passive && !rankable ? 'P' : KEYS[i]}</span>
                {cost > 0 && <span className="hh__slot-cost">{cost}</span>}
                {passive && buffs.combo > 0 && <span className="hh__slot-combo">{buffs.combo}/{comboNeed}</span>}
                {locked && <span className="hh__slot-lock" aria-hidden>{upgradable ? '' : skill.need > me.level ? skill.need : ''}</span>}
              </div>
                {rankable && (
                  <span className={`hh__pips ${skill.ult ? 'is-ult' : ''}`} aria-label={`Stopień ${rank} z ${skill.max}`}>
                    {Array.from({ length: skill.max }, (_, n) => (
                      <i key={n} className={n < rank ? 'is-on' : ''} />
                    ))}
                  </span>
                )}
              </div>
            );
          })}
          {hero.abilities.length < 4 && <div className="hh__slot is-empty"><span className="hh__slot-key">V</span></div>}
        </div>

        <div className="hh__bars">
          {(hasStacks || tribe || hunter || timers.length > 0) && (
            <div className="hh__buffs" aria-label="Wzmocnienia">
              {hasStacks && (
                <span className={`hh__stacks ${left(buffs.primed) > 0 ? 'is-primed' : ''}`} title={`Stacki Głowy Najemnika: ${buffs.stacks} (+${Math.round(buffs.stacks * buffs.stackDamage)} obrażeń wzmocnionego ciosu)`}>
                  <StackIcon />
                  <b>{buffs.stacks}</b>
                </span>
              )}
              {tribe && (
                <span className={`hh__stacks ${buffs.tribe > 0 ? 'is-primed' : ''}`} title={`Postęp Plemienia: ${buffs.tribe} stacków (zabity mob +1, bohater +5; X na sojuszniku z focusem oddaje mu XP)`}>
                  <StackIcon />
                  <b>{buffs.tribe}</b>
                </span>
              )}
              {hunter && (
                <span className={`hh__combo ${buffs.combo >= comboNeed - 1 ? 'is-ready' : ''}`} title={`${hunter.name}: ${buffs.combo}/${comboNeed} trafień z rzędu w ten sam cel — następne ${buffs.combo >= comboNeed - 1 ? 'zada więcej obrażeń' : 'liczy się dalej'}`}>
                  {Array.from({ length: comboNeed }, (_, k) => (
                    <i key={k} className={k < buffs.combo ? 'is-on' : ''} />
                  ))}
                </span>
              )}
              {timers.map((t) => (
                <span key={t.key} className={`hh__buff hh__buff--${t.key}`} title={t.label}>
                  <i>{t.glyph}</i>
                  {(t.ms / 1000).toFixed(1)}
                </span>
              ))}
            </div>
          )}
          <div className={`hh__bar hh__bar--hp ${hpRatio < 0.3 ? 'is-low' : ''}`}>
            <i style={{ width: `${hpRatio * 100}%` }} />
            <span>
              {fmt(vital.hp)} / {fmt(vital.maxHp)}
            </span>
          </div>
          <div className="hh__bar hh__bar--mp">
            <i style={{ width: `${manaRatio * 100}%` }} />
            <span>
              {fmt(currentMana)} / {fmt(mana.max)}
            </span>
            {mana.regen > 0 && <em>+{mana.regen.toFixed(1)}</em>}
          </div>
        </div>
      </div>

      <div className="hh__side">
        <div className="hh__gold" title="Złoto">
          <span className="hh__coin" />
          <b>{fmt(me.gold)}</b>
        </div>
        <div className="hh__items" aria-label="Przedmioty">
          {Array.from({ length: shop.slots }, (_, i) => {
            const item = me.items[i] ? itemByKey(me.items[i]) : undefined;
            const cd = item && itemCooldown.cooldowns[item.key] ? itemCooldown.cooldowns[item.key] : null;
            return (
              <span key={i} className={`hh__item ${item ? `hh__item--${item.group}` : ''} ${item?.use ? 'is-usable' : ''}`} title={item ? `${item.name}${item.use ? ` (klawisz ${i + 4}${item.use.kind === 'active' ? `, odnowienie ${item.use.cooldown ?? 0} s` : ''})` : ''}` : `Slot ${i + 1}`}>
                {item && <ItemIcon itemKey={item.key} fallback={item.glyph} />}
                <kbd>{i + 4}</kbd>
                {item?.use && item.use.kind !== 'active' && <b>{me.charges[i] ?? 1}</b>}
                {cd && <i className="hh__cd" key={cd.at} style={{ animationDuration: `${cd.ms}ms` }} />}
              </span>
            );
          })}
        </div>
        {me.elixirs.length > 0 && (
          <div className="hh__elixirs" aria-label="Działające eliksiry">
            {me.elixirs.map((e) => {
              const buff = BUFFS[e.key];
              return (
                <span key={`${e.key}-${e.ms}`} className={`hh__elixir ${buff ? `hh__elixir--${buff.cls}` : ''}`} title={buff?.title ?? itemByKey(e.key)?.name ?? e.key}>
                  {buff ? buff.icon : <ItemIcon itemKey={e.key} />}
                  <i style={{ animationDuration: `${e.ms}ms` }} />
                </span>
              );
            })}
          </div>
        )}
        {shop.inside && !shop.open && <div className="hh__shophint"><kbd>P</kbd> sklep</div>}
        <div className="hh__xp" title={`Poziom ${me.level} — ${fmt(me.xp)} XP`}>
          <i style={{ width: `${xpRatio * 100}%` }} />
        </div>
      </div>
    </div>
  );
}

const BUFFS: Record<string, { cls: string; title: string; icon: JSX.Element }> = {
  buff_blue: {
    cls: 'blue',
    title: 'Błogosławieństwo Lodowego Golema: regeneracja many 3× szybsza (znika po śmierci)',
    icon: (
      <svg viewBox="0 0 24 24" fill="none" stroke="#8fd8ff" strokeWidth="1.8" strokeLinecap="round" aria-hidden>
        <path d="M12 3v18M4.2 7.5l15.6 9M19.8 7.5l-15.6 9" />
        <path d="M9.5 4.5 12 6.5l2.5-2M9.5 19.5 12 17.5l2.5 2" strokeWidth="1.4" />
      </svg>
    ),
  },
  buff_red: {
    cls: 'red',
    title: 'Błogosławieństwo Cieniostwora: twoje ataki podpalają (znika po śmierci)',
    icon: (
      <svg viewBox="0 0 24 24" fill="rgba(255,120,40,0.35)" stroke="#ff8a3d" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
        <path d="M12 3c1 3.2 4.5 5 4.5 9.2A4.5 4.5 0 0 1 12 17a4.5 4.5 0 0 1-4.5-4.8c0-1.8.9-3 1.9-4 .3 1.4.9 2.1 1.7 2.4C11 8.6 11 5.6 12 3z" />
      </svg>
    ),
  },
  buff_baron: {
    cls: 'baron',
    title: 'Ręka Barona: więcej AD i AP, wzmocnione miniony (znika po śmierci)',
    icon: (
      <svg viewBox="0 0 24 24" fill="rgba(166,91,216,0.35)" stroke="#c58cf0" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
        <path d="M5 20c0-6 2-9 4-11-1 2-1 3.5 0 4.5C9 9 10 6 12 3c.5 3 2 4.5 3 6.5.8-1 1-2.3.6-3.8C18 8 19 11 19 14c0 3-2.5 6-7 6s-7-1-7-0z" />
      </svg>
    ),
  },
};

function StackIcon() {
  return (
    <svg viewBox="0 0 24 24" width="15" height="15" aria-hidden>
      <path d="M12 1.5 22.5 12 12 22.5 1.5 12z" fill="#0d1a33" stroke="#6fb0ff" strokeWidth="1.4" />
      <path d="M12 6v12" stroke="#cfe6ff" strokeWidth="1.6" strokeLinecap="round" />
      <path d="M12.6 7.2c3 .2 4.6 2 4.6 4.4-2.2-.6-3.8-1.8-4.6-4.4z" fill="#8fc4ff" />
    </svg>
  );
}
