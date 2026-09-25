'use strict';
// Append-only event store. Every event the game sends is written as one JSON line, so the
// dashboard survives restarts with no database to install. Reads are served from memory.
const fs = require('fs');
const path = require('path');

const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, '..', 'data');
const FILE = path.join(DATA_DIR, 'events.jsonl');

let events = [];
let version = 0;

function load() {
  fs.mkdirSync(DATA_DIR, { recursive: true });
  events = [];
  if (!fs.existsSync(FILE)) return;
  const lines = fs.readFileSync(FILE, 'utf8').split('\n');
  for (const line of lines) {
    if (!line.trim()) continue;
    try { events.push(JSON.parse(line)); } catch (_) { /* skip a torn line */ }
  }
  version++;
}

function clean(e, batch) {
  const t = Number(e.t);
  return {
    t: Number.isFinite(t) && t > 0 ? t : Date.now(),
    deviceId: String(batch.deviceId || e.deviceId || '').slice(0, 64),
    sessionId: String(batch.sessionId || e.sessionId || '').slice(0, 64),
    type: String(e.type || '').slice(0, 40),
    level: Number.isFinite(Number(e.level)) ? Number(e.level) : 0,
    data: e.data && typeof e.data === 'object' ? e.data : {},
  };
}

function add(batch) {
  const list = Array.isArray(batch.events) ? batch.events : [];
  const accepted = [];
  for (const e of list) {
    const c = clean(e, batch);
    if (!c.deviceId || !c.sessionId || !c.type) continue;
    accepted.push(c);
  }
  if (!accepted.length) return 0;
  fs.mkdirSync(DATA_DIR, { recursive: true });
  fs.appendFileSync(FILE, accepted.map((e) => JSON.stringify(e)).join('\n') + '\n');
  events.push(...accepted);
  version++;
  return accepted.length;
}

function addMany(list) {
  if (!list.length) return 0;
  fs.mkdirSync(DATA_DIR, { recursive: true });
  fs.appendFileSync(FILE, list.map((e) => JSON.stringify(e)).join('\n') + '\n');
  events.push(...list);
  version++;
  return list.length;
}

function removeWhere(predicate) {
  const keep = events.filter((e) => !predicate(e));
  const removed = events.length - keep.length;
  events = keep;
  fs.mkdirSync(DATA_DIR, { recursive: true });
  fs.writeFileSync(FILE, events.map((e) => JSON.stringify(e)).join('\n') + (events.length ? '\n' : ''));
  version++;
  return removed;
}

module.exports = {
  load,
  add,
  addMany,
  removeWhere,
  all: () => events,
  version: () => version,
};
