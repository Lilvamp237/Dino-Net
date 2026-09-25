'use strict';
const $ = (s) => document.querySelector(s);
const app = $('#app');
let me = null;
let config = { levelNames: {}, aiAvailable: false };
const charts = [];

const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const pct = (x) => Math.round((x || 0) * 100) + '%';
function dur(sec) {
  sec = Math.round(sec || 0);
  if (sec < 60) return sec + ' s';
  const m = Math.round(sec / 60);
  if (m < 60) return m + ' min';
  return Math.floor(m / 60) + ' h ' + (m % 60) + ' min';
}
const when = (t) => new Date(t).toLocaleString([], { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
const ago = (t) => {
  const d = Date.now() - t;
  if (d < 3600e3) return Math.max(1, Math.round(d / 60e3)) + ' min ago';
  if (d < 86400e3) return Math.round(d / 3600e3) + ' h ago';
  return Math.round(d / 86400e3) + ' days ago';
};
const stars = (n) => `<span class="star">${'★'.repeat(n)}</span><span class="star off">${'★'.repeat(Math.max(0, 3 - n))}</span>`;
const levelName = (n) => (n === 0 ? 'Tutorial' : `Level ${n}: ${config.levelNames[n] || ''}`);

async function api(path, opts = {}) {
  const res = await fetch(path, { credentials: 'same-origin', headers: { 'content-type': 'application/json' }, ...opts });
  let json = null;
  try { json = await res.json(); } catch (_) { /* empty */ }
  if (res.status === 401 && path !== '/api/login') { me = null; renderLogin(); throw new Error('login'); }
  if (!res.ok) throw new Error((json && json.error) || 'Something went wrong');
  return json;
}

function killCharts() { while (charts.length) charts.pop().destroy(); }
function chart(id, cfg) {
  const el = document.getElementById(id);
  if (!el) return;
  cfg.options = { responsive: true, maintainAspectRatio: false, plugins: { legend: { display: cfg.legend === true } }, ...(cfg.options || {}) };
  charts.push(new Chart(el, cfg));
}

// ---------------------------------------------------------------- login
function renderLogin(msg) {
  killCharts();
  $('#top').hidden = true;
  app.innerHTML = `
    <div class="login">
      <div class="big">🦕</div>
      <h1>Dino Net Dashboard</h1>
      <p class="muted">See how children are learning how the internet works - safely and playfully.</p>
      <div class="grid">
        <form class="card" id="tForm">
          <h2>👩‍🏫 Teacher</h2>
          <small>See the whole class: play time, progress and what to practise.</small>
          <input type="password" id="pw" placeholder="Class password" autocomplete="current-password">
          <button>Open class dashboard</button>
        </form>
        <form class="card" id="pForm">
          <h2>👨‍👩‍👧 Parent</h2>
          <small>Enter the <b>family code</b> shown on the progress screen in the game.</small>
          <input class="code" id="code" maxlength="6" placeholder="ABC234" autocomplete="off">
          <button>See my child's progress</button>
        </form>
      </div>
      <p class="err" id="err">${esc(msg || '')}</p>
    </div>`;
  $('#tForm').onsubmit = (e) => { e.preventDefault(); login({ password: $('#pw').value }); };
  $('#pForm').onsubmit = (e) => { e.preventDefault(); login({ code: $('#code').value }); };
}
async function login(body) {
  try {
    await api('/api/login', { method: 'POST', body: JSON.stringify(body) });
    await boot();
  } catch (e) { if (e.message !== 'login') $('#err').textContent = e.message; }
}

// ---------------------------------------------------------------- shell
function shell() {
  $('#top').hidden = false;
  $('#roleLabel').textContent = me.role === 'teacher' ? 'Teacher dashboard' : 'Parent dashboard';
  const links = me.role === 'teacher'
    ? [['#/overview', 'Class overview'], ['#/children', 'Children']]
    : [[`#/child/${encodeURIComponent(me.deviceId)}`, 'My child']];
  const cur = location.hash || links[0][0];
  $('#nav').innerHTML = links.map(([h, t]) => `<a href="${h}" class="${cur.startsWith(h) ? 'on' : ''}">${t}</a>`).join('');
}
$('#logout').onclick = async () => { try { await api('/api/logout', { method: 'POST' }); } catch (_) { /* ignore */ } me = null; location.hash = ''; renderLogin(); };

async function route() {
  if (!me) return;
  killCharts();
  shell();
  const h = location.hash || (me.role === 'teacher' ? '#/overview' : `#/child/${encodeURIComponent(me.deviceId)}`);
  try {
    if (h.startsWith('#/child/')) return await viewChild(decodeURIComponent(h.slice(8)));
    if (me.role !== 'teacher') return (location.hash = `#/child/${encodeURIComponent(me.deviceId)}`);
    if (h.startsWith('#/children')) return await viewChildren();
    return await viewOverview();
  } catch (e) { if (e.message !== 'login') app.innerHTML = `<div class="card empty">${esc(e.message)}</div>`; }
}
window.addEventListener('hashchange', route);

// ---------------------------------------------------------------- teacher: overview
async function viewOverview() {
  const o = await api('/api/overview');
  if (!o.players) {
    app.innerHTML = `<div class="card empty"><h2>No children have played yet</h2>
      <p>Once the game is running it sends sessions here automatically.<br>To see how the dashboard looks, load some pretend children.</p>
      <button id="seed">Load demo children</button></div>`;
    $('#seed').onclick = async () => { await api('/api/demo/seed', { method: 'POST' }); route(); };
    return;
  }
  app.innerHTML = `
    <div class="head"><div><h1>Class overview</h1><small>Everything the children have done in Dino Net</small></div>
      <div>${o.demoPlayers ? `<button class="danger" id="clear">Remove ${o.demoPlayers} demo children</button>` : `<button class="light" id="seed">Add demo children</button>`}</div></div>
    ${o.demoPlayers ? '<div class="notice">Some of the children below are <b>demo data</b> for showing the dashboard.</div>' : ''}
    <div class="grid kpis">
      <div class="card kpi g"><span>Children</span><b>${o.players}</b></div>
      <div class="card kpi t"><span>Time in game (total)</span><b>${dur(o.totalSeconds)}</b></div>
      <div class="card kpi y"><span>Average session</span><b>${dur(o.avgSessionSeconds)}</b></div>
      <div class="card kpi p"><span>Sessions played</span><b>${o.sessions}</b></div>
      <div class="card kpi g"><span>Levels completed</span><b>${o.levelsCompleted}</b></div>
      <div class="card kpi y"><span>Stars earned</span><b>${o.starsEarned}</b></div>
    </div>
    <div class="grid two">
      <div class="card"><h2>Time in game - last 14 days</h2><div class="chart"><canvas id="cAct"></canvas></div></div>
      <div class="card"><h2>How many children finish each level</h2><div class="chart"><canvas id="cLvl"></canvas></div></div>
    </div>
    <div class="card" style="margin-top:1rem"><h2>What to practise together</h2><small>Share of questions answered correctly on the first try (lowest first)</small>
      <div id="concepts" style="margin-top:.6rem"></div></div>
    <div class="card" style="margin-top:1rem"><h2>Level detail</h2>
      <table><thead><tr><th>Level</th><th>Children</th><th>Finished</th><th>Avg. stars</th><th>Avg. time</th><th>Timed out</th></tr></thead>
      <tbody>${o.levels.map((l) => `<tr><td>${esc(levelName(l.level))}</td><td>${l.players}</td><td>${pct(l.completionRate)}</td><td>${stars(Math.round(l.avgStars))}</td><td>${l.avgSeconds ? dur(l.avgSeconds) : '-'}</td><td>${l.fails}</td></tr>`).join('')}</tbody></table></div>`;
  const btn = $('#seed') || $('#clear');
  if (btn) btn.onclick = async () => { if (btn.id === 'seed') await api('/api/demo/seed', { method: 'POST' }); else await api('/api/demo', { method: 'DELETE' }); route(); };

  chart('cAct', {
    type: 'bar', legend: true,
    data: { labels: o.activity.map((d) => d.day.slice(5)), datasets: [
      { label: 'Minutes played', data: o.activity.map((d) => d.minutes), backgroundColor: '#2f9e6a', borderRadius: 6 },
      { label: 'Children active', data: o.activity.map((d) => d.players), type: 'line', borderColor: '#f2b230', backgroundColor: '#f2b230', yAxisID: 'y2', tension: .3 },
    ] },
    options: { scales: { y: { beginAtZero: true, title: { display: true, text: 'minutes' } }, y2: { position: 'right', beginAtZero: true, grid: { display: false }, ticks: { precision: 0 } } } },
  });
  chart('cLvl', {
    type: 'bar',
    data: { labels: o.levels.map((l) => (l.level === 0 ? 'Tut' : 'L' + l.level)), datasets: [{ data: o.levels.map((l) => Math.round(l.completionRate * 100)), backgroundColor: '#1aa7a1', borderRadius: 6 }] },
    options: { scales: { y: { beginAtZero: true, max: 100, ticks: { callback: (v) => v + '%' } } } },
  });
  $('#concepts').innerHTML = o.concepts.length ? o.concepts.map((c) => rowBar(c.concept, c.firstTryRate)).join('') : '<p class="muted">Concept answers will appear here.</p>';
}

function rowBar(label, v) {
  const cls = v >= 0.7 ? '' : v >= 0.45 ? 'warn' : 'bad';
  return `<div class="row-bar"><span>${esc(label)}</span><div class="bar ${cls}"><i style="width:${Math.round(v * 100)}%"></i></div><b>${pct(v)}</b></div>`;
}

// ---------------------------------------------------------------- teacher: children list
async function viewChildren() {
  const list = await api('/api/players');
  if (!list.length) { app.innerHTML = '<div class="card empty">No children yet. Go to Class overview to load demo children, or play a level.</div>'; return; }
  app.innerHTML = `<div class="head"><div><h1>Children</h1><small>Click a child for their full progress</small></div></div>
    <div class="card"><table><thead><tr><th>Child</th><th>Family code</th><th>Time in game</th><th>Sessions</th><th>Levels</th><th>Stars</th><th>Streak</th><th>Last played</th></tr></thead>
    <tbody>${list.map((p) => `<tr class="click" data-id="${esc(p.deviceId)}"><td><span class="dot" style="background:${esc(p.color)}"></span><b>${esc(p.nickname)}</b>${p.demo ? ' <span class="pill warn">demo</span>' : ''}</td>
      <td><span class="pill code">${esc(p.code)}</span></td><td>${dur(p.totalSeconds)}</td><td>${p.sessionCount}</td><td>${p.levelsCompleted}/10</td><td>${p.stars} ★</td><td>${p.streak} day${p.streak === 1 ? '' : 's'}</td><td>${ago(p.lastSeen)}</td></tr>`).join('')}</tbody></table></div>`;
  app.querySelectorAll('tr.click').forEach((tr) => { tr.onclick = () => { location.hash = '#/child/' + encodeURIComponent(tr.dataset.id); }; });
}

// ---------------------------------------------------------------- child detail (teacher + parent)
async function viewChild(id) {
  const p = await api('/api/players/' + encodeURIComponent(id));
  const ins = p.insights;
  const days = [];
  for (let i = 13; i >= 0; i--) { const d = new Date(Date.now() - i * 86400000).toISOString().slice(0, 10); days.push(d); }
  const allLevels = Array.from({ length: 10 }, (_, i) => i + 1);
  const byLevel = Object.fromEntries(p.levels.map((l) => [l.level, l]));

  app.innerHTML = `
    <div class="head"><div><h1><span class="dot" style="background:${esc(p.color)}"></span>${esc(p.nickname)}</h1>
      <small>Family code <span class="pill code">${esc(p.code)}</span> · last played ${ago(p.lastSeen)}</small></div>
      ${me.role === 'teacher' ? '<button class="light" onclick="location.hash=\'#/children\'">← All children</button>' : ''}</div>
    <div class="grid kpis">
      <div class="card kpi t"><span>Total time in game</span><b>${dur(p.totalSeconds)}</b></div>
      <div class="card kpi y"><span>Average session</span><b>${dur(p.avgSessionSeconds)}</b></div>
      <div class="card kpi p"><span>Sessions</span><b>${p.sessionCount}</b></div>
      <div class="card kpi g"><span>Levels finished</span><b>${p.levelsCompleted}/10</b></div>
      <div class="card kpi y"><span>Stars</span><b>${p.stars} ★</b></div>
      <div class="card kpi c"><span>Play streak</span><b>${p.streak} day${p.streak === 1 ? '' : 's'}</b></div>
    </div>
    <div class="card insight"><h2>💡 How ${esc(p.nickname)} is doing</h2>
      <p><b>${esc(ins.headline)}</b> ${esc(ins.playtime)}</p>
      <h3>Going well</h3><ul>${ins.strengths.map((s) => `<li>${esc(s)}</li>`).join('')}</ul>
      <h3>Practise together</h3><ul>${ins.focus.map((s) => `<li>${esc(s)}</li>`).join('')}</ul>
      ${ins.notes.length ? `<h3>Good to know</h3><ul>${ins.notes.map((s) => `<li>${esc(s)}</li>`).join('')}</ul>` : ''}
      <p>➡️ ${esc(ins.nextStep)}</p>
      <div id="aiBox"></div>
      <button class="light" id="aiBtn">✨ Write a friendly summary</button>
    </div>
    ${p.badges.length ? `<div class="card" style="margin-top:1rem"><h2>Badges</h2><div class="badges">${p.badges.map((b) => `<span class="badge">🏅 ${esc(b.name)}</span>`).join('')}</div></div>` : ''}
    <div class="grid two" style="margin-top:1rem">
      <div class="card"><h2>Time in game - last 14 days</h2><div class="chart"><canvas id="cDay"></canvas></div></div>
      <div class="card"><h2>Understanding of each idea</h2><small>Right on the first try</small><div id="concepts" style="margin-top:.5rem"></div></div>
    </div>
    <div class="card" style="margin-top:1rem"><h2>Levels</h2>
      <table><thead><tr><th>Level</th><th>Stars</th><th>Tries</th><th>Best time</th><th>Mistakes</th></tr></thead>
      <tbody>${allLevels.map((n) => { const l = byLevel[n]; return `<tr><td>${esc(levelName(n))}</td><td>${l && l.completes ? stars(l.bestStars) : '<span class="muted">not finished</span>'}</td><td>${l ? l.starts : 0}</td><td>${l && l.bestTime ? dur(l.bestTime) : '-'}</td><td>${l ? l.mistakes : '-'}</td></tr>`; }).join('')}</tbody></table></div>
    <div class="grid two" style="margin-top:1rem">
      <div class="card"><h2>Safety and habits</h2>
        <div class="row-bar"><span>Time near the volcano (unsafe zone)</span><span></span><b>${dur(p.dangerSeconds)}</b></div>
        <div class="row-bar"><span>Visits to the wrong dinosaur</span><span></span><b>${p.wrongNodes}</b></div>
        <div class="row-bar"><span>Average time to connect a dino</span><span></span><b>${p.avgConnectSeconds ? dur(p.avgConnectSeconds) : '-'}</b></div>
        <div class="row-bar"><span>Route efficiency</span><div class="bar"><i style="width:${Math.round((p.avgEfficiency || 0) * 100)}%"></i></div><b>${p.avgEfficiency === null ? '-' : pct(p.avgEfficiency)}</b></div>
        <div class="row-bar"><span>Sandbox: messages sent</span><span></span><b>${p.sandbox.sends}</b></div>
        <div class="row-bar"><span>Sandbox: problems fixed</span><span></span><b>${Object.values(p.sandbox.fixes).reduce((a, b) => a + b, 0)}</b></div></div>
      <div class="card"><h2>Recent sessions</h2>
        <table><thead><tr><th>When</th><th>Length</th><th>Device</th><th>Levels</th></tr></thead>
        <tbody>${p.sessions.slice(0, 8).map((s) => `<tr><td>${when(s.start)}</td><td>${dur(s.activeSeconds)}</td><td>${esc(s.platform || '-')}</td><td>${s.levelsPlayed.length ? s.levelsPlayed.map((l) => (l === 0 ? 'T' : l)).join(', ') : '-'}</td></tr>`).join('')}</tbody></table></div>
    </div>`;

  chart('cDay', {
    type: 'bar',
    data: { labels: days.map((d) => d.slice(5)), datasets: [{ label: 'Minutes', data: days.map((d) => Math.round(((p.daily[d] || {}).seconds || 0) / 60)), backgroundColor: '#1aa7a1', borderRadius: 6 }] },
    options: { scales: { y: { beginAtZero: true, title: { display: true, text: 'minutes' } } } },
  });
  $('#concepts').innerHTML = p.concepts.length ? p.concepts.slice().sort((a, b) => a.mastery - b.mastery).map((c) => rowBar(c.concept, c.mastery)).join('') : '<p class="muted">Answers will appear after the first level.</p>';

  $('#aiBtn').onclick = async () => {
    const btn = $('#aiBtn'); btn.disabled = true; btn.textContent = 'Writing…';
    try {
      const r = await api(`/api/players/${encodeURIComponent(id)}/insights?ai=1`);
      if (r.aiText) $('#aiBox').innerHTML = `<div class="ai">${esc(r.aiText)}</div><small>Written by AI from anonymised numbers only.</small>`;
      else $('#aiBox').innerHTML = `<p><small>${esc(r.aiError || 'AI summaries are switched off on this server, so the summary above is the standard one. An administrator can turn them on by adding an API key.')}</small></p>`;
    } catch (e) { $('#aiBox').innerHTML = `<small>${esc(e.message)}</small>`; }
    btn.disabled = false; btn.textContent = '✨ Write a friendly summary';
  };
}

// ---------------------------------------------------------------- boot
async function boot() {
  try { config = await api('/api/config'); } catch (_) { /* keep defaults */ }
  try { me = await api('/api/me'); } catch (_) { me = null; }
  if (!me) return renderLogin();
  if (!location.hash) location.hash = me.role === 'teacher' ? '#/overview' : `#/child/${encodeURIComponent(me.deviceId)}`;
  route();
}
boot();
