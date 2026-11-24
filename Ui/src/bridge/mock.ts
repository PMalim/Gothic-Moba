import { deliver, setOutboundSink } from './bridge';
import type { AdminPlayer, BuilderState, ChatCommand } from './protocol';
import { builderDefaults } from '../state/stores';
import { installLobbyMock } from './mockLobby';

const COMMANDS: ChatCommand[] = [
  { name: 'team', aliases: ['druzyna'], description: 'twoja drużyna albo zmiana: /team niebiescy | /team czerwoni', admin: false },
  { name: 'heal', aliases: [], description: 'przywraca pełne zdrowie', admin: false },
  { name: 'arena', aliases: [], description: 'teleport na środek areny', admin: false },
  { name: 'bots', aliases: ['moby'], description: 'stan obozów', admin: false },
  { name: 'stats', aliases: [], description: 'twoje zabicia', admin: false },
  { name: 'ai', aliases: [], description: 'podgląd AI w 3D (przełącza)', admin: false },
  { name: 'admin', aliases: [], description: 'logowanie admina hasłem: /admin <hasło>', admin: false },
  { name: 'wave', aliases: ['fala'], description: 'wypuszcza falę minionów z obu baz od razu', admin: true },
  { name: 'spawn', aliases: [], description: 'przywołuje moby obok ciebie, np. /spawn wilk 3', admin: true },
  { name: 'kick', aliases: [], description: 'wyrzuca gracza: /kick <nick|id> [powód]', admin: true },
  { name: 'gold', aliases: ['zloto'], description: 'daje złoto: /gold <ilość> [nick|id]', admin: true },
  { name: 'vobs', aliases: [], description: 'world builder: filtr katalogu, np. /vobs barrel', admin: true },
];

const PLAYERS: AdminPlayer[] = [
  { id: 0, name: 'Hakuś', hp: 540, maxHp: 600, ping: 22, dead: false, ghost: false, admin: true },
  { id: 1, name: 'Gomez', hp: 600, maxHp: 600, ping: 48, dead: false, ghost: false, admin: false },
  { id: 2, name: 'Diego', hp: 0, maxHp: 600, ping: 71, dead: true, ghost: false, admin: false },
  { id: 3, name: 'Lares', hp: 310, maxHp: 600, ping: 35, dead: false, ghost: true, admin: true },
];

const SAMPLE = ['NW_HARBOUR_BARREL_01.3DS','NW_CITY_BEAM_01.3DS','OC_CHEST_01.3DS','ITFO_APPLE.3DS','EVT_CHEST_01.3DS','MIN_LAMP_01.3DS'];
let placed: [number, string, number, number, number, number][] = Array.from({ length: 48 }, (_, i) => [100 + i, SAMPLE[i % SAMPLE.length], 1000 + i * 350, 100, -2000 + (i % 7) * 600, 0]);

const time = (): string => new Date().toTimeString().slice(0, 5);

function startWorldMock(): void {
  deliver('world.add', { id: 1, kind: 'bot', name: 'Mag', team: 0, maxHp: 80 });
  deliver('world.add', { id: 2, kind: 'bot', name: 'Siepacz', team: 1, maxHp: 80 });
  deliver('world.add', { id: 3, kind: 'bot', name: 'Wilk', team: -1, maxHp: 60 });
  deliver('world.add', { id: 4, kind: 'tower', name: 'Wieża niebieskich', team: 0, maxHp: 2500 });
  deliver('world.add', { id: 5, kind: 'tower', name: 'Wieża czerwonych', team: 1, maxHp: 2500 });
  let t = 0;
  window.setInterval(() => {
    t += 0.03;
    const w = window.innerWidth;
    const h = window.innerHeight;
    const hp = (base: number, speed: number) => Math.round(base * (0.55 + 0.45 * Math.sin(t * speed)));
    deliver('world.frame', {
      f: [
        1, w * 0.3 + Math.sin(t) * 120, h * 0.55, hp(80, 1.3), 900,
        2, w * 0.45 + Math.cos(t) * 150, h * 0.5, hp(80, 0.9), 1700,
        3, w * 0.6, h * 0.62 + Math.sin(t) * 30, hp(60, 1.7), 2400,
        4, w * 0.25, h * 0.3, hp(2500, 0.4), 2600,
        5, w * 0.78, h * 0.28, 2500, 5200,
      ],
    });
    deliver('hud.player', { hp: hp(600, 0.5), maxHp: 600 });
  }, 33);
}

export function installMock(): void {
  installLobbyMock();
  let typing = false;
  let chatOpen = false;
  let adminOpen = false;
  let flying = false;
  let menuOpen = false;
  let builder: BuilderState = { ...builderDefaults, vobs: 128, storage: 'MySQL (mock)' };
  const fly = { speed: 900, pitch: -12, yaw: 214 };

  const pushVobs = () => deliver('builder.vobs', { cam: [0, 300, 0], rows: placed });
  const pushBuilder = () => {
    deliver('builder.state', builder);
  };
  const pushFly = () => deliver('fly', { on: flying, ...fly });
  const system = (text: string, color: [number, number, number] = [255, 220, 120]) =>
    deliver('chat.line', { kind: 'system', color, text, time: time() });
  const note = (text: string) => {
    builder = { ...builder, note: text };
    pushBuilder();
    window.setTimeout(() => {
      if (builder.note === text) {
        builder = { ...builder, note: '' };
        pushBuilder();
      }
    }, 2500);
  };
  const setFly = (on: boolean) => {
    flying = on;
    pushFly();
  };
  const setBuilder = (on: boolean) => {
    builder = { ...builder, on, panelFocus: on, mode: 'place', note: '' };
    if (on && !flying) setFly(true);
    if (!on && flying) setFly(false);
    pushBuilder();
    if (on) pushVobs();
  };

  setOutboundSink((name, args) => {
    switch (name) {
      case 'ready':
        deliver('init', { name: 'Hakuś', resX: window.innerWidth, resY: window.innerHeight, admin: true });
        deliver('chat.commands', { commands: COMMANDS });
        deliver('admin.players', { players: PLAYERS, minions: 14 });
        pushBuilder();
        startWorldMock();
        system('Arena C#: wokół stoją obozy bandytów, wilków, ścierwojadów, nieumarłych i orków. /help – komendy.', [255, 215, 0]);
        deliver('chat.line', { kind: 'player', author: 'Gomez', authorColor: [120, 160, 255], color: [255, 255, 255], text: 'Ktoś widział wartownika przy wieży?', time: time() });
        deliver('chat.line', { kind: 'player', author: 'Diego', authorColor: [255, 110, 110], color: [255, 255, 255], text: 'Tak, idzie fala z południa!', time: time() });
        break;
      case 'ui.typing':
        typing = Boolean(args[0]);
        break;
      case 'chat.closed':
        chatOpen = false;
        break;
      case 'chat.send': {
        const text = String(args[0]);
        if (text.startsWith('/')) system(`Wykonano komendę ${text.split(' ')[0]}.`, [200, 200, 200]);
        else deliver('chat.line', { kind: 'player', author: 'Hakuś', authorColor: [255, 255, 255], color: [255, 255, 255], text, time: time() });
        break;
      }
      case 'builder.select':
        break;
      case 'builder.goto':
        note(`Kamera przy voba #${String(args[0])}`);
        break;
      case 'builder.grab':
        builder = { ...builder, mode: 'grab', grabbed: Number(args[0]), panelFocus: false, holding: true };
        pushBuilder();
        break;
      case 'builder.remove':
        placed = placed.filter((r) => r[0] !== Number(args[0]));
        note(`Usunięto voba #${String(args[0])}`);
        break;
      case 'builder.hide': {
        placed = placed.map((r) => (r[0] === Number(args[0]) ? [r[0], r[1], r[2], r[3], r[4], r[5] ? 0 : 1] : r));
        builder = { ...builder, hidden: placed.filter((r) => r[5]).length };
        pushBuilder();
        pushVobs();
        break;
      }
      case 'builder.highlight':
      case 'builder.gallery':
        break;
      case 'builder.preview':
        break;
      case 'builder.set': {
        const key = args[0] as string;
        const value = Number(args[1]);
        builder = { ...builder, [key]: key === 'collision' ? value !== 0 : value };
        pushBuilder();
        break;
      }
      case 'builder.action':
        switch (args[0]) {
          case 'panel':
            builder = { ...builder, panelFocus: !builder.panelFocus };
            pushBuilder();
            if (builder.panelFocus) pushVobs();
            break;
          case 'look':
            builder = { ...builder, panelFocus: false, holding: true };
            pushBuilder();
            break;
          case 'reset':
            builder = { ...builder, pitch: 0, roll: 0, scale: 1, lift: 0 };
            pushBuilder();
            break;
          case 'lock':
            builder = { ...builder, locked: !builder.locked };
            note(builder.locked ? 'Kamera zablokowana: strzałki ruszają obiektem (Shift = szybciej)' : 'Kamera odblokowana');
            break;
          case 'showall':
            placed = placed.map((r) => [r[0], r[1], r[2], r[3], r[4], 0]);
            builder = { ...builder, hidden: 0 };
            pushBuilder();
            pushVobs();
            break;
          case 'exit':
            setBuilder(false);
            break;
          case 'delete':
            builder = { ...builder, mode: 'place', grabbed: -1, holding: false };
            note('Usunięto voba');
            break;
          case 'cancel':
            builder = { ...builder, mode: 'place', grabbed: -1, holding: false };
            note('Anulowano');
            break;
        }
        break;
      case 'admin.action':
        system(`[mock] akcja admina ${String(args[0])} na ${String(args[1])}`, [180, 220, 255]);
        break;
      case 'admin.close':
        adminOpen = false;
        deliver('admin.panel', { open: false });
        break;
      case 'fly.toggle':
        setFly(!flying);
        break;
      case 'srv.action':
        window.__mockLobby?.(String(args[0]), String(args[1]));
        break;
      case 'menu.close':
        menuOpen = false;
        deliver('menu.state', { open: false });
        break;
      case 'menu.exit':
        system('[mock] wyjście z gry', [255, 160, 160]);
        break;
    }
  });

  window.addEventListener('keydown', (e) => {
    if (typing || chatOpen) return;
    if (e.key === 'Escape' && !adminOpen && !(builder.on && builder.panelFocus)) {
      menuOpen = !menuOpen;
      deliver('menu.state', { open: menuOpen });
      return;
    }
    switch (e.key) {
      case 'Enter':
      case '/':
        chatOpen = true;
        deliver('chat.open', { text: e.key === '/' ? '/' : '' });
        e.preventDefault();
        break;
      case 'F6':
        adminOpen = !adminOpen;
        deliver('admin.panel', { open: adminOpen });
        e.preventDefault();
        break;
      case 'F7':
        setFly(!flying);
        e.preventDefault();
        break;
      case 'F9':
        window.__mockLobby?.('nav.open', '{}');
        e.preventDefault();
        break;
      case 'F8':
        setBuilder(!builder.on);
        e.preventDefault();
        break;
      case 'Tab':
        if (builder.on) {
          builder = { ...builder, panelFocus: !builder.panelFocus };
          pushBuilder();
          if (builder.panelFocus) pushVobs();
        }
        e.preventDefault();
        break;
      case 'q':
      case 'e':
        if (builder.on) deliver('builder.key', { key: e.key === 'q' ? 'prev' : 'next' });
        break;
      case 'PageUp':
      case 'PageDown':
        if (builder.on) deliver('builder.key', { key: e.key === 'PageUp' ? 'catPrev' : 'catNext' });
        break;
      case 'z':
      case 'c':
        if (builder.on) {
          builder = { ...builder, yaw: (builder.yaw + (e.key === 'c' ? 15 : 345)) % 360 };
          pushBuilder();
        }
        break;
    }
  });

  const bar = document.createElement('div');
  bar.className = 'mock-bar';
  bar.innerHTML = '<span>PODGLĄD (bez gry):</span>';
  const add = (label: string, fn: () => void) => {
    const b = document.createElement('button');
    b.textContent = label;
    b.onclick = () => {
      b.blur();
      fn();
    };
    bar.append(b);
  };
  add('Enter – czat', () => {
    chatOpen = true;
    deliver('chat.open', { text: '' });
  });
  add('F6 – admin', () => {
    adminOpen = !adminOpen;
    deliver('admin.panel', { open: adminOpen });
  });
  add('F7 – lot', () => setFly(!flying));
  add('F8 – builder', () => setBuilder(!builder.on));
  add('wiadomość', () => system('Test: wiadomość systemowa ' + time(), [255, 255, 255]));
  document.body.append(bar);
}
