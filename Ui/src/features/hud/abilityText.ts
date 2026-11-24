import type { AbilityInfo } from '../lobby/lobbyStore';

function ratioParts(ad?: number, ap?: number): string {
  const parts: string[] = [];
  if (ad && ad > 0) parts.push(`+${Math.round(ad * 100)}% AD`);
  if (ap && ap > 0) parts.push(`+${Math.round(ap * 100)}% AP`);
  return parts.join(', ');
}

const NOT_DAMAGE = ['shield', 'haste', 'rampage', 'passive', 'lightning', 'decoy', 'brewer', 'barrel', 'ironhead', 'pull', 'shadowform', 'pierceshot', 'huntermode', 'roll', 'servant', 'tribeprogress', 'tribeguard'];

export function abilityValues(a: AbilityInfo, rank: number, ad: number, ap: number): string {
  const base = a.ranks?.[Math.max(1, rank) - 1];
  const lines: string[] = [];
  const line = (label: string, value: number, adRatio?: number, apRatio?: number) => {
    const text = ratioParts(adRatio, apRatio);
    if (!text) return;
    const bonus = Math.round((adRatio ?? 0) * ad + (apRatio ?? 0) * ap);
    lines.push(`${label} ${value + bonus} (${value} ${text.replace(/\+(\d+)%/g, '+ $1%')} = ${bonus})`);
  };
  const damage = base?.damage ?? a.damage;
  const heal = base?.heal ?? a.heal;
  if (damage > 0 && !NOT_DAMAGE.includes(a.kind)) {
    const label = a.kind === 'summon' ? 'obrażenia ciosu' : a.kind === 'bloodlust' ? 'kradzież' : a.kind === 'curse' ? 'obrażenia co 0,5 s' : a.kind === 'ignite' ? 'podpalenie na sekundę' : a.kind === 'totem' ? 'obrażenia strzału' : 'obrażenia';
    line(label, damage, a.adRatio, a.apRatio);
  }
  if (heal > 0) line(a.kind === 'summon' ? 'zdrowie' : 'leczenie', heal, a.healAdRatio, a.healApRatio);
  if (lines.length === 0) return '';
  const type = a.kind === 'summon' || a.kind === 'bloodlust' || damage <= 0 ? '' : a.magic ? ' [magiczne]' : ' [fizyczne]';
  return `${lines.join(' · ')}${type}`;
}
