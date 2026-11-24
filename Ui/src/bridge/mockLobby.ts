import { deliver } from './bridge';
import type { HeroInfo, Mode, Profile, QueueInfo, RankingRow, RoomInfo, RoomRow } from '../features/lobby/lobbyStore';
import type { ShopItem } from '../features/shop/shopStore';
import mockItems from './mockItems.json';
import { creditOfTree, recipeTree } from '../features/shop/recipe';

const ab = (key: string, name: string, text: string, kind: string, cooldown: number, damage = 0, heal = 0) => ({
  key, name, text, kind, cooldown, range: 0, radius: 0, damage, heal, seconds: 0, cost: Math.round((20 + cooldown * 4) / 5) * 5,
});

const withRanks = (heroes: HeroInfo[]): HeroInfo[] => {
  heroes.forEach((h) => h.abilities.forEach((a, slot) => {
    if (['passive', 'lightning', 'brewer', 'servant'].includes(a.kind)) {
      const step = a.kind === 'passive' ? 25 : a.kind === 'lightning' ? 15 : 0;
      a.ranks = [1, 2, 3].map((r) => ({ damage: a.damage + step * (r - 1), heal: a.kind === 'brewer' ? a.heal + 5 * (r - 1) : a.heal, seconds: a.kind === 'brewer' ? 50 - 5 * (r - 1) : a.seconds, cooldown: 0, cost: 0, need: 2 * r - 1 }));
      return;
    }
    a.ultimate = h.abilities.length >= 4 && slot === h.abilities.length - 1;
    a.ranks = [1, 2, 3].map((r) => ({ damage: Math.round(a.damage * (1 + 0.3 * (r - 1))), heal: Math.round(a.heal * (1 + 0.3 * (r - 1))), seconds: a.seconds, cooldown: Math.round(a.cooldown * (1 - 0.08 * (r - 1)) * 10) / 10, cost: a.cost ?? 0, need: a.ultimate ? 1 + 3 * r : 2 * r - 1 }));
  }));
  return heroes;
};
const HEROES: HeroInfo[] = withRanks([
  { key: 'diego', name: 'Diego', title: 'Cień Starego Obozu', role: 'Strzelec', lore: 'Przywódca Cieni ze Starego Obozu.', price: 0, health: 560, strength: 70, dexterity: 110, weapon: 'bow',
    abilities: [{ ...ab('mysliwy', 'Myśliwy', 'Pasywna: co trzecie trafienie z rzędu zadaje 150% obrażeń.', 'passive', 0, 150), radius: 3, cost: 0 }, ab('przewrot', 'Przewrót', 'Przewrót w przód o 5 m.', 'roll', 7), ab('przeszywajaca', 'Przeszywająca Strzała', 'Następna strzała ogłusza na 1,5 s.', 'pierceshot', 10), ab('wielkilowca', 'Wielki Łowca', 'Na 15 s wzmacnia pozostałe zdolności.', 'huntermode', 60)] },
  { key: 'xardas', name: 'Xardas', title: 'Mroczny Mag', role: 'Mag', lore: 'Najpotężniejszy z nekromantów, niegdyś członek Kręgu Ognia. Sam rzadko staje do walki — wysyła szkielety i golemy.', price: 0, health: 640, strength: 60, dexterity: 50, mana: 600, weapon: 'twohanded',
    abilities: [ab('szkielet', 'Przyzwanie Szkieleta', 'Przyzywasz szkieleta, który atakuje twój cel i żyje 11 s.', 'summon', 8, 14, 110), { ...ab('piorun', 'Piorun', 'Pasywna: celne trafienie wrogiego gracza razi go piorunem z góry — razem 1,5× obrażeń.', 'lightning', 0, 50), cost: 0 }, ab('ucieczka', 'Ucieczka', 'Zostawia kopię, która biegnie tam, gdzie ty, albo stoi; znika po 3 s.', 'decoy', 18), ab('golem', 'Przyzwanie Golema', 'Przyzywa golema na 30 s; walczy jak szkielet.', 'summon', 40, 45, 700)] },
  { key: 'cipher', name: 'Cipher', title: 'Pijak z Bagien', role: 'Wojownik', lore: 'Łowca smoków z bagiennego obozu. Piwo go leczy, beczką ogłusza, a kiedy ma dość — zmienia się w cieniostwora.', price: 0, health: 820, strength: 100, dexterity: 60, mana: 420, weapon: 'onehanded',
    abilities: [{ ...ab('piwosz', 'Piwosz', 'Pasywna: poza walką co ok. 50 s wypijasz piwo i odzyskujesz 20% zdrowia.', 'brewer', 0, 0, 20), cost: 0 }, ab('beczka', 'Beczka', 'Celowanie strzałkami, lewy przycisk albo Enter rzuca, Esc anuluje. Po upadku ogłusza w obszarze na 2 s.', 'barrel', 12), ab('mocnaglowa', 'Mocna głowa', 'Przez 3 s odporność na ogłuszenia i trucizny.', 'ironhead', 18), ab('cieniostwor', 'Cieniostwór', 'Zmiana w cieniostwora na 10 s: szybsze ataki, cios +15%.', 'shadowform', 50)] },
  { key: 'kruk', name: 'Kruk', title: 'Wybraniec Beliara', role: 'Wojownik', lore: 'Były magnat Starego Obozu, który oddał się Beliarowi.', price: 0, health: 900, strength: 110, dexterity: 60, mana: 500, weapon: 'twoHanded',
    abilities: [ab('jezyk', 'Język Beliara', 'Czarna plama ogłusza wrogów po 1 s.', 'beliartongue', 15), { ...ab('sluga', 'Sługa', 'Pasywna: kopia zabitego bohatera walczy po twojej stronie 3,5 s.', 'servant', 0, 25), cost: 0 }, ab('okrazenie', 'Okrążenie', 'Pojawiasz się za wrogiem.', 'flank', 18), ab('silabeliara', 'Siła Beliara', 'Skok i wielki miecz Beliara.', 'beliarmight', 150, 220)] },
  { key: 'urshak', name: 'Ur-Shak', title: 'Szaman Plemienia', role: 'Mag', lore: 'Orkowy szaman, który służy plemieniu.', price: 0, health: 800, strength: 90, dexterity: 50, mana: 450, weapon: 'none',
    abilities: [ab('wielkakula', 'Wielka Kula', 'Płoniesz: 3 następne ciosy podpalają.', 'ignite', 9, 18), ab('postep', 'Postęp Plemienia', 'Stacki za zabójstwa -> XP dla sojusznika.', 'tribeprogress', 15, 10), ab('totem', 'Plemienny Posąg', 'Totem strzela 10 s.', 'totem', 22, 22), ab('plemie', 'Plemię jest najważniejsze', 'Sojusznicy niewrażliwi na obrażenia.', 'tribeguard', 90)] },
  { key: 'thorus', name: 'Thorus', title: 'Strażnik Bramy', role: 'Wojownik', lore: 'Dowódca Strażników Starego Obozu w pancerzu ze szczęką rekina.', price: 0, health: 950, strength: 110, dexterity: 60, mana: 380, weapon: 'twohanded',
    abilities: [ab('obrot', 'Obrót', 'Wymaga dobytej broni. Kołowrotek rani każdego wroga w zasięgu.', 'spin', 8, 70), ab('zadzakrwi', 'Żądza krwi', 'Przez 1 s następny cios w bohatera wysysa mu życie.', 'bloodlust', 12, 45), ab('przyciagniecie', 'Przyciągnięcie', 'Zagarnia do siebie moby i bohaterów wroga.', 'pull', 16), ab('egzekucja', 'Egzekucja', 'Skok i cięcie z góry. Zabicie bohatera zwraca manę i odnawia zdolność.', 'execute', 20, 170)] },
  { key: 'lee', name: 'Lee', title: 'Generał Najemników', role: 'Wojownik', lore: 'Dawny generał króla, wrobiony i zesłany za Barierę. Dowodzi najemnikami z farmy Onara. Z każdą bitwą jego topór uderza mocniej.', price: 0, health: 920, strength: 110, dexterity: 60, weapon: 'twohanded',
    abilities: [ab('glowa', 'Głowa Najemnika', 'Następne uderzenie toporem (6 s) zadaje +20 obrażeń i +2 za każdy stack, a trafienie daje 1 stack.', 'empower', 6, 20), ab('rozped', 'Rozpęd', 'Przez 3 s biegniesz o 30% szybciej.', 'haste', 12), ab('klatwa', 'Klątwa', 'Niebieski krąg przed tobą (4 s) lekko rani wrogów w środku.', 'curse', 10, 5), ab('pogrom', 'Pogrom', 'Na 8 s: ×1,3 wzrostu, +30% obrażeń, +50% zdrowia, leczenie 25%, stacki ×1,5.', 'rampage', 60, 30)] },
  { key: 'rycerz', name: 'Rycerz Królewski', title: 'Tarcza Myrtany', role: 'Tank', lore: 'Weteran bitew pod Vengardem. Wolny, ale niezłomny — idzie na czele linii i przyjmuje ciosy za innych.', price: 0, health: 900, strength: 100, dexterity: 60, weapon: 'onehanded',
    abilities: [ab('szarza', 'Szarża', 'Skok do przodu; wrogowie na drodze dostają obrażenia.', 'dash', 7, 40), ab('tarcza', 'Tarcza Wiary', 'Przez 4 s przyjmujesz o 60% mniej obrażeń.', 'shield', 14, 60), ab('uderzenie', 'Uderzenie Sprawiedliwości', 'Wybuch wokół ciebie: obrażenia i ogłuszenie na 1 s.', 'nova', 10, 55)] },
  { key: 'lowca', name: 'Łowca Cieni', title: 'Mistrz łuku', role: 'Strzelec', lore: 'Zwiadowca z Nowego Obozu. Trzyma dystans, a jego strzały znajdują cel nawet zza pleców minionów.', price: 0, health: 520, strength: 60, dexterity: 100, weapon: 'bow',
    abilities: [ab('strzal', 'Strzał Przebijający', 'Pocisk w najbliższego wroga przed tobą.', 'strike', 5, 70), ab('unik', 'Unik', 'Szybki skok do tyłu.', 'dash', 9), ab('deszcz', 'Deszcz Strzał', 'Obszarowy ostrzał w miejscu najbliższego wroga.', 'rain', 12, 60)] },
  { key: 'mag', name: 'Mag Ognia', title: 'Płomień Khorinis', role: 'Mag', lore: 'Uczeń Kręgu Ognia. Kruchy, lecz najgroźniejszy z daleka: kula ognia, lodowa fala i deszcz płomieni.', price: 500, health: 480, strength: 40, dexterity: 60, weapon: 'twohanded',
    abilities: [ab('kula', 'Kula Ognia', 'Pocisk w najbliższego wroga przed tobą.', 'strike', 4, 80), ab('fala', 'Lodowa Fala', 'Wybuch lodu wokół ciebie: obrażenia i ogłuszenie na 1,2 s.', 'nova', 11, 50), ab('deszczognia', 'Deszcz Ognia', 'Płonący deszcz w miejscu najbliższego wroga.', 'rain', 14, 90)] },
  { key: 'zabojca', name: 'Zabójca', title: 'Cień Portu', role: 'Zabójca', lore: 'Nóż w ciemności. Skacze na cel, rzuca sztyletami i kręci ostrzami wokół siebie.', price: 500, health: 560, strength: 90, dexterity: 100, weapon: 'onehanded',
    abilities: [ab('skok', 'Skok Cienia', 'Skok do przodu; wrogowie na drodze dostają obrażenia.', 'dash', 8, 60), ab('sztylet', 'Rzut Sztyletem', 'Pocisk w najbliższego wroga przed tobą.', 'strike', 3.5, 55), ab('wir', 'Wir Ostrzy', 'Obrażenia wokół ciebie.', 'nova', 9, 70)] },
  { key: 'kaplan', name: 'Kapłan Innosa', title: 'Światło Klasztoru', role: 'Wsparcie', lore: 'Leczy sojuszników, osłania ich tarczą i razi wrogów świętym pociskiem.', price: 650, health: 600, strength: 70, dexterity: 60, weapon: 'onehanded',
    abilities: [ab('pocisk', 'Święty Pocisk', 'Pocisk w najbliższego wroga przed tobą.', 'strike', 3.5, 55), ab('uzdrowienie', 'Uzdrowienie', 'Leczy ciebie i sojuszników w promieniu 6 m.', 'heal', 10, 0, 180), ab('oslona', 'Boska Tarcza', 'Przez 3 s przyjmujesz o 50% mniej obrażeń.', 'shield', 15, 50)] },
  { key: 'barbarzynca', name: 'Wojownik Orków', title: 'Krew i Stal', role: 'Wojownik', lore: 'Dwuręczny topór i szał bojowy. Wpada w tłum, ogłusza okrzykiem i rzuca toporem.', price: 700, health: 1000, strength: 120, dexterity: 50, weapon: 'twohanded',
    abilities: [ab('skokorka', 'Skok Berserkera', 'Skok do przodu; wrogowie na drodze dostają obrażenia.', 'dash', 8, 50), ab('okrzyk', 'Okrzyk Bojowy', 'Wybuch wokół ciebie: obrażenia i ogłuszenie na 1,2 s.', 'nova', 12, 45), ab('topor', 'Rzut Toporem', 'Pocisk w najbliższego wroga przed tobą.', 'strike', 6, 90)] },
]);

const RATIOS: Record<string, { ad?: number; ap?: number; healAd?: number; healAp?: number }> = {
  strzal: { ad: 0.6 }, deszcz: { ad: 0.45 }, szarza: { ad: 0.4 }, uderzenie: { ad: 0.5 }, skok: { ad: 0.5 }, sztylet: { ad: 0.7 }, wir: { ad: 0.5 },
  skokorka: { ad: 0.5 }, okrzyk: { ad: 0.4 }, topor: { ad: 0.7 }, kula: { ap: 0.7 }, fala: { ap: 0.5 }, deszczognia: { ap: 0.6 }, pocisk: { ap: 0.6 },
  uzdrowienie: { healAp: 0.5 }, glowa: { ad: 0.2 }, klatwa: { ad: 0.04 }, obrot: { ad: 0.6 }, zadzakrwi: { ad: 0.4 }, egzekucja: { ad: 0.8 },
  szkielet: { ap: 0.15, healAp: 0.9 }, golem: { ap: 0.4, healAp: 2.5 },
};
const MAGIC_ROLES = ['Mag', 'Wsparcie'];
HEROES.forEach((h) => h.abilities.forEach((a) => {
  const r = RATIOS[a.key];
  a.adRatio = r?.ad ?? 0; a.apRatio = r?.ap ?? 0; a.healAdRatio = r?.healAd ?? 0; a.healApRatio = r?.healAp ?? 0; a.magic = a.kind === 'curse' || MAGIC_ROLES.includes(h.role);
}));

const srv = (name: string, data: unknown) => deliver('srv', { name, data });

export function installLobbyMock(): void {
  const profile: Profile = { loaded: true, name: 'Hakuś', points: 620, lp: 128, wins: 14, losses: 9, ranked: 11, rank: 3, hero: 'diego', owned: ['diego', 'xardas', 'cipher', 'thorus', 'lee', 'rycerz', 'lowca'] };
  let rooms: RoomRow[] = [
    { id: 1, title: 'Gomez: z graczami', mode: 'normal', state: 'room', players: 2, max: 6, teamSize: 3, host: 'Gomez', seconds: 0 },
    { id: 2, title: 'Diego: z botami', mode: 'bots', state: 'running', players: 1, max: 1, teamSize: 1, host: 'Diego', seconds: 412 },
    { id: 3, title: 'Rankingowa', mode: 'ranked', state: 'running', players: 4, max: 4, teamSize: 2, host: 'Lares', seconds: 128 },
  ];
  let room: RoomInfo | null = null;
  let nextBot = 1;
  let loggedIn = false;
  const queue: QueueInfo = { active: false, waiting: 0, since: 0 };
  const ranking: RankingRow[] = [
    { rank: 1, name: 'Lares', lp: 412, wins: 51, losses: 22, you: false },
    { rank: 2, name: 'Gomez', lp: 266, wins: 33, losses: 20, you: false },
    { rank: 3, name: 'Hakuś', lp: 128, wins: 14, losses: 9, you: true },
    { rank: 4, name: 'Diego', lp: 74, wins: 8, losses: 10, you: false },
  ];

  const pushAll = () => {
    srv('heroes', HEROES);
    srv('profile', profile);
    srv('rooms', rooms);
    srv('queue', queue);
    srv('room', room);
    srv('ranking', ranking);
  };
  const toast = (text: string, kind = 'info') => srv('toast', { text, kind });
  const makeRoom = (mode: Mode, teamSize: number): RoomInfo => ({
    id: 9, title: `Hakuś: ${mode === 'bots' ? 'z botami' : 'z graczami'}`, mode, state: 'room', teamSize, isHost: true, canStart: true, botSlots: { blue: true, red: true }, countdown: 0, seconds: 0,
    blue: [{ name: 'Hakuś', hero: profile.hero ?? 'rycerz', host: true, you: true, kills: 0, deaths: 0, lp: profile.lp ?? 0 }], red: [], winner: null,
  });

  window.setTimeout(() => {
    deliver('phase', { phase: 'lobby', match: 0 });
    srv('auth', { status: 'login', name: '', error: '' });
  }, 50);

  const shopCatalog = mockItems as unknown as ShopItem[];
  let shopGold = 1400;
  let shopOwned: string[] = ['nowicjusz_ostrze', 'mik_zdrowie', 'kilof'];
  const shopAdvice = { hero: 'Diego', role: 'Strzelec', build: ['wiecznosc', 'przypomnienie', 'kruszyciel', 'zbieracz', 'krwiopijca'], sections: [
    { id: 'start', title: 'Na start', hint: 'Kup od razu w bazie: przedmiot startowy i mikstury zdrowia (klawisze 4–9).', items: ['nowicjusz_ostrze', 'nowicjusz_luk', 'mik_zdrowie'] },
    { id: 'early', title: 'Wczesne', hint: 'Składniki pierwszych przedmiotów — kup, gdy wrócisz do bazy z mniejszą ilością złota.', items: ['sztylet', 'zwinnosc', 'kilof', 'bf'] },
    { id: 'core', title: 'Kluczowe', hint: 'Najważniejsze przedmioty dla roli „Strzelec”, w tej kolejności.', items: ['wiecznosc', 'przypomnienie', 'kruszyciel'] },
    { id: 'late', title: 'Na później', hint: 'Domknięcie ekwipunku.', items: ['zbieracz', 'krwiopijca'] },
    { id: 'situational', title: 'Sytuacyjne', hint: 'Gdy wróg mocno bije: pancerz na fizycznych, odporność na magów.', items: ['omenmurow', 'zaslona', 'zniwiarz', 'gniewwygnanej', 'plugawa'] },
  ] };
  const creditMock = (item: ShopItem) => creditOfTree(recipeTree(item, shopOwned, (key) => shopCatalog.find((i) => i.key === key)));
  const priceOf = (item: ShopItem): number => Math.max(0, item.cost - creditMock(item).credit);
  const pushShop = (withCatalog = false) => {
    srv('me', { kills: 0, deaths: 0, assists: 1, cs: 0, gold: shopGold, level: 5, xp: 0, xpFloor: 0, xpNext: 100, aa: 80, aaMagic: false, critMult: 200, ad: 80, ap: 0, crit: 0, armor: 40, resist: 25, cdr: 0, regen: 0, regenHp: 2, items: shopOwned, charges: shopOwned.map(() => 1), elixirs: [] });
    srv('shop', { open: true, owned: shopOwned, charges: shopOwned.map(() => 1), slots: 6, sellRatio: 0.7, prices: shopCatalog.map(priceOf), catalog: withCatalog ? shopCatalog : null, advice: shopAdvice });
  };
  window.__mockShop = () => {
    deliver('phase', { phase: 'game', match: 1 });
    pushShop(true);
  };

  const boardRows = () => [
    { id: 0, team: 0, name: 'Hakuś', hero: 'diego', bot: false, you: true, lp: 128, level: 7, kills: 5, deaths: 2, assists: 6, cs: 84, items: ['berlo', 'zadlo', 'mlot'], ad: 142, ap: 0, armor: 58, resist: 31, alive: true },
    { id: 3, team: 0, name: 'Lares', hero: 'lee', bot: false, you: false, lp: 412, level: 8, kills: 9, deaths: 1, assists: 10, cs: 102, items: ['rebacz'], ad: 168, ap: 0, armor: 91, resist: 44, alive: true },
    { id: 1, team: 0, name: 'Xardas (bot)', hero: 'xardas', bot: true, you: false, lp: 0, level: 6, kills: 2, deaths: 3, assists: 3, cs: 47, items: ['nowicjusz_pierscien', 'starykostur'], ad: 40, ap: 100, armor: 18, resist: 40, alive: false },
    { id: 5, team: 1, name: 'Gomez', hero: 'thorus', bot: false, you: false, lp: 266, level: 6, kills: 3, deaths: 6, assists: 4, cs: 61, items: ['kosa', 'kolczuga'], ad: 121, ap: 0, armor: 77, resist: 38, alive: true },
    { id: 2, team: 1, name: 'Cipher (bot)', hero: 'cipher', bot: true, you: false, lp: 0, level: 7, kills: 4, deaths: 2, assists: 5, cs: 66, items: ['rebacz'], ad: 85, ap: 0, armor: 35, resist: 20, alive: true },
    { id: 4, team: 1, name: 'Diego (bot)', hero: 'diego', bot: true, you: false, lp: 0, level: 4, kills: 1, deaths: 5, assists: 2, cs: 38, items: ['nowicjusz_luk', 'bf'], ad: 88, ap: 0, armor: 20, resist: 18, alive: false },
  ];
  const mockTimers = () => [
    { key: 'drake', name: 'Smok', state: 'wait', left: 143 },
    { key: 'baron', name: 'Baron', state: 'soon', left: 0 },
  ];
  window.__mockBoard = () => {
    deliver('phase', { phase: 'game', match: 1 });
    srv('shop', { open: false, owned: [], charges: [], slots: 6, sellRatio: 0.7, prices: shopCatalog.map((i) => i.cost), catalog: shopCatalog });
    srv('board', { rows: boardRows(), seconds: 754, blue: 16, red: 10, timers: mockTimers() });
    deliver('board.show', { on: true });
  };

  window.__mockDamage = () => {
    deliver('phase', { phase: 'game', match: 1 });
    let n = 0;
    const timer = window.setInterval(() => {
      const kind = n % 3, taken = n % 4 === 3;
      deliver('dmg.pop', { x: taken ? 900 : 1100 + (n % 5) * 18, y: taken ? 560 : 420 - (n % 3) * 6, amount: 30 + ((n * 37) % 240), kind, taken });
      if (++n >= 16) window.clearInterval(timer);
    }, 140);
  };

  window.__mockResult = (ranked = true, won = true) => {
    deliver('phase', { phase: 'game', match: 1 });
    srv('shop', { open: false, owned: [], charges: [], slots: 6, sellRatio: 0.7, prices: shopCatalog.map((i) => i.cost), catalog: shopCatalog });
    const row = (id: number, bot: boolean, you: boolean, team: number, name: string, hero: string, level: number, k: number, d: number, a: number, cs: number, gold: number,
      dc: number, dobj: number, dm: number, taken: number, items: string[], mvp = false) => ({ id, bot, you, team, name, hero, level, left: false, kills: k, deaths: d, assists: a, cs, gold,
      dmgChampions: dc, dmgObjectives: dobj, dmgMinions: dm, taken, items, mvp });
    const rows = [
      row(0, false, true, 0, 'Hakuś', 'diego', 12, 11, 3, 9, 142, 14820, 21840, 3210, 9400, 11800, ['berlo', 'zadlo', 'mlot'], true),
      row(3, false, false, 0, 'Lares', 'lee', 11, 7, 2, 14, 118, 12950, 14120, 5820, 6100, 19400, ['rebacz']),
      row(1, true, false, 0, 'Xardas (bot)', 'xardas', 10, 3, 5, 8, 71, 9420, 9870, 1200, 5300, 8200, ['nowicjusz_pierscien', 'starykostur']),
      row(5, false, false, 1, 'Gomez', 'thorus', 11, 6, 7, 5, 103, 11860, 12300, 4100, 7700, 21500, ['kosa', 'kolczuga'], true),
      row(2, true, false, 1, 'Cipher (bot)', 'cipher', 10, 4, 6, 6, 88, 10240, 11250, 2800, 6900, 17200, ['rebacz']),
      row(4, true, false, 1, 'Diego (bot)', 'diego', 9, 2, 8, 3, 64, 8120, 8960, 900, 4100, 9800, ['nowicjusz_luk', 'bf']),
    ];
    const sum = (team: number, pick: (r: (typeof rows)[number]) => number) => rows.filter((r) => r.team === team).reduce((s, r) => s + pick(r), 0);
    const team = (t: number, towers: number, inhibitors: number, drakes: number, barons: number) => ({ team: t, kills: sum(t, (r) => r.kills), deaths: sum(t, (r) => r.deaths),
      assists: sum(t, (r) => r.assists), gold: sum(t, (r) => r.gold), damage: sum(t, (r) => r.dmgChampions), towers, inhibitors, drakes, barons });
    const me = rows[0];
    const chance = 58;
    const raw = Math.round(32 * ((won ? 1 : 0) - chance / 100));
    const limited = won ? Math.min(40, Math.max(6, raw)) : Math.max(-40, Math.min(-6, raw));
    srv('result', {
      won, winner: won ? 'blue' : 'red', team: 'blue', hero: 'diego', kills: me.kills, deaths: me.deaths, assists: me.assists, botKills: me.cs, points: won ? 180 : 70,
      lpChange: ranked ? limited : 0, lp: 128 + (ranked ? limited : 0), ranked, seconds: 1624, mode: ranked ? 'ranked' : 'normal', rows,
      teams: won ? [team(0, 9, 2, 3, 1), team(1, 3, 0, 1, 0)] : [team(0, 3, 0, 1, 0), team(1, 9, 2, 3, 1)],
      lpInfo: ranked ? { own: 1240, other: 1210, chance, k: 32, raw, limited, min: won ? 6 : -40, max: won ? 40 : -6, applied: limited } : null,
    });
  };

  let mockRanks = [0, 0, 0, 0];
  const mockSkills = () => {
    const hero = HEROES.find((h) => h.key === profile.hero) ?? HEROES[0];
    const level = 4;
    const points = Math.max(0, level - mockRanks.reduce((a, b) => a + b, 0));
    const need = (ult: boolean, rank: number) => (ult ? 1 + 3 * rank : 2 * rank - 1);
    return {
      points,
      skills: hero.abilities.map((a, i) => {
        const max = a.ranks ? 3 : 0;
        const next = Math.min(3, mockRanks[i] + 1);
        return { rank: mockRanks[i], max, can: max > 0 && mockRanks[i] < 3 && points > 0 && level >= need(!!a.ultimate, next), need: max ? need(!!a.ultimate, next) : 0, ult: !!a.ultimate, cost: a.cost ?? 0 };
      }),
    };
  };

  const navStrokes: { pts: number[]; r: number; cost: number }[] = [];
  const mk = (id: string, k: string, n: string, lane: string, team: number, x: number, z: number, a: number, t: number, extra: Record<string, unknown> = {}) =>
    ({ id, k, n, lane, team, x, y: 130, z, a, t, c: false, dx: x, dz: z, da: a, ...extra });
  const objs = [
    mk('s|Środek|Blue|Turret|1|1', 'turret', 'Wieża 1 niebieskich (Środek)', 'Środek', 0, 1864, 363, 258, 1),
    mk('s|Środek|Blue|Turret|2|1', 'turret', 'Wieża 2 niebieskich (Środek)', 'Środek', 0, 3561, 3600, 211, 2),
    mk('s|Środek|Red|Turret|1|1', 'turret', 'Wieża 1 czerwonych (Środek)', 'Środek', 1, -1088, -544, 68, 1),
    mk('s|Środek|Blue|Inhibitor|1|1', 'inhibitor', 'Inhibitor niebieskich (Środek)', 'Środek', 0, 6894, 6667, 35, 1),
    mk('s|Środek|Blue|Nexus|1|1', 'nexus', 'Nexus niebieskich (Środek)', 'Środek', 0, 8623, 8578, 221, 1),
    mk('s|Środek|Blue|Dragon|1|1', 'dragon', 'Głowa smoka niebieskich (Środek)', 'Środek', 0, 10179, 10415, 228, 1),
    ...[[9867, 10185], [5487, 5218], [1698, 991], [-4602, -5392], [-9656, -9873]].map(([x, z], i, all) =>
      mk(`p|Środek|${i}`, 'point', i === 0 ? 'Środek: spawn niebieskich' : i === all.length - 1 ? 'Środek: spawn czerwonych' : `Środek: punkt ${i + 1}`, 'Środek', -1, x, z, 0, 0, { i, last: i === all.length - 1 })),
  ] as { id: string; k: string; n: string; lane: string; team: number; x: number; y: number; z: number; a: number; t: number; c: boolean; dx: number; dz: number; da: number; i?: number; last?: boolean }[];
  const wp = {
    next: 8,
    nodes: [
      { id: 1, n: 'Środek-Start-N', x: 9867, y: 575, z: 10185 },
      { id: 2, n: 'Środek-P1', x: 5487, y: 131, z: 5218 },
      { id: 3, n: 'Środek-1.1', x: 3045, y: 131, z: 2540 },
      { id: 4, n: 'Środek-P2', x: 1698, y: 131, z: 991 },
      { id: 5, n: 'Teren-1', x: 4200, y: 131, z: 8000 },
      { id: 6, n: 'Teren-2', x: 7000, y: 131, z: 7600 },
      { id: 7, n: 'Brama-kryjowka', x: 800, y: 131, z: 5000 },
    ],
    links: [[1, 2], [2, 3], [3, 4], [5, 2], [6, 2], [6, 1]] as [number, number][],
  };
  return void (window.__mockLobby = (name: string, json: string) => {
    const args = JSON.parse(json || '{}') as Record<string, unknown>;
    switch (name) {
      case 'skill.up': {
        const slot = Number(args.slot);
        const entry = mockSkills().skills[slot];
        if (entry?.can) mockRanks[slot] += 1;
        srv('me', { kills: 3, deaths: 1, assists: 4, cs: 12, gold: 671, level: 4, xp: 520, xpFloor: 450, xpNext: 700, ad: 86, ap: 0, crit: 25, armor: 52, resist: 34, cdr: 12, regen: 11.4, regenHp: 2.3, ...mockSkills() });
        break;
      }
      case 'feedback.send': {
        const bad = String(args.message ?? '').toLowerCase().includes('limit');
        window.setTimeout(() => srv('feedback.result', bad ? { ok: false, id: 0, error: 'Limit: 5 zgłoszeń na godzinę. Spróbuj trochę później.' } : { ok: true, id: 42, error: '' }), 700);
        break;
      }
      case 'board.get':
        srv('board', { rows: boardRows(), seconds: 754, blue: 16, red: 10, timers: mockTimers() });
        break;
      case 'nav.open':
        srv('nav', { strokes: navStrokes });
        break;
      case 'nav.wp.open':
      case 'nav.wp.add':
      case 'nav.wp.move':
      case 'nav.wp.rename':
      case 'nav.wp.delete':
      case 'nav.wp.link':
      case 'nav.wp.generate':
      case 'nav.wp.undo': {
        let selected = -1;
        const id = Number(args.id);
        if (name === 'nav.wp.add') {
          wp.nodes.push({ id: wp.next, n: `wp-${wp.next}`, x: Number(args.x), y: 130, z: Number(args.z) });
          selected = wp.next++;
        } else if (name === 'nav.wp.move') {
          const n = wp.nodes.find((x) => x.id === id);
          if (n) { n.x = Number(args.x); n.z = Number(args.z); }
          selected = id;
        } else if (name === 'nav.wp.rename') {
          const n = wp.nodes.find((x) => x.id === id);
          if (n && String(args.name).trim()) n.n = String(args.name).trim();
          selected = id;
        } else if (name === 'nav.wp.delete') {
          wp.nodes = wp.nodes.filter((x) => x.id !== id);
          wp.links = wp.links.filter(([a, b]) => a !== id && b !== id);
        } else if (name === 'nav.wp.link') {
          const a = Number(args.a), b = Number(args.b);
          const had = wp.links.some(([p, q]) => (p === a && q === b) || (p === b && q === a));
          wp.links = had ? wp.links.filter(([p, q]) => !((p === a && q === b) || (p === b && q === a))) : [...wp.links, [a, b]];
          selected = b;
        }
        srv('wp', { selected, nodes: wp.nodes, links: wp.links });
        break;
      }
      case 'nav.obj.open':
      case 'nav.obj.move':
      case 'nav.obj.angle':
      case 'nav.obj.addpoint':
      case 'nav.obj.delpoint':
      case 'nav.obj.reset': {
        let selected = '';
        const id = String(args.id ?? '');
        const o = objs.find((x) => x.id === id);
        if (name === 'nav.obj.move' && o) { o.x = Number(args.x); o.z = Number(args.z); o.c = true; selected = id; }
        else if (name === 'nav.obj.angle' && o) { o.a = ((Number(args.angle) % 360) + 360) % 360; o.c = true; selected = id; }
        else if (name === 'nav.obj.addpoint') {
          const lane = String(args.lane);
          const after = Number(args.after);
          const idx = objs.findIndex((x) => x.k === 'point' && x.lane === lane && x.i === after);
          if (idx >= 0) {
            objs.splice(idx + 1, 0, { id: '', k: 'point', n: '', lane, team: -1, x: Number(args.x), y: 130, z: Number(args.z), a: 0, t: 0, c: true, dx: Number(args.x), dz: Number(args.z), da: 0, i: 0 });
            let n = 0;
            for (const p of objs.filter((x) => x.k === 'point' && x.lane === lane)) { p.i = n; p.id = `p|${lane}|${n}`; p.n = `${lane}: punkt ${n + 1}`; p.c = true; n += 1; }
            selected = `p|${lane}|${after + 1}`;
          }
        } else if (name === 'nav.obj.delpoint' && o?.k === 'point' && !o.last && o.i !== 0) {
          objs.splice(objs.indexOf(o), 1);
          let n = 0;
          const pts = objs.filter((x) => x.k === 'point' && x.lane === o.lane);
          for (const p of pts) { p.i = n; p.id = `p|${o.lane}|${n}`; p.last = n === pts.length - 1; n += 1; }
        } else if (name === 'nav.obj.reset' && o) { o.x = o.dx; o.z = o.dz; o.a = o.da; o.c = false; selected = id; }
        srv('obj', { selected, items: objs.map(({ dx, dz, da, ...rest }) => rest) });
        break;
      }
      case 'nav.paint':
        navStrokes.push(args as never);
        break;
      case 'nav.undo':
        navStrokes.pop();
        srv('nav.strokes', { strokes: navStrokes });
        break;
      case 'nav.clear':
        navStrokes.length = 0;
        srv('nav.strokes', { strokes: [] });
        break;
      case 'nav.route':
      case 'nav.wp.route': {
        const a = args.a as number[];
        const b = args.b as number[];
        srv(name === 'nav.wp.route' ? 'wp.route' : 'nav.route', { points: [...a, (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, ...b], ms: 1.2 });
        break;
      }
      case 'ui.ready':
        if (loggedIn) pushAll();
        else srv('auth', { status: 'login', name: '', error: '' });
        break;
      case 'auth.login':
      case 'auth.register': {
        const user = String(args.user ?? '');
        if (String(args.pass) === 'zlehaslo') {
          srv('auth', { status: 'login', name: '', error: name === 'auth.login' ? 'Zła nazwa albo hasło.' : 'Ta nazwa jest już zajęta.' });
          break;
        }
        loggedIn = true;
        profile.name = user;
        window.setTimeout(() => {
          srv('auth', { status: 'ok', name: user, error: '' });
          pushAll();
        }, 400);
        break;
      }
      case 'auth.logout':
        loggedIn = false;
        room = null;
        srv('auth', { status: 'login', name: '', error: '' });
        break;
      case 'room.create': {
        room = makeRoom(args.mode as Mode, Number(args.teamSize));
        srv('room', room);
        break;
      }
      case 'room.join': {
        const r = rooms.find((x) => x.id === Number(args.id));
        if (!r) break;
        room = { ...makeRoom(r.mode, r.teamSize), id: r.id, title: r.title, isHost: false, canStart: false,
          blue: [{ name: r.host, hero: 'mag', host: true, you: false, kills: 0, deaths: 0, lp: 74 }, { name: 'Hakuś', hero: profile.hero ?? 'rycerz', host: false, you: true, kills: 0, deaths: 0, lp: 128 }], red: [] };
        srv('room', room);
        break;
      }
      case 'room.leave':
        room = null;
        srv('room', null);
        break;
      case 'room.team':
        break;
      case 'bot.add': {
        if (!room) break;
        const side = args.team === 'red' ? 'red' : 'blue';
        const bot = { name: (HEROES.find((h) => h.key === args.hero)?.name ?? String(args.hero)) + ' (bot)', hero: String(args.hero), host: false, you: false, kills: 0, deaths: 0, lp: 0, bot: true, id: nextBot++ };
        room = { ...room, [side]: [...room[side], bot] };
        srv('room', room);
        break;
      }
      case 'bot.remove': {
        if (!room) break;
        room = { ...room, blue: room.blue.filter((p) => p.id !== args.id || !p.bot), red: room.red.filter((p) => p.id !== args.id || !p.bot) };
        srv('room', room);
        break;
      }
      case 'room.start':
        if (room) {
          srv('room', (room = { ...room, state: 'countdown', countdown: 6 }));
          window.setTimeout(() => {
            deliver('phase', { phase: 'game', match: 9 });
            srv('hud', { hero: profile.hero });
            srv('score', { blue: 0, red: 0, seconds: 0 });
            deliver('hud.player', { hp: 540, maxHp: 900 });
            srv('mana', { current: 310, max: 520, regen: 6.2 });
            srv('buffs', { stacks: 23, stackDamage: 2, primed: 0, haste: 0, rampage: 0, pierce: 30000, hunt: 30000, hidden: 0, combo: 2 });
            srv('me', { kills: 3, deaths: 1, assists: 4, cs: 12, gold: 671, level: 4, xp: 520, xpFloor: 450, xpNext: 700, ad: 86, ap: 0, crit: 25, armor: 52, resist: 34, cdr: 12, regen: 11.4, regenHp: 2.3, ...mockSkills() });
            const lane: [number, number][] = [[9867, 10185], [5487, 5218], [3045, 2540], [1698, 991], [-46, -511], [-1879, -2073], [-4602, -5392], [-6562, -7339], [-9656, -9873]];
            const at = (k: number): [number, number] => {
              const f = Math.min(0.999, Math.max(0, k)) * (lane.length - 1);
              const i = Math.floor(f);
              const [x0, z0] = lane[i];
              const [x1, z1] = lane[i + 1];
              return [x0 + (x1 - x0) * (f - i), z0 + (z1 - z0) * (f - i)];
            };
            const m0 = Date.now();
            window.setInterval(() => {
              const k = ((Date.now() - m0) / 60000) % 1;
              const [x, z] = at(k);
              const [nx, nz] = at(k + 0.01);
              const l = Math.hypot(nx - x, nz - z) || 1;
              deliver('mm.me', { x, z, ax: (nx - x) / l, az: (nz - z) / l });
            }, 100);
            window.setInterval(() => {
              const k = ((Date.now() - m0) / 60000) % 1;
              const minions: [number, number, number][] = [];
              for (let i = 0; i < 6; i++) {
                const [bx, bz] = at(k + 0.02 + i * 0.012);
                minions.push([bx, bz, 0]);
                const [rx, rz] = at(0.55 - (k * 0.3) - i * 0.012);
                minions.push([rx, rz, 1]);
              }
              const [ex, ez] = at(0.48);
              srv('minimap', {
                c: [[...at(k), 0, 1, 100, 'diego'], [...at(k - 0.03), 0, 0, 80, 'lee'], [ex, ez, 1, 0, 60, 'thorus']],
                m: minions,
                t: [[1864, 363, 0, 100, 0], [-1088, -544, 1, 70, 0], [3561, 3600, 0, 100, 0], [6470, 6122, 0, 100, 0], [6894, 6667, 0, 100, 1], [7988, 8609, 0, 100, 0], [8710, 7691, 0, 100, 0], [8623, 8578, 0, 100, 2], [-6259, -5810, 1, 100, 0], [-6683, -6355, 1, 100, 1], [-7777, -8297, 1, 100, 0], [-8499, -7379, 1, 100, 0], [-8412, -8266, 1, 100, 2]],
              });
            }, 500);
            window.setTimeout(() => deliver('mm.mode', {}), 20000);
            let blue = 0;
            let red = 0;
            const t0 = Date.now();
            window.setInterval(() => {
              if (Math.random() < 0.5) blue++; else red++;
              srv('score', { blue, red, seconds: Math.floor((Date.now() - t0) / 1000) });
            }, 9000);
          }, 1500);
        }
        break;
      case 'queue.join':
        queue.active = true;
        queue.since = 0;
        queue.waiting = 3;
        srv('queue', queue);
        break;
      case 'queue.leave':
        queue.active = false;
        srv('queue', queue);
        break;
      case 'hero.select':
        profile.hero = String(args.key);
        srv('profile', profile);
        break;
      case 'item.buy': {
        const item = shopCatalog.find((i) => i.key === args.key);
        if (!item) break;
        const { slots: usedAt, credit } = creditMock(item);
        const price = Math.max(0, item.cost - credit);
        if (shopGold < price) { toast('Za mało złota.', 'error'); break; }
        if (shopOwned.filter(Boolean).length - usedAt.length + 1 > 6) { toast('Brak miejsca w ekwipunku.', 'error'); break; }
        shopGold -= price;
        usedAt.forEach((i) => { shopOwned[i] = ''; });
        const hole = shopOwned.indexOf('');
        const place = usedAt.length ? Math.min(...usedAt) : hole >= 0 ? hole : shopOwned.length;
        while (shopOwned.length <= place) shopOwned.push('');
        shopOwned[place] = item.key;
        while (shopOwned.length && !shopOwned[shopOwned.length - 1]) shopOwned.pop();
        shopOwned = [...shopOwned];
        pushShop();
        break;
      }
      case 'item.sell': {
        const slot = Number(args.slot);
        const item = shopCatalog.find((i) => i.key === shopOwned[slot]);
        if (item) shopGold += Math.round(item.cost * 0.7);
        shopOwned[slot] = '';
        while (shopOwned.length && !shopOwned[shopOwned.length - 1]) shopOwned.pop();
        shopOwned = [...shopOwned];
        pushShop();
        break;
      }
      case 'item.move': {
        const from = Number(args.from), to = Number(args.to);
        if (!shopOwned[from] || to < 0 || to >= 6 || to === from) break;
        while (shopOwned.length <= to) shopOwned.push('');
        [shopOwned[from], shopOwned[to]] = [shopOwned[to], shopOwned[from]];
        while (shopOwned.length && !shopOwned[shopOwned.length - 1]) shopOwned.pop();
        shopOwned = [...shopOwned];
        pushShop();
        break;
      }
      case 'item.close':
        break;
      case 'shop.buy': {
        const hero = HEROES.find((h) => h.key === args.key);
        if (!hero || (profile.points ?? 0) < hero.price) {
          toast('Brakuje punktów.', 'error');
          break;
        }
        profile.points = (profile.points ?? 0) - hero.price;
        profile.owned = [...(profile.owned ?? []), hero.key];
        srv('profile', profile);
        toast(`Kupiono: ${hero.name}.`, 'ok');
        break;
      }
    }
    rooms = [...rooms];
  });
}

declare global {
  interface Window {
    __mockLobby?: (name: string, json: string) => void;
    __mockShop?: () => void;
    __mockBoard?: () => void;
    __mockResult?: (ranked?: boolean, won?: boolean) => void;
    __mockDamage?: () => void;
  }
}
