'use strict';
// Plain-language summaries for parents and teachers.
//  1. Rule-based (always available, works offline, no data leaves this machine).
//  2. Optional AI rewrite: only used when ANTHROPIC_API_KEY is set. It receives anonymised
//     numbers only - never the child's nickname, family code or device id.
const { LEVEL_NAMES } = require('./analytics');

const CONCEPT_LEVEL = {
  'HTTP vs HTTPS': 1,
  'GET vs POST': 2,
  'Safe data vs private data': 3,
  'Passwords are private': 3,
  'Packets in pieces': 5,
  'Signal strength': 5,
  'Got it! replies': 6,
  'Backup roads': 6,
  'Addresses': 7,
  'Phone book (DNS)': 7,
  'Fake friends': 8,
  'Strong passwords': 8,
  'Firewall': 9,
  'Secret codes': 9,
};

const KID_LABEL = {
  'HTTP vs HTTPS': 'choosing the safe (HTTPS) connection',
  'GET vs POST': 'telling asking (GET) from sending (POST)',
  'Safe data vs private data': 'knowing what is safe to share',
  'Passwords are private': 'keeping passwords private',
  'Packets in pieces': 'how messages travel in pieces',
  'Signal strength': 'why signals get weaker with distance',
  'Got it! replies': 'why receivers say "got it!"',
  'Backup roads': 'using backup routes when a road is blocked',
  'Addresses': 'delivering to the right address',
  'Phone book (DNS)': 'looking up where a name lives',
  'Fake friends': 'spotting fake friends online',
  'Strong passwords': 'building strong passwords',
  'Firewall': 'firewalls that block sneaky visitors',
  'Secret codes': 'secret codes that protect messages',
};

const mins = (s) => Math.round(s / 60);

function ruleInsights(p) {
  const weekAgo = Date.now() - 7 * 86400000;
  const weekSeconds = p.sessions.filter((s) => s.start >= weekAgo).reduce((n, s) => n + s.activeSeconds, 0);

  const strong = p.concepts.filter((c) => c.questions >= 1 && c.mastery >= 0.75).sort((a, b) => b.mastery - a.mastery);
  const weak = p.concepts.filter((c) => c.attempts >= 1 && c.mastery < 0.6).sort((a, b) => a.mastery - b.mastery);

  const strengths = strong.slice(0, 3).map((c) => `Confident at ${KID_LABEL[c.concept] || c.concept.toLowerCase()} (${Math.round(c.mastery * 100)}% right first time).`);
  const focus = weak.slice(0, 3).map((c) => `Still learning ${KID_LABEL[c.concept] || c.concept.toLowerCase()} - it took ${(c.attempts / Math.max(1, c.questions)).toFixed(1)} tries on average.`);

  const done = new Set(p.levels.filter((l) => l.completes > 0).map((l) => l.level));
  let nextLevel = null;
  if (weak.length) nextLevel = CONCEPT_LEVEL[weak[0].concept] || null;
  if (!nextLevel) for (let i = 1; i <= 10; i++) if (!done.has(i)) { nextLevel = i; break; }

  const notes = [];
  if (p.dangerSeconds > 45) notes.push(`Spent ${p.dangerSeconds}s near the volcano - a good chance to talk about "the unsafe part of the internet: step back and tell a grown-up".`);
  else if (p.sessionCount > 0) notes.push('Kept away from the volcano hazards - good safety instincts.');
  if (p.avgEfficiency !== null && p.avgEfficiency !== undefined) {
    notes.push(p.avgEfficiency >= 0.8 ? 'Picks efficient routes between dinosaurs.' : 'Often takes the long way round - the Golden Firefly can help spot shorter routes.');
  }
  if (p.streak >= 3) notes.push(`${p.streak}-day play streak - great habit!`);

  const headline = p.sessionCount
    ? `${p.nickname} has played ${p.sessionCount} session${p.sessionCount === 1 ? '' : 's'} (${mins(p.totalSeconds)} min in total) and finished ${p.levelsCompleted} level${p.levelsCompleted === 1 ? '' : 's'}.`
    : `${p.nickname} has not played yet.`;

  return {
    source: 'rules',
    headline,
    playtime: `${mins(weekSeconds)} min in the last 7 days; an average session is ${mins(p.avgSessionSeconds)} min.`,
    strengths: strengths.length ? strengths : ['Still getting started - strengths will show here after a few levels.'],
    focus: focus.length ? focus : ['Nothing to worry about right now.'],
    notes,
    nextStep: nextLevel ? `Suggested next: Level ${nextLevel} - ${LEVEL_NAMES[nextLevel] || ''}.` : 'All levels finished - try the sandbox and build a network!',
    nextLevel,
  };
}

function anonymised(p) {
  return {
    sessions: p.sessionCount,
    totalMinutes: mins(p.totalSeconds),
    avgSessionMinutes: mins(p.avgSessionSeconds),
    levelsCompleted: p.levelsCompleted,
    stars: p.stars,
    streakDays: p.streak,
    dangerSeconds: p.dangerSeconds,
    wrongNodeVisits: p.wrongNodes,
    routeEfficiency: p.avgEfficiency === null ? null : Math.round(p.avgEfficiency * 100) / 100,
    concepts: p.concepts.map((c) => ({ concept: c.concept, questionsAnswered: c.questions, firstTryRate: Math.round(c.mastery * 100) / 100 })),
    levels: p.levels.map((l) => ({ level: l.level, name: l.name, completed: l.completes > 0, bestStars: l.bestStars, attempts: l.starts })),
  };
}

async function aiInsights(p) {
  const key = process.env.ANTHROPIC_API_KEY;
  const base = ruleInsights(p);
  if (!key) return { ...base, aiAvailable: false };
  try {
    const res = await fetch('https://api.anthropic.com/v1/messages', {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'x-api-key': key, 'anthropic-version': '2023-06-01' },
      body: JSON.stringify({
        model: process.env.INSIGHTS_MODEL || 'claude-haiku-4-5-20251001',
        max_tokens: 450,
        system:
          'You write short, warm progress notes for parents and teachers about a child (age 5-9) playing a VR game that teaches internet basics. ' +
          'Use only the numbers given. Do not invent facts. No names. Plain language, max 120 words, 3 short paragraphs: how they are doing, what to celebrate, one gentle thing to practise together. ' +
          'Mention internet safety kindly, never scary.',
        messages: [{ role: 'user', content: JSON.stringify(anonymised(p)) }],
      }),
    });
    if (!res.ok) throw new Error('status ' + res.status);
    const json = await res.json();
    const text = (json.content || []).map((c) => c.text || '').join('').trim();
    if (!text) throw new Error('empty');
    return { ...base, source: 'ai', aiText: text, aiAvailable: true };
  } catch (err) {
    return { ...base, aiAvailable: true, aiError: 'AI summary unavailable right now - showing the standard summary.' };
  }
}

module.exports = { ruleInsights, aiInsights, CONCEPT_LEVEL };
