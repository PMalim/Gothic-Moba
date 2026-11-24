'use strict';
const $ = (selector, root = document) => root.querySelector(selector);
const KINDS = { bug: 'Błąd', complaint: 'Skarga', idea: 'Pomysł', balance: 'Balans', other: 'Inne' };
const STATUS = { new: 'Nowe', seen: 'Przeczytane', done: 'Załatwione', rejected: 'Odrzucone' };
const MODES = { bots: 'z botami', normal: 'z graczami', ranked: 'rankingowa' };
const STATES = { room: 'pokój', countdown: 'odliczanie', running: 'trwa', finished: 'zakończona' };

const app = { tab: 'overview', data: null, game: null, authed: false, chat: { last: 0, entries: [], match: '' }, gameChat: { last: 0, entries: [], match: null }, reports: [], bounds: {} };

function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (value == null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key.startsWith('on')) el.addEventListener(key.slice(2), value);
    else el.setAttribute(key, value === true ? '' : value);
  }
  for (const kid of kids.flat()) if (kid != null && kid !== false) el.append(kid.nodeType ? kid : document.createTextNode(String(kid)));
  return el;
}
const fmtTime = (seconds) => `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;
function fmtUptime(since) {
  const s = Math.max(0, Math.floor(Date.now() / 1000 - since)), d = Math.floor(s / 86400), hh = Math.floor((s % 86400) / 3600), mm = Math.floor((s % 3600) / 60);
  return (d ? d + ' d ' : '') + hh + ' h ' + mm + ' min';
}

async function api(path, options = {}) {
  const init = { credentials: 'same-origin', ...options, headers: { 'X-Requested-With': 'webcenter', ...(options.body ? { 'Content-Type': 'application/json' } : {}) } };
  const response = await fetch(path, init);
  if (response.status === 401) { showLogin(); throw new Error('login'); }
  if (!response.ok) throw new Error('http ' + response.status);
  return response.json();
}

function showLogin(message) {
  app.authed = false;
  $('#login').classList.remove('hidden');
  $('#logout').classList.add('hidden');
  $('#loginError').textContent = message || '';
  setConn(false);
  $('#password').focus();
}
$('#loginForm').addEventListener('submit', async (event) => {
  event.preventDefault();
  const password = $('#password').value;
  const response = await fetch('/api/login', { method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'webcenter' }, body: JSON.stringify({ password }) });
  if (response.ok) {
    $('#password').value = '';
    $('#login').classList.add('hidden');
    $('#logout').classList.remove('hidden');
    app.authed = true;
    poll();
    loadReportsBadge();
  } else if (response.status === 429) {
    const body = await response.json().catch(() => ({}));
    $('#loginError').textContent = `Za dużo prób. Poczekaj ${body.seconds || 60} s.`;
  } else $('#loginError').textContent = 'Nieprawidłowe hasło.';
});
$('#logout').addEventListener('click', async () => { try { await api('/api/logout', { method: 'POST' }); } catch (e) {  } showLogin(); });

function setConn(ok) {
  const pill = $('#conn');
  pill.textContent = ok ? 'połączono' : 'brak połączenia';
  pill.className = 'pill ' + (ok ? 'on' : 'off');
}

$('#tabs').addEventListener('click', (event) => {
  const tab = event.target.closest('button')?.dataset.tab;
  if (tab) selectTab(tab);
});
function selectTab(tab) {
  app.tab = tab;
  for (const button of document.querySelectorAll('#tabs button')) button.classList.toggle('on', button.dataset.tab === tab);
  for (const section of document.querySelectorAll('.tab')) section.classList.toggle('hidden', section.id !== tab);
  if (tab === 'reports') loadReports();
  if (tab === 'chat') pollChat();
  render();
}

async function poll() {
  if (!app.authed) return;
  try {
    app.data = await api('/api/state');
    setConn(true);
    render();
    if (app.tab === 'games' && app.game != null) pollGameChat();
    if (app.tab === 'chat') pollChat();
  } catch (error) { if (error.message !== 'login') setConn(false); }
}
setInterval(() => { if (!document.hidden) poll(); }, 1500);

function render() {
  const data = app.data;
  if (!data) return;
  $('#gamesBadge').textContent = data.matches.length;
  $('#playersBadge').textContent = data.players.length;
  if (app.tab === 'overview') renderOverview(data);
  else if (app.tab === 'games') renderGames(data);
  else if (app.tab === 'players') renderPlayers(data);
  else if (app.tab === 'chat') fillChatFilter(data);
}

function card(key, value, sub, fill) {
  const kids = [h('div', { class: 'k' }, key), h('div', { class: 'v' }, value)];
  if (sub) kids.push(h('div', { class: 's' }, sub));
  if (fill != null) kids.push(h('div', { class: 'bar' }, h('i', { class: fill > 90 ? 'bad' : fill > 75 ? 'warn' : '', style: `width:${Math.min(100, fill)}%` })));
  return h('div', { class: 'card' }, kids);
}
function renderOverview(data) {
  const s = data.server;
  const memPercent = s.totalMb ? (s.workingSetMb / s.totalMb) * 100 : null;
  $('#cards').replaceChildren(
    card('RAM (zestaw roboczy)', [`${s.workingSetMb} `, h('small', {}, 'MB')], `prywatna ${s.privateMb} MB · z ${s.totalMb} MB maszyny`, memPercent),
    card('Sterta .NET', [`${s.heapMb} `, h('small', {}, 'MB')], `GC: gen0 ${s.gen0} · gen1 ${s.gen1} · gen2 ${s.gen2}`),
    card('CPU procesu', [`${s.cpu} `, h('small', {}, '%')], `${s.threads} wątków`, s.cpu),
    card('Tick trybu', [`${s.tickAvgMs} `, h('small', {}, 'ms')], `maks. ${s.tickMaxMs} ms · ${s.ticks} ticków / 2 s`, Math.min(100, s.tickMaxMs / 0.5)),
    card('Gracze online', s.players, `${data.players.filter((p) => p.match).length} w grach`),
    card('Gry', s.matches, `${data.matches.filter((m) => m.state === 'running').length} trwa`),
    card('Moby (NPC) w grach', s.mobs, 'miniony, wieże, obozy, boty'),
    card('Czas działania', fmtUptime(s.startedAt), s.persistent ? 'baza: ' + s.storage : 'UWAGA: ' + s.storage),
  );
  const labels = data.history;
  draw($('#chMem'), [labels.map((p) => p[1]), labels.map((p) => p[2])], ['#e0a63a', '#4aa3ff']);
  draw($('#chCpu'), [labels.map((p) => p[3])], ['#4ccf8b'], 100);
  draw($('#chTick'), [labels.map((p) => p[4]), labels.map((p) => p[5])], ['#4ccf8b', '#ff6b6b']);
  $('#memLegend').textContent = 'żółty: zestaw roboczy · niebieski: sterta';
}

function draw(canvas, series, colors, fixedMax) {
  const ratio = window.devicePixelRatio || 1, width = canvas.clientWidth, height = 150;
  canvas.width = width * ratio; canvas.height = height * ratio;
  const g = canvas.getContext('2d');
  g.scale(ratio, ratio);
  g.clearRect(0, 0, width, height);
  const all = series.flat();
  if (all.length < 2) return;
  const max = fixedMax || Math.max(...all, 1) * 1.15, left = 38, top = 8, plotW = width - left - 6, plotH = height - top - 16;
  const css = getComputedStyle(document.documentElement);
  g.strokeStyle = css.getPropertyValue('--line'); g.fillStyle = css.getPropertyValue('--muted'); g.font = '11px system-ui'; g.lineWidth = 1;
  for (let i = 0; i <= 3; i++) {
    const y = top + (plotH * i) / 3;
    g.beginPath(); g.moveTo(left, y); g.lineTo(width - 6, y); g.stroke();
    const label = max * (1 - i / 3); g.fillText(max > 20 ? Math.round(label) : Math.round(label * 10) / 10, 2, y + 4);
  }
  series.forEach((values, index) => {
    g.strokeStyle = colors[index]; g.lineWidth = 2; g.beginPath();
    values.forEach((value, i) => {
      const x = left + (plotW * i) / Math.max(1, values.length - 1), y = top + plotH * (1 - value / max);
      i ? g.lineTo(x, y) : g.moveTo(x, y);
    });
    g.stroke();
  });
  g.fillStyle = css.getPropertyValue('--muted');
  g.fillText('−' + Math.round((series[0].length * 2) / 60) + ' min', left, height - 3);
  g.fillText('teraz', width - 36, height - 3);
}

function renderGames(data) {
  const list = $('#gameList');
  if (!data.matches.length) list.replaceChildren(h('p', { class: 'empty' }, 'Nie trwa żadna gra.'));
  else list.replaceChildren(...data.matches.map((m) =>
    h('div', { class: 'game' + (m.id === app.game ? ' on' : ''), onclick: () => { app.game = m.id; app.gameChat = { last: 0, entries: [], match: m.id }; render(); pollGameChat(); } },
      h('div', { class: 't' }, h('span', {}, m.title), h('span', { class: 'chip ' + m.state }, STATES[m.state] || m.state)),
      h('div', { class: 'm' }, h('span', {}, `#${m.id} · ${MODES[m.mode] || m.mode}`), h('span', {}, `${m.players.length} graczy${m.bots.length ? ' + ' + m.bots.length + ' botów' : ''}`),
        h('span', { class: 'score' }, h('b', { class: 'b' }, m.blue), ' : ', h('b', { class: 'r' }, m.red)), m.state === 'running' ? h('span', {}, fmtTime(m.seconds)) : null))));
  const match = data.matches.find((m) => m.id === app.game);
  if (!match) { if (app.game != null) { app.game = null; } $('#gameView').replaceChildren(h('p', { class: 'empty' }, 'Wybierz grę z listy.')); return; }
  renderGameView(match);
}

function teamTable(match, team) {
  const players = match.players.filter((p) => p.team === team), bots = match.bots.filter((b) => b.team === team);
  const rows = players.map((p) => h('tr', { class: p.dead ? 'dead' : '' },
    h('td', {}, h('span', { class: 'teamtag t' + team }), p.name), h('td', {}, p.hero), h('td', { class: 'num' }, p.level),
    h('td', { class: 'num' }, `${p.kills}/${p.deaths}/${p.cs}`), h('td', { class: 'num' }, p.gold),
    h('td', {}, h('span', { class: 'hp' }, h('i', { style: `width:${p.maxHp ? Math.max(0, p.hp * 100 / p.maxHp) : 0}%` })), ' ', `${p.hp}`)));
  for (const b of bots) rows.push(h('tr', {}, h('td', {}, h('span', { class: 'teamtag t' + team }), b.name), h('td', {}, b.hero), h('td', { class: 'num' }, '–'), h('td', { class: 'num' }, `${b.kills}/${b.deaths}/–`), h('td', { class: 'num' }, '–'), h('td', {}, 'bot')));
  return h('div', { class: 'panel' }, h('h3', {}, team === 0 ? 'Niebiescy' : 'Czerwoni'),
    h('table', {}, h('tr', {}, h('th', {}, 'Gracz'), h('th', {}, 'Bohater'), h('th', { class: 'num' }, 'Lvl'), h('th', { class: 'num' }, 'K/D/CS'), h('th', { class: 'num' }, 'Złoto'), h('th', {}, 'Zdrowie')), rows.length ? rows : h('tr', {}, h('td', { colspan: 6, class: 'muted' }, 'brak'))));
}

function renderGameView(match) {
  let view = $('#gameView');
  if (!view.dataset.game || view.dataset.game !== String(match.id)) {
    view.dataset.game = match.id;
    view.replaceChildren(h('div', { id: 'gvHead', class: 'gv-head' }), h('div', { class: 'gv-grid' }, h('div', { id: 'gvTeams', class: 'gameview' }), h('div', { class: 'gameview' },
      h('div', { class: 'panel mapbox' }, h('h3', {}, 'Mapa na żywo'), h('canvas', { id: 'gvMap', height: 340 }), h('div', { class: 'legend' }, '■ wieża  ◆ inhibitor  ★ nexus  ● bohater  · minion')),
      h('div', { class: 'panel' }, h('h3', {}, 'Czat tej gry'), h('div', { id: 'gvChat', class: 'chatbox small' })))));
  }
  $('#gvHead').replaceChildren(...[h('h2', {}, match.title), h('span', { class: 'chip ' + match.state }, STATES[match.state] || match.state), h('span', { class: 'muted' }, `#${match.id} · ${MODES[match.mode] || match.mode}`),
    h('span', { class: 'score' }, h('b', { class: 'b' }, match.blue), ' : ', h('b', { class: 'r' }, match.red)), match.state === 'running' || match.state === 'finished' ? h('span', {}, fmtTime(match.seconds)) : null,
    h('span', { class: 'muted' }, `gospodarz: ${match.host || '–'}`), match.away ? h('span', { class: 'chip' }, `${match.away} poza grą (może wrócić)`) : null,
    match.winner ? h('span', { class: 'chip' }, 'wygrali: ' + (match.winner === 'blue' ? 'niebiescy' : 'czerwoni')) : null].filter(Boolean));
  $('#gvTeams').replaceChildren(teamTable(match, 0), teamTable(match, 1));
  drawMap($('#gvMap'), match);
  paintChat($('#gvChat'), app.gameChat.entries, '', false);
}

function drawMap(canvas, match) {
  const map = match.map, width = canvas.clientWidth, height = 340, ratio = window.devicePixelRatio || 1;
  const points = [...map.structures, ...map.champions, ...map.minions];
  const b = app.bounds[match.id] || (app.bounds[match.id] = { x0: Infinity, x1: -Infinity, z0: Infinity, z1: -Infinity });
  for (const p of points) { b.x0 = Math.min(b.x0, p[0]); b.x1 = Math.max(b.x1, p[0]); b.z0 = Math.min(b.z0, p[1]); b.z1 = Math.max(b.z1, p[1]); }
  canvas.width = width * ratio; canvas.height = height * ratio;
  const g = canvas.getContext('2d'); g.scale(ratio, ratio); g.clearRect(0, 0, width, height);
  if (!points.length) { g.fillStyle = '#8a94a7'; g.fillText('Brak danych (gra jeszcze się nie zaczęła)', 12, 24); return; }
  const pad = 16, spanX = Math.max(1, b.x1 - b.x0), spanZ = Math.max(1, b.z1 - b.z0), scale = Math.min((width - pad * 2) / spanX, (height - pad * 2) / spanZ);
  const ox = (width - spanX * scale) / 2, oz = (height - spanZ * scale) / 2;
  const X = (x) => ox + (x - b.x0) * scale, Y = (z) => height - (oz + (z - b.z0) * scale);
  const color = (team) => (team === 0 ? '#4aa3ff' : team === 1 ? '#ff6b6b' : '#aaaaaa');
  for (const [x, z, team] of map.minions) { g.fillStyle = color(team); g.globalAlpha = .75; g.fillRect(X(x) - 1.5, Y(z) - 1.5, 3, 3); }
  g.globalAlpha = 1;
  for (const [x, z, team, hp, type] of map.structures) {
    g.fillStyle = color(team); g.strokeStyle = '#0f1218'; const cx = X(x), cy = Y(z), r = type === 2 ? 9 : type === 1 ? 7 : 5;
    g.beginPath();
    if (type === 1) { g.moveTo(cx, cy - r); g.lineTo(cx + r, cy); g.lineTo(cx, cy + r); g.lineTo(cx - r, cy); g.closePath(); }
    else if (type === 2) { for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + (i * Math.PI) / 5, rr = i % 2 ? r * 0.45 : r; g.lineTo(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr); } g.closePath(); }
    else g.rect(cx - r, cy - r, r * 2, r * 2);
    g.globalAlpha = hp > 0 ? 1 : .3; g.fill(); g.stroke(); g.globalAlpha = 1;
  }
  for (const [x, z, team, hp, human] of map.champions) {
    g.beginPath(); g.arc(X(x), Y(z), human ? 6 : 5, 0, Math.PI * 2); g.fillStyle = color(team); g.fill();
    g.lineWidth = 2; g.strokeStyle = human ? '#fff' : '#0f1218'; g.stroke();
  }
}

function paintChat(box, entries, filter, showGame) {
  const stick = box.scrollTop + box.clientHeight >= box.scrollHeight - 30;
  const word = filter.toLowerCase();
  const lines = entries.filter((e) => !word || (e.player + ' ' + e.text + ' ' + e.title).toLowerCase().includes(word)).map((e) =>
    h('div', { class: 'line' + (e.system ? ' sys' : '') }, h('span', { class: 'at', title: e.day }, e.at),
      showGame ? h('span', { class: 'g', title: e.title }, e.match ? `#${e.match}` : 'lobby') : null,
      e.system ? null : h('span', { class: 'who' }, e.player + ':'), h('span', { class: 'tx' }, e.text)));
  box.replaceChildren(...lines);
  if (stick) box.scrollTop = box.scrollHeight;
  return lines.length;
}

async function pollChatInto(state, matchQuery) {
  const url = `/api/chat?after=${state.last}&limit=300${matchQuery}`;
  const result = await api(url);
  if (result.entries.length) { state.entries.push(...result.entries); state.last = result.entries[result.entries.length - 1].id; if (state.entries.length > 1500) state.entries.splice(0, state.entries.length - 1500); return true; }
  return false;
}
async function pollGameChat() {
  if (app.game == null || app.gameChat.match !== app.game) return;
  try { await pollChatInto(app.gameChat, `&match=${app.game}`); paintChat($('#gvChat') || document.createElement('div'), app.gameChat.entries, '', false); } catch (e) {  }
}
function fillChatFilter(data) {
  const select = $('#chatMatch'), current = select.value;
  const wanted = ['', 'lobby', ...data.matches.map((m) => String(m.id))];
  const have = [...select.options].map((o) => o.value);
  if (wanted.join() !== have.join()) {
    select.replaceChildren(h('option', { value: '' }, 'Wszystkie gry i lobby'), h('option', { value: '0' }, 'Lobby'), ...data.matches.map((m) => h('option', { value: String(m.id) }, `#${m.id} ${m.title}`)));
    select.value = [...select.options].some((o) => o.value === current) ? current : '';
  }
}
async function pollChat() {
  try { await pollChatInto(app.chat, ''); } catch (e) { return; }
  const match = $('#chatMatch').value;
  const entries = match === '' ? app.chat.entries : app.chat.entries.filter((e) => String(e.match) === match);
  const count = paintChat($('#chatBox'), entries, $('#chatFind').value, true);
  $('#chatCount').textContent = `${count} wiadomości (ostatnie 1500, tylko w pamięci serwera)`;
  if ($('#chatScroll').checked) $('#chatBox').scrollTop = $('#chatBox').scrollHeight;
}
$('#chatMatch').addEventListener('change', pollChat);
$('#chatFind').addEventListener('input', pollChat);

async function loadReports() {
  try {
    const status = $('#repStatus').value;
    const result = await api('/api/reports?limit=300' + (status ? '&status=' + status : ''));
    app.reports = result.reports;
    paintReports();
  } catch (e) {  }
}
function paintReports() {
  const kind = $('#repKind').value, word = $('#repFind').value.toLowerCase();
  const shown = app.reports.filter((r) => (!kind || r.kind === kind) && (!word || (r.title + ' ' + r.message + ' ' + r.player + ' ' + r.account).toLowerCase().includes(word)));
  const fresh = app.reports.filter((r) => r.status === 'new').length;
  const badge = $('#reportsBadge'); badge.textContent = fresh; badge.classList.toggle('hidden', !fresh); badge.classList.add('hot');
  $('#repCount').textContent = `${shown.length} z ${app.reports.length}`;
  $('#repList').replaceChildren(...(shown.length ? shown.map(reportCard) : [h('p', { class: 'empty' }, 'Brak zgłoszeń.')]));
}
function reportCard(r) {
  const select = h('select', { onchange: async (event) => { try { await api('/api/reports/status', { method: 'POST', body: JSON.stringify({ id: r.id, status: event.target.value }) }); r.status = event.target.value; paintReports(); } catch (e) { event.target.value = r.status; } } },
    Object.entries(STATUS).map(([value, label]) => h('option', { value, selected: value === r.status }, label)));
  return h('div', { class: 'rep ' + r.status },
    h('div', { class: 'h' }, h('span', { class: 'chip ' + r.kind }, KINDS[r.kind] || r.kind), h('span', { class: 'chip p' + r.priority }, ['', 'niski', 'normalny', 'wysoki'][r.priority] || r.priority), h('b', {}, r.title), h('span', { class: 'sp' }), h('span', { class: 'muted' }, '#' + r.id)),
    h('div', { class: 'meta' }, `${r.at} · ${r.player || '?'} (konto ${r.account || '?'})${r.context ? ' · ' + r.context : ''}`),
    h('div', { class: 'body' }, r.message),
    h('div', { class: 'act' }, h('span', { class: 'muted' }, 'Status:'), select));
}
$('#repStatus').addEventListener('change', loadReports);
$('#repKind').addEventListener('change', paintReports);
$('#repFind').addEventListener('input', paintReports);
$('#repReload').addEventListener('click', loadReports);
setInterval(() => { if (app.authed && app.tab === 'reports' && !document.hidden) loadReports(); }, 30000);

function renderPlayers(data) {
  const table = $('#playerTable');
  table.replaceChildren(h('tr', {}, h('th', {}, 'Id'), h('th', {}, 'Nick'), h('th', {}, 'Gdzie'), h('th', {}, 'Bohater'), h('th', {}, 'Zdrowie')),
    ...(data.players.length ? data.players.map((p) => h('tr', { class: p.dead ? 'dead' : '' }, h('td', { class: 'num' }, p.id), h('td', {}, p.name),
      h('td', {}, p.match ? h('a', { href: '#', onclick: (e) => { e.preventDefault(); app.game = p.match; app.gameChat = { last: 0, entries: [], match: p.match }; selectTab('games'); } }, '#' + p.match) : 'lobby'),
      h('td', {}, p.hero || '–'), h('td', {}, p.match ? `${p.hp}/${p.maxHp}` : '–'))) : [h('tr', {}, h('td', { colspan: 5, class: 'muted' }, 'Nikogo nie ma na serwerze.'))]));
}

(async function start() {
  try { app.data = await api('/api/state'); app.authed = true; $('#logout').classList.remove('hidden'); setConn(true); render(); loadReportsBadge(); }
  catch (error) { if (error.message !== 'login') setConn(false); }
})();
async function loadReportsBadge() { try { const r = await api('/api/reports?limit=300&status=new'); app.reports = r.reports; const b = $('#reportsBadge'); b.textContent = r.reports.length; b.classList.toggle('hidden', !r.reports.length); b.classList.add('hot'); } catch (e) {  } }
