'use strict';
// Dino Net teacher & parent dashboard - a tiny dependency-free Node server.
//   npm start                       (or: node server.js)
// Environment:
//   PORT                       default 3000
//   DINONET_API_KEY            key the game sends (default "dino-demo-key")
//   DASH_TEACHER_PASSWORD      teacher login (default "teacher")
//   DATA_DIR                   where events.jsonl lives (default ../data)
//   ANTHROPIC_API_KEY          optional - enables AI-written summaries
const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { URL } = require('url');

const store = require('./lib/store');
const analytics = require('./lib/analytics');
const insights = require('./lib/insights');
const seed = require('./lib/seed');

const PORT = Number(process.env.PORT) || 3000;
const API_KEY = process.env.DINONET_API_KEY || 'dino-demo-key';
const TEACHER_PASSWORD = process.env.DASH_TEACHER_PASSWORD || 'teacher';
const SECRET = process.env.DASH_SECRET || crypto.randomBytes(24).toString('hex');
const PUBLIC = path.join(__dirname, 'public');

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon', '.md': 'text/plain; charset=utf-8',
};

function send(res, status, body, headers = {}) {
  const isObj = body !== null && typeof body === 'object' && !Buffer.isBuffer(body);
  res.writeHead(status, { 'content-type': isObj ? 'application/json' : 'text/plain; charset=utf-8', 'cache-control': 'no-store', ...headers });
  res.end(isObj ? JSON.stringify(body) : body);
}

function readBody(req, limit = 2 * 1024 * 1024) {
  return new Promise((resolve, reject) => {
    let size = 0; const chunks = [];
    req.on('data', (c) => { size += c.length; if (size > limit) { reject(new Error('too large')); req.destroy(); } else chunks.push(c); });
    req.on('end', () => { try { resolve(chunks.length ? JSON.parse(Buffer.concat(chunks).toString('utf8')) : {}); } catch (e) { reject(new Error('bad json')); } });
    req.on('error', reject);
  });
}

// ---- auth: a signed cookie carrying the role (teacher, or parent bound to one child) ----
function sign(payload) {
  const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const mac = crypto.createHmac('sha256', SECRET).update(body).digest('base64url');
  return `${body}.${mac}`;
}
function verify(token) {
  if (!token || !token.includes('.')) return null;
  const [body, mac] = token.split('.');
  const good = crypto.createHmac('sha256', SECRET).update(body).digest('base64url');
  if (mac.length !== good.length || !crypto.timingSafeEqual(Buffer.from(mac), Buffer.from(good))) return null;
  try {
    const p = JSON.parse(Buffer.from(body, 'base64url').toString('utf8'));
    return p.exp > Date.now() ? p : null;
  } catch (_) { return null; }
}
function session(req) {
  const cookie = req.headers.cookie || '';
  const m = /(?:^|;\s*)dn_auth=([^;]+)/.exec(cookie);
  return m ? verify(decodeURIComponent(m[1])) : null;
}
const cookie = (value, maxAge) => `dn_auth=${encodeURIComponent(value)}; Path=/; HttpOnly; SameSite=Lax; Max-Age=${maxAge}`;

// Small brake on password guessing.
const attempts = new Map();
function throttled(ip) {
  const now = Date.now();
  const rec = attempts.get(ip) || { n: 0, t: now };
  if (now - rec.t > 60000) { rec.n = 0; rec.t = now; }
  rec.n++; attempts.set(ip, rec);
  return rec.n > 12;
}

function playerView(p) {
  return {
    ...analytics.summary(p),
    firstSeen: p.firstSeen,
    avgSessionSeconds: p.avgSessionSeconds,
    levels: p.levels, concepts: p.concepts, badges: p.badges,
    dangerSeconds: p.dangerSeconds, wrongNodes: p.wrongNodes, hints: p.hints,
    avgConnectSeconds: p.avgConnectSeconds, avgEfficiency: p.avgEfficiency,
    sandbox: p.sandbox, sessions: p.sessions.slice(0, 40),
    daily: p.daily,
  };
}

async function api(req, res, url) {
  const route = url.pathname;
  const method = req.method;
  const ip = req.socket.remoteAddress || '';

  if (route === '/api/health') return send(res, 200, { ok: true, events: store.all().length });

  // Ingest from the game: key-protected, no login.
  if (route === '/api/ingest' && method === 'POST') {
    const key = req.headers['x-api-key'] || url.searchParams.get('key');
    if (key !== API_KEY) return send(res, 401, { error: 'bad api key' });
    let body;
    try { body = await readBody(req); } catch (e) { return send(res, 400, { error: e.message }); }
    const accepted = store.add(body);
    return send(res, 200, { accepted });
  }

  if (route === '/api/config') {
    return send(res, 200, { aiAvailable: !!process.env.ANTHROPIC_API_KEY, levelNames: analytics.LEVEL_NAMES });
  }

  if (route === '/api/login' && method === 'POST') {
    if (throttled(ip)) return send(res, 429, { error: 'Too many tries. Please wait a minute.' });
    let body;
    try { body = await readBody(req, 10000); } catch (e) { return send(res, 400, { error: e.message }); }
    if (body.password !== undefined) {
      const ok = String(body.password).length === TEACHER_PASSWORD.length && crypto.timingSafeEqual(Buffer.from(String(body.password)), Buffer.from(TEACHER_PASSWORD));
      if (!ok) return send(res, 401, { error: 'That password is not right.' });
      return send(res, 200, { role: 'teacher' }, { 'set-cookie': cookie(sign({ role: 'teacher', exp: Date.now() + 12 * 3600000 }), 43200) });
    }
    if (body.code !== undefined) {
      const p = analytics.playerByCode(body.code);
      if (!p) return send(res, 404, { error: 'We could not find that family code. Check the code shown in the game.' });
      return send(res, 200, { role: 'parent', deviceId: p.deviceId }, { 'set-cookie': cookie(sign({ role: 'parent', deviceId: p.deviceId, exp: Date.now() + 12 * 3600000 }), 43200) });
    }
    return send(res, 400, { error: 'password or code required' });
  }

  if (route === '/api/logout' && method === 'POST') return send(res, 200, { ok: true }, { 'set-cookie': cookie('', 0) });

  const who = session(req);
  if (!who) return send(res, 401, { error: 'login required' });
  if (route === '/api/me') return send(res, 200, { role: who.role, deviceId: who.deviceId || null });

  const canSee = (deviceId) => who.role === 'teacher' || who.deviceId === deviceId;

  if (route === '/api/overview') {
    if (who.role !== 'teacher') return send(res, 403, { error: 'teachers only' });
    return send(res, 200, analytics.overview());
  }
  if (route === '/api/players') {
    if (who.role !== 'teacher') return send(res, 403, { error: 'teachers only' });
    const list = [...analytics.players().values()].map(analytics.summary).sort((a, b) => b.lastSeen - a.lastSeen);
    return send(res, 200, list);
  }

  let m = /^\/api\/players\/([^/]+)$/.exec(route);
  if (m) {
    const id = decodeURIComponent(m[1]);
    if (!canSee(id)) return send(res, 403, { error: 'not allowed' });
    const p = analytics.player(id);
    if (!p) return send(res, 404, { error: 'unknown player' });
    return send(res, 200, { ...playerView(p), insights: insights.ruleInsights(p) });
  }
  m = /^\/api\/players\/([^/]+)\/insights$/.exec(route);
  if (m) {
    const id = decodeURIComponent(m[1]);
    if (!canSee(id)) return send(res, 403, { error: 'not allowed' });
    const p = analytics.player(id);
    if (!p) return send(res, 404, { error: 'unknown player' });
    return send(res, 200, url.searchParams.get('ai') === '1' ? await insights.aiInsights(p) : insights.ruleInsights(p));
  }

  if (route === '/api/demo/seed' && method === 'POST') {
    if (who.role !== 'teacher') return send(res, 403, { error: 'teachers only' });
    seed.clear();
    return send(res, 200, { added: seed.seed() });
  }
  if (route === '/api/demo' && method === 'DELETE') {
    if (who.role !== 'teacher') return send(res, 403, { error: 'teachers only' });
    return send(res, 200, { removed: seed.clear() });
  }

  return send(res, 404, { error: 'not found' });
}

function serveStatic(req, res, url) {
  let rel = decodeURIComponent(url.pathname);
  if (rel === '/' || rel === '') rel = '/index.html';
  const file = path.normalize(path.join(PUBLIC, rel));
  if (!file.startsWith(PUBLIC)) return send(res, 403, 'forbidden');
  fs.readFile(file, (err, data) => {
    if (err) return send(res, 404, 'not found');
    res.writeHead(200, { 'content-type': MIME[path.extname(file)] || 'application/octet-stream', 'cache-control': 'no-cache' });
    res.end(data);
  });
}

store.load();

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname.startsWith('/api/')) return await api(req, res, url);
    return serveStatic(req, res, url);
  } catch (err) {
    console.error(err);
    send(res, 500, { error: 'server error' });
  }
});

server.listen(PORT, '0.0.0.0', () => {
  console.log(`Dino Net dashboard running on http://localhost:${PORT}`);
  console.log(`  teacher password : ${TEACHER_PASSWORD === 'teacher' ? '"teacher" (default - change with DASH_TEACHER_PASSWORD)' : '(set)'}`);
  console.log(`  game api key     : ${API_KEY === 'dino-demo-key' ? '"dino-demo-key" (default - change with DINONET_API_KEY)' : '(set)'}`);
  console.log(`  AI summaries     : ${process.env.ANTHROPIC_API_KEY ? 'on' : 'off (set ANTHROPIC_API_KEY to enable)'}`);
});
