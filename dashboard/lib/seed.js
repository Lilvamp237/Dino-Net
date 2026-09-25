'use strict';
// Realistic pretend children, so the dashboard can be shown before enough real play-tests exist.
// Every seeded device id starts with "demo-" so it can be cleared without touching real data.
const store = require('./store');

function rng(seed) {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const LEVEL_CONCEPTS = {
  1: ['HTTP vs HTTPS'],
  2: ['GET vs POST', 'GET vs POST'],
  3: ['Safe data vs private data', 'Passwords are private'],
  4: ['HTTP vs HTTPS', 'GET vs POST', 'Passwords are private'],
  5: ['Packets in pieces', 'Signal strength'],
  6: ['Got it! replies', 'Backup roads'],
  7: ['Addresses', 'Phone book (DNS)'],
  8: ['Fake friends', 'Strong passwords'],
  9: ['Firewall', 'Secret codes'],
  10: ['HTTP vs HTTPS', 'Fake friends', 'Backup roads', 'Secret codes'],
};
const LEVEL_NODES = { 1: 2, 2: 3, 3: 4, 4: 5, 5: 4, 6: 5, 7: 4, 8: 5, 9: 5, 10: 7 };

const KIDS = [
  { skill: 0.9, sessions: 12, danger: 0.05, name: 'a' },
  { skill: 0.75, sessions: 9, danger: 0.1, name: 'b' },
  { skill: 0.55, sessions: 8, danger: 0.3, name: 'c' },
  { skill: 0.4, sessions: 6, danger: 0.45, name: 'd' },
  { skill: 0.8, sessions: 5, danger: 0.15, name: 'e' },
  { skill: 0.3, sessions: 4, danger: 0.5, name: 'f' },
  { skill: 0.65, sessions: 10, danger: 0.2, name: 'g' },
];

function seed() {
  const out = [];
  const now = Date.now();
  KIDS.forEach((kid, ki) => {
    const r = rng(1000 + ki * 77);
    const deviceId = `demo-${kid.name}${ki}-${Math.floor(r() * 1e6)}`;
    let nextLevel = 1;
    const completedLevels = new Set();
    for (let s = 0; s < kid.sessions; s++) {
      const dayOffset = Math.floor(((kid.sessions - 1 - s) / kid.sessions) * 13 + r() * 1.2);
      let t = now - dayOffset * 86400000 - Math.floor(r() * 6) * 3600000;
      const sessionId = `${deviceId}-s${s}`;
      const push = (type, level, data, dt) => { t += (dt || 1) * 1000; out.push({ t, deviceId, sessionId, type, level, data: data || {} }); };
      let active = 0;
      push('session_start', 0, { platform: r() > 0.4 ? 'Quest' : 'Windows PC', version: '1.0' });
      push('scene_enter', 0, { scene: 'MainMenu' }, 2);
      active += 20;
      const levelsThisSession = 1 + Math.floor(r() * 2.4);
      for (let li = 0; li < levelsThisSession; li++) {
        const level = Math.min(10, nextLevel);
        push('scene_enter', level, { scene: 'Level' + level }, 8);
        push('level_start', level, {}, 2);
        const nodes = LEVEL_NODES[level];
        let mistakes = 0;
        let levelSeconds = 0;
        let danger = 0;
        for (let n = 0; n < nodes; n++) {
          const secs = Math.round(18 + r() * 45 + (1 - kid.skill) * 30);
          if (r() < (1 - kid.skill) * 0.7) { push('wrong_node', level, { node: 'decoy' }, 3); mistakes++; }
          levelSeconds += secs;
          push('node_connected', level, { index: n, seconds: secs }, secs);
          const cs = LEVEL_CONCEPTS[level];
          if (n < cs.length) {
            const concept = cs[n];
            let attempt = 1;
            while (attempt < 4) {
              const ok = r() < 0.35 + kid.skill * 0.6 + attempt * 0.12;
              push('lesson_answer', level, { concept, correct: ok, attempt }, 6);
              if (ok) break;
              mistakes++;
              attempt++;
            }
            if (attempt === 4) push('lesson_answer', level, { concept, correct: true, attempt: 4 }, 4);
          }
        }
        if (r() < kid.danger) {
          const secs = Math.round(6 + r() * 40);
          danger = secs;
          push('danger_enter', level, { zone: 'volcano' }, 1);
          push('danger_exit', level, { zone: 'volcano', seconds: secs }, secs);
          levelSeconds += secs;
        }
        const timeLimit = 300;
        const failed = levelSeconds + danger * 2 > timeLimit * (0.7 + kid.skill * 0.4) || r() < (1 - kid.skill) * 0.3 + (level >= 8 ? 0.08 : 0);
        active += levelSeconds + 30;
        push('heartbeat', level, { activeSeconds: Math.round(active), scene: 'Level' + level }, 1);
        if (failed) {
          push('level_fail', level, { reason: "Time's up!" }, 2);
        } else {
          const stars = mistakes === 0 && levelSeconds < timeLimit * 0.5 ? 3 : mistakes <= 2 ? 2 : 1;
          const efficiency = Math.max(0.35, Math.min(1, 0.55 + kid.skill * 0.45 - r() * 0.15));
          push('level_complete', level, { stars, seconds: levelSeconds, mistakes, timeLeft: Math.max(0, timeLimit - levelSeconds), efficiency: Math.round(efficiency * 100) / 100 }, 2);
          if (stars === 3 && !completedLevels.has(level)) push('badge_earned', level, { id: 'perfect_' + level, name: 'Perfect Level ' + level }, 1);
          completedLevels.add(level);
          if (level === nextLevel && nextLevel < 10) nextLevel++;
        }
      }
      if (r() > 0.7) { push('sandbox_build', 0, { nodes: 4 }, 30); push('sandbox_send', 0, {}, 10); active += 60; }
      active += 25;
      push('session_end', 0, { activeSeconds: Math.round(active) }, 5);
    }
  });
  return store.addMany(out);
}

function clear() { return store.removeWhere((e) => e.deviceId.startsWith('demo-')); }

module.exports = { seed, clear };
