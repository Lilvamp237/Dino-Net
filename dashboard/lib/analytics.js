'use strict';
// Turns raw game events into the numbers teachers and parents care about.
const crypto = require('crypto');
const store = require('./store');

const ADJECTIVES = ['Brave', 'Speedy', 'Sunny', 'Clever', 'Happy', 'Mighty', 'Gentle', 'Curious', 'Jolly', 'Zippy', 'Cozy', 'Bold'];
const DINOS = ['Raptor', 'Stego', 'Trike', 'Ptero', 'Rex', 'Brachio', 'Anky', 'Para', 'Diplo', 'Spino'];
const CODE_CHARS = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';

const LEVEL_NAMES = {
  0: 'Tutorial',
  1: 'Safe Connections',
  2: 'Ask or Send',
  3: 'Keep Secrets',
  4: 'Smart & Safe Routing',
  5: 'Pieces & Signal',
  6: 'Got It!',
  7: 'Find the Home',
  8: 'Fake Friends',
  9: 'Gatekeepers & Secret Codes',
  10: 'Grand Challenge',
};

function hash(id) { return crypto.createHash('sha1').update(String(id)).digest(); }

function identity(deviceId) {
  const h = hash(deviceId);
  let code = '';
  for (let i = 0; i < 6; i++) code += CODE_CHARS[h[i + 2] % CODE_CHARS.length];
  return {
    code,
    nickname: `${ADJECTIVES[h[0] % ADJECTIVES.length]} ${DINOS[h[1] % DINOS.length]}`,
    color: '#' + h.subarray(8, 11).toString('hex'),
  };
}

const DAY = 86400000;
const dayKey = (t) => new Date(t).toISOString().slice(0, 10);

let cache = { v: -1, players: null };

function build() {
  if (cache.v === store.version()) return cache.players;
  const byDevice = new Map();
  for (const e of store.all()) {
    if (!byDevice.has(e.deviceId)) byDevice.set(e.deviceId, []);
    byDevice.get(e.deviceId).push(e);
  }
  const players = new Map();
  for (const [deviceId, list] of byDevice) players.set(deviceId, buildPlayer(deviceId, list));
  cache = { v: store.version(), players };
  return players;
}

function buildPlayer(deviceId, list) {
  list.sort((a, b) => a.t - b.t);
  const who = identity(deviceId);
  const sessions = new Map();
  const levels = {};
  const concepts = {};
  const badges = [];
  const daily = {};
  let dangerSeconds = 0;
  let wrongNodes = 0;
  const connectTimes = [];
  const efficiencies = [];
  let hints = 0;
  const sandbox = { sends: 0, fixes: {}, builds: 0 };
  let stars = 0;

  const levelOf = (n) => (levels[n] = levels[n] || { level: n, name: LEVEL_NAMES[n] || `Level ${n}`, starts: 0, completes: 0, fails: 0, bestStars: 0, bestTime: null, totalTime: 0, mistakes: 0, lastPlayed: 0 });

  for (const e of list) {
    let s = sessions.get(e.sessionId);
    if (!s) {
      s = { id: e.sessionId, start: e.t, end: e.t, activeSeconds: 0, platform: '', levelsPlayed: new Set(), events: 0 };
      sessions.set(e.sessionId, s);
    }
    s.end = Math.max(s.end, e.t);
    s.events++;
    const d = e.data || {};
    switch (e.type) {
      case 'session_start': s.platform = d.platform || s.platform; s.start = Math.min(s.start, e.t); break;
      case 'heartbeat':
      case 'session_end':
        if (Number(d.activeSeconds) > s.activeSeconds) s.activeSeconds = Number(d.activeSeconds);
        break;
      case 'level_start': {
        const l = levelOf(e.level); l.starts++; l.lastPlayed = e.t; s.levelsPlayed.add(e.level); break;
      }
      case 'level_complete': {
        const l = levelOf(e.level); l.completes++; l.lastPlayed = e.t;
        const secs = Number(d.seconds) || 0;
        l.totalTime += secs;
        if (l.bestTime === null || (secs > 0 && secs < l.bestTime)) l.bestTime = secs || l.bestTime;
        l.mistakes += Number(d.mistakes) || 0;
        if ((Number(d.stars) || 0) > l.bestStars) l.bestStars = Number(d.stars);
        if (Number.isFinite(Number(d.efficiency)) && d.efficiency !== null) efficiencies.push(Number(d.efficiency));
        break;
      }
      case 'level_fail': levelOf(e.level).fails++; break;
      case 'node_connected':
        if (Number.isFinite(Number(d.seconds))) connectTimes.push(Number(d.seconds));
        break;
      case 'wrong_node': wrongNodes++; break;
      case 'danger_exit': dangerSeconds += Number(d.seconds) || 0; break;
      case 'hint_used': hints++; break;
      case 'lesson_answer': {
        const term = d.concept || 'Unknown';
        const c = (concepts[term] = concepts[term] || { concept: term, attempts: 0, correct: 0, firstTry: 0, questions: 0 });
        c.attempts++;
        if (d.correct) { c.correct++; if (Number(d.attempt) === 1) c.firstTry++; c.questions++; }
        break;
      }
      case 'badge_earned': badges.push({ id: d.id, name: d.name || d.id, t: e.t }); break;
      case 'sandbox_send': sandbox.sends++; break;
      case 'sandbox_fix': sandbox.fixes[d.fix] = (sandbox.fixes[d.fix] || 0) + 1; break;
      case 'sandbox_build': sandbox.builds++; break;
      default: break;
    }
  }

  for (const l of Object.values(levels)) stars += l.bestStars;

  const sessionList = [...sessions.values()].map((s) => ({
    id: s.id,
    start: s.start,
    end: s.end,
    platform: s.platform,
    activeSeconds: s.activeSeconds || Math.max(0, Math.round((s.end - s.start) / 1000)),
    levelsPlayed: [...s.levelsPlayed].sort((a, b) => a - b),
  })).sort((a, b) => b.start - a.start);

  let totalSeconds = 0;
  for (const s of sessionList) {
    totalSeconds += s.activeSeconds;
    const k = dayKey(s.start);
    daily[k] = daily[k] || { sessions: 0, seconds: 0 };
    daily[k].sessions++;
    daily[k].seconds += s.activeSeconds;
  }

  // Streak: consecutive days with play, counting back from the most recent day played.
  const days = Object.keys(daily).sort();
  let streak = 0;
  if (days.length) {
    let cursor = new Date(days[days.length - 1] + 'T00:00:00Z').getTime();
    const set = new Set(days);
    while (set.has(dayKey(cursor))) { streak++; cursor -= DAY; }
  }

  const avg = (a) => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : null);
  const conceptList = Object.values(concepts).map((c) => ({
    ...c,
    accuracy: c.attempts ? c.correct / c.attempts : 0,
    mastery: c.questions ? c.firstTry / c.questions : 0,
  }));

  return {
    deviceId,
    ...who,
    firstSeen: list[0].t,
    lastSeen: list[list.length - 1].t,
    sessionCount: sessionList.length,
    totalSeconds,
    avgSessionSeconds: sessionList.length ? totalSeconds / sessionList.length : 0,
    stars,
    levelsCompleted: Object.values(levels).filter((l) => l.completes > 0 && l.level > 0).length,
    levels: Object.values(levels).sort((a, b) => a.level - b.level),
    concepts: conceptList,
    badges,
    dangerSeconds: Math.round(dangerSeconds),
    wrongNodes,
    hints,
    avgConnectSeconds: avg(connectTimes),
    avgEfficiency: avg(efficiencies),
    streak,
    daily,
    sandbox,
    sessions: sessionList,
    demo: deviceId.startsWith('demo-'),
  };
}

function players() { return build(); }
function player(deviceId) { return build().get(deviceId) || null; }
function playerByCode(code) {
  const c = String(code || '').trim().toUpperCase();
  for (const p of build().values()) if (p.code === c) return p;
  return null;
}

function overview() {
  const list = [...build().values()];
  const now = Date.now();
  const days = [];
  for (let i = 13; i >= 0; i--) days.push(dayKey(now - i * DAY));
  const activity = days.map((d) => ({
    day: d,
    sessions: list.reduce((n, p) => n + (p.daily[d] ? p.daily[d].sessions : 0), 0),
    minutes: Math.round(list.reduce((n, p) => n + (p.daily[d] ? p.daily[d].seconds : 0), 0) / 60),
    players: list.filter((p) => p.daily[d]).length,
  }));

  const levelStats = {};
  const conceptStats = {};
  for (const p of list) {
    for (const l of p.levels) {
      const s = (levelStats[l.level] = levelStats[l.level] || { level: l.level, name: l.name, players: 0, completed: 0, starts: 0, completes: 0, fails: 0, avgTimeSum: 0, timeN: 0, starsSum: 0 });
      s.players++;
      if (l.completes > 0) { s.completed++; s.starsSum += l.bestStars; }
      s.starts += l.starts; s.completes += l.completes; s.fails += l.fails;
      if (l.completes > 0) { s.avgTimeSum += l.totalTime / l.completes; s.timeN++; }
    }
    for (const c of p.concepts) {
      const s = (conceptStats[c.concept] = conceptStats[c.concept] || { concept: c.concept, attempts: 0, correct: 0, firstTry: 0, questions: 0 });
      s.attempts += c.attempts; s.correct += c.correct; s.firstTry += c.firstTry; s.questions += c.questions;
    }
  }

  const totalSeconds = list.reduce((n, p) => n + p.totalSeconds, 0);
  const sessions = list.reduce((n, p) => n + p.sessionCount, 0);
  return {
    players: list.length,
    sessions,
    totalSeconds,
    avgSessionSeconds: sessions ? totalSeconds / sessions : 0,
    activeThisWeek: list.filter((p) => now - p.lastSeen < 7 * DAY).length,
    levelsCompleted: list.reduce((n, p) => n + p.levelsCompleted, 0),
    starsEarned: list.reduce((n, p) => n + p.stars, 0),
    activity,
    levels: Object.values(levelStats).sort((a, b) => a.level - b.level).map((s) => ({
      level: s.level, name: s.name, players: s.players, completed: s.completed,
      completionRate: s.players ? s.completed / s.players : 0,
      avgSeconds: s.timeN ? s.avgTimeSum / s.timeN : null,
      avgStars: s.completed ? s.starsSum / s.completed : 0,
      fails: s.fails,
    })),
    concepts: Object.values(conceptStats).map((s) => ({
      concept: s.concept, attempts: s.attempts,
      accuracy: s.attempts ? s.correct / s.attempts : 0,
      firstTryRate: s.questions ? s.firstTry / s.questions : 0,
    })).sort((a, b) => a.firstTryRate - b.firstTryRate),
    demoPlayers: list.filter((p) => p.demo).length,
  };
}

function summary(p) {
  return {
    deviceId: p.deviceId, code: p.code, nickname: p.nickname, color: p.color,
    sessionCount: p.sessionCount, totalSeconds: p.totalSeconds, avgSessionSeconds: p.avgSessionSeconds,
    lastSeen: p.lastSeen, stars: p.stars, levelsCompleted: p.levelsCompleted, streak: p.streak, demo: p.demo,
    badges: p.badges.length,
  };
}

module.exports = { players, player, playerByCode, overview, summary, identity, LEVEL_NAMES };
