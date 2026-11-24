import { useEffect, useState } from 'react';
import { useStore } from '../../state/store';
import { act, heroOf, lobbyStore, type AbilityInfo, type HeroInfo, type RankInfo } from './lobbyStore';
import { ABILITY_ICON } from '../hud/abilityIcons';
import { PORTRAIT } from '../../assets/heroes/portraits';
import { Btn, Icon } from './ui';

const KEYS = ['Z', 'X', 'C', 'V'];

export function HeroPlaque({ hero, children }: { hero?: HeroInfo; children?: React.ReactNode }) {
  if (!hero) {
    return (
      <aside className="lb-panel lb-detail">
        <h3 className="lb-panel__title">Bohaterowie</h3>
        <div className="lb-empty">Wybierz kogoś z galerii</div>
      </aside>
    );
  }
  const bar = (value: number, max: number) => <i style={{ width: `${Math.min(100, (value / max) * 100)}%` }} />;
  const card = PORTRAIT[hero.key]?.card;
  return (
    <aside className="lb-panel lb-detail" key={hero.key}>
      <div className="lb-detail__hero">
        {card && <img src={card} alt={hero.name} draggable={false} />}
        <span className="lb-detail__fade" />
        <div className="lb-detail__name">
          <small>
            {hero.title} · {hero.role}
          </small>
          <h3>{hero.name}</h3>
        </div>
      </div>
      <div className="lb-detail__scroll">
        <div className="lb-stats">
          <div className="lb-stat">
            <span>Zdrowie</span>
            <div>{bar(hero.health, 1000)}</div>
            <output>{hero.health}</output>
          </div>
          <div className="lb-stat">
            <span>Siła</span>
            <div>{bar(hero.strength, 130)}</div>
            <output>{hero.strength}</output>
          </div>
          <div className="lb-stat">
            <span>Zręczność</span>
            <div>{bar(hero.dexterity, 130)}</div>
            <output>{hero.dexterity}</output>
          </div>
        </div>

        <div className="lb-abilities">
          {hero.abilities.map((a, i) => (
            <div className="lb-ability" key={a.key}>
              <span className="lb-ability__icon">
                {ABILITY_ICON[a.key] ? ABILITY_ICON[a.key]() : <Icon name="flame" />}
                <kbd>{KEYS[i]}</kbd>
              </span>
              <div>
                <b>{a.name}</b>
                <small>{a.text}</small>
                <em>
                  {['passive', 'lightning', 'brewer', 'servant'].includes(a.kind) ? 'pasywna' : `odnowienie ${a.cooldown} s`}
                  {a.damage > 0 && !['shield', 'haste', 'rampage', 'passive', 'lightning', 'decoy', 'brewer', 'barrel', 'ironhead', 'pull', 'servant'].includes(a.kind)
                    ? ` · ${a.kind === 'summon' ? 'obrażenia ciosu' : a.kind === 'bloodlust' ? 'kradzież' : 'obrażenia'} ${a.damage}${ratio(a.adRatio, a.apRatio)}${a.kind === 'summon' || a.kind === 'bloodlust' ? '' : a.magic ? ' magiczne' : ' fizyczne'}`
                    : ''}
                  {a.kind === 'summon' ? ` · zdrowie ${a.heal}${ratio(a.healAdRatio, a.healApRatio)}` : a.heal > 0 ? ` · leczenie ${a.heal}${ratio(a.healAdRatio, a.healApRatio)}` : ''}
                </em>
                {a.ranks && a.ranks.length > 0 && (
                  <span className="lb-ranks">
                    {a.ultimate && <i className="lb-ranks__ult">Ultimate</i>}
                    {a.ranks.map((r, n) => (
                      <span key={n} title={`Stopień ${n + 1}: od poziomu ${r.need}`}>
                        <b>{['I', 'II', 'III'][n]}</b> {rankLead(a, r)}{r.cooldown > 0 ? ` · ${r.cooldown} s` : ''}
                      </span>
                    ))}
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>

        <p className="lb-lore">{hero.lore}</p>
        {children}
      </div>
    </aside>
  );
}

function ratio(ad?: number, ap?: number): string {
  const parts: string[] = [];
  if (ad && ad > 0) parts.push(`+${Math.round(ad * 100)}% AD`);
  if (ap && ap > 0) parts.push(`+${Math.round(ap * 100)}% AP`);
  return parts.length ? ` (${parts.join(', ')})` : '';
}

function rankLead(a: AbilityInfo, r: RankInfo): string {
  switch (a.kind) {
    case 'passive':
      return `${r.damage}% co 3. cios`;
    case 'lightning':
      return `+${r.damage}% piorunem`;
    case 'brewer':
      return `${r.heal}% zdrowia co ${r.seconds} s`;
    case 'servant':
      return `sługa ${r.seconds} s`;
    case 'beliartongue':
      return `ogłuszenie ${r.seconds} s`;
    case 'heal':
      return `${r.heal} lecz.`;
    case 'shield':
      return `−${r.damage}% obr.`;
    case 'haste':
      return `+${r.damage}% tempa`;
    case 'rampage':
      return `+${r.damage}% obr.`;
    case 'empower':
    case 'summon':
      return `${r.damage} obr.`;
    case 'pierceshot':
    case 'huntermode':
    case 'decoy':
    case 'ironhead':
    case 'shadowform':
    case 'barrel':
      return `${r.seconds} s`;
    default:
      return r.damage > 0 ? `${r.damage} obr.` : `${r.seconds} s`;
  }
}

function HeroGallery({ heroes, selected, onPick, owned, showPrice }: { heroes: HeroInfo[]; selected: string; onPick: (key: string) => void; owned: Set<string>; showPrice: boolean }) {
  const chosen = lobbyStore.get().profile?.hero;
  return (
    <div className="lb-gallery">
      {heroes.map((h) => {
        const have = owned.has(h.key);
        return (
          <button key={h.key} className={`lb-hcard ${selected === h.key ? 'is-selected' : ''} ${showPrice && !have ? 'is-locked' : ''}`} onClick={() => onPick(h.key)}>
            <span className="lb-hcard__in">
              {PORTRAIT[h.key]?.card ? <img src={PORTRAIT[h.key].card} alt="" draggable={false} /> : <span className="lb-hcard__blank" />}
              <span className="lb-hcard__shade" />
              {showPrice ? (
                have ? (
                  <span className="lb-tag lb-tag--ok">posiadasz</span>
                ) : (
                  <span className="lb-tag lb-tag--price">
                    <Icon name="coin" size="0.85rem" /> {h.price}
                  </span>
                )
              ) : (
                chosen === h.key && <span className="lb-tag lb-tag--ok">wybrany</span>
              )}
              <span className="lb-hcard__name">
                <b>{h.name}</b>
                <small>{h.role}</small>
              </span>
            </span>
          </button>
        );
      })}
    </div>
  );
}

export function HeroesTab() {
  const s = useStore(lobbyStore);
  const owned = new Set(s.profile?.owned ?? []);
  const mine = s.heroes.filter((h) => owned.has(h.key));
  const [picked, setPicked] = useState('');
  useEffect(() => {
    if (!picked && mine.length > 0) setPicked(s.profile?.hero ?? mine[0].key);
  }, [mine.length, picked, s.profile?.hero]);
  const hero = heroOf(picked, s.heroes);
  const chosen = s.profile?.hero === picked;
  return (
    <main className="lb-split">
      <section className="lb-split__main">
        <h2 className="lb-title">
          <span>Twoi bohaterowie</span>
        </h2>
        {mine.length === 0 ? <div className="lb-empty">Profil się wczytuje…</div> : <HeroGallery heroes={mine} selected={picked} onPick={setPicked} owned={owned} showPrice={false} />}
        <div className="lb-split__act">
          <Btn xl variant="primary" disabled={!hero || chosen} onClick={() => act('hero.select', { key: picked })}>
            {chosen ? 'Wybrany' : 'Wybierz bohatera'}
          </Btn>
        </div>
      </section>
      <HeroPlaque hero={hero} />
    </main>
  );
}

export function ShopTab() {
  const s = useStore(lobbyStore);
  const owned = new Set(s.profile?.owned ?? []);
  const [picked, setPicked] = useState('');
  const sorted = [...s.heroes].sort((a, b) => Number(owned.has(a.key)) - Number(owned.has(b.key)) || a.price - b.price);
  useEffect(() => {
    if (!picked && sorted.length > 0) setPicked(sorted[0].key);
  }, [sorted.length, picked]);
  const hero = heroOf(picked, s.heroes);
  const points = s.profile?.points ?? 0;
  const has = hero ? owned.has(hero.key) : false;
  return (
    <main className="lb-split">
      <section className="lb-split__main">
        <h2 className="lb-title">
          <span>Sklep bohaterów</span>
        </h2>
        <HeroGallery heroes={sorted} selected={picked} onPick={setPicked} owned={owned} showPrice />
        <div className="lb-split__act">
          <Btn xl variant="primary" disabled={!hero || has || points < (hero?.price ?? 0)} onClick={() => act('shop.buy', { key: picked })}>
            {has ? 'Już posiadasz' : `Kup za ${hero?.price ?? 0} pkt`}
          </Btn>
          <span className="lb-hint">
            <Icon name="coin" size="1rem" /> Masz <b>{points}</b> pkt · punkty dostajesz za każdą grę
          </span>
        </div>
      </section>
      <HeroPlaque hero={hero} />
    </main>
  );
}
