'use strict';
// End-to-end check: start the server on a temp folder, send game-style events, read them back.
const { spawn } = require('child_process');
const path = require('path');
const os = require('os');
const fs = require('fs');
const assert = require('assert');

const PORT = 3900 + Math.floor(Math.random() * 90);
const DATA_DIR = fs.mkdtempSync(path.join(os.tmpdir(), 'dinonet-'));
const base = `http://localhost:${PORT}`;
const KEY = 'test-key';

async function call(method, url, body, cookie) {
  const res = await fetch(base + url, {
    method,
    headers: { 'content-type': 'application/json', ...(cookie ? { cookie } : {}), ...(url === '/api/ingest' ? { 'x-api-key': KEY } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null; try { json = JSON.parse(text); } catch (_) { /* not json */ }
  return { status: res.status, json, headers: res.headers };
}

(async () => {
  const child = spawn(process.execPath, [path.join(__dirname, '..', 'server.js')], {
    env: { ...process.env, PORT: String(PORT), DATA_DIR, DINONET_API_KEY: KEY, DASH_TEACHER_PASSWORD: 'pw', ANTHROPIC_API_KEY: '' },
    stdio: 'ignore',
  });
  try {
    for (let i = 0; i < 40; i++) { try { const h = await call('GET', '/api/health'); if (h.status === 200) break; } catch (_) { await new Promise((r) => setTimeout(r, 100)); } }

    const now = Date.now();
    const ev = (type, level, data, dt) => ({ t: now + dt * 1000, type, level, data });
    const batch = {
      deviceId: 'test-device-1', sessionId: 's1',
      events: [
        ev('session_start', 0, { platform: 'Quest' }, 0),
        ev('level_start', 1, {}, 5),
        ev('lesson_answer', 1, { concept: 'HTTP vs HTTPS', correct: false, attempt: 1 }, 10),
        ev('lesson_answer', 1, { concept: 'HTTP vs HTTPS', correct: true, attempt: 2 }, 14),
        ev('node_connected', 1, { index: 0, seconds: 20 }, 30),
        ev('level_complete', 1, { stars: 2, seconds: 60, mistakes: 1, efficiency: 0.9 }, 60),
        ev('heartbeat', 1, { activeSeconds: 90 }, 90),
        ev('session_end', 0, { activeSeconds: 120 }, 120),
      ],
    };

    let r = await call('POST', '/api/ingest', batch);
    assert.strictEqual(r.status, 200); assert.strictEqual(r.json.accepted, 8);

    const bad = await fetch(base + '/api/ingest', { method: 'POST', headers: { 'content-type': 'application/json', 'x-api-key': 'nope' }, body: '{}' });
    assert.strictEqual(bad.status, 401, 'wrong key must be rejected');

    r = await call('GET', '/api/overview');
    assert.strictEqual(r.status, 401, 'dashboard data needs login');

    r = await call('POST', '/api/login', { password: 'wrong' });
    assert.strictEqual(r.status, 401);
    r = await call('POST', '/api/login', { password: 'pw' });
    assert.strictEqual(r.status, 200);
    const teacher = r.headers.get('set-cookie').split(';')[0];

    r = await call('GET', '/api/overview', null, teacher);
    assert.strictEqual(r.json.players, 1);
    assert.strictEqual(r.json.totalSeconds, 120, 'time in game comes from session_end');

    r = await call('GET', '/api/players', null, teacher);
    assert.strictEqual(r.json.length, 1);
    const p = r.json[0];
    assert.ok(/^[A-Z2-9]{6}$/.test(p.code), 'family code shape');

    r = await call('GET', '/api/players/test-device-1', null, teacher);
    assert.strictEqual(r.json.stars, 2);
    assert.strictEqual(r.json.levelsCompleted, 1);
    const c = r.json.concepts.find((x) => x.concept === 'HTTP vs HTTPS');
    assert.strictEqual(c.attempts, 2); assert.strictEqual(c.firstTry, 0, 'needed two tries');

    // Parent: family code only, and only their own child.
    r = await call('POST', '/api/login', { code: 'ZZZZZZ' });
    assert.strictEqual(r.status, 404);
    r = await call('POST', '/api/login', { code: p.code.toLowerCase() });
    assert.strictEqual(r.status, 200);
    const parent = r.headers.get('set-cookie').split(';')[0];
    r = await call('GET', '/api/players/test-device-1', null, parent);
    assert.strictEqual(r.status, 200);
    r = await call('GET', '/api/overview', null, parent);
    assert.strictEqual(r.status, 403, 'parents cannot see the class');
    r = await call('GET', '/api/players/someone-else', null, parent);
    assert.strictEqual(r.status, 403);

    // Rule-based insights always work; AI is off without a key.
    r = await call('GET', '/api/players/test-device-1/insights?ai=1', null, teacher);
    assert.strictEqual(r.json.source, 'rules'); assert.strictEqual(r.json.aiAvailable, false);

    // Demo data seeds and clears without touching real data.
    r = await call('POST', '/api/demo/seed', null, teacher);
    assert.ok(r.json.added > 500, 'seed adds events');
    r = await call('GET', '/api/overview', null, teacher);
    assert.ok(r.json.players >= 8);
    assert.ok(r.json.levels.length > 0 && r.json.concepts.length > 0 && r.json.activity.length === 14);
    r = await call('DELETE', '/api/demo', null, teacher);
    r = await call('GET', '/api/overview', null, teacher);
    assert.strictEqual(r.json.players, 1, 'only the real player remains');

    console.log('dashboard smoke test: all checks passed');
  } catch (err) {
    console.error('FAILED:', err.message);
    process.exitCode = 1;
  } finally {
    child.kill();
    fs.rmSync(DATA_DIR, { recursive: true, force: true });
  }
})();
