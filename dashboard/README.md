# Dino Net dashboard

A separate web app for teachers and parents. The VR game sends anonymous session data to it; the dashboard turns that into time in game, progress, and what to practise. It is **not** part of the headset build, and there is **no login inside the game** - children are recognised by an anonymous device id.

No dependencies: only Node 18+ (Chart.js is vendored in `public/vendor`).

```
cd dashboard
npm start            # http://localhost:3000
npm test             # end-to-end smoke test (starts its own temp server)
```

## Logging in
| Who | How |
|---|---|
| Teacher | class password (default `teacher`) - sees every child |
| Parent | the 6-character **family code** shown on the progress screen in the game - sees only that child |

Set real values before showing anyone: `DASH_TEACHER_PASSWORD`, `DINONET_API_KEY`.

## Connecting the game
The game reads `Assets/Resources/telemetry.json`:
```json
{ "url": "http://localhost:3000", "apiKey": "dino-demo-key", "enabled": true }
```
- Testing on a PC / Quest Link: `http://localhost:3000` works.
- Standalone Quest: use the PC's LAN address (`http://192.168.x.x:3000`) or a hosted HTTPS address. Both devices must be on the same network for the LAN option.
- If the server is unreachable the game keeps events on the headset and uploads them later, so play is never interrupted.

## What the game sends
Batches of events to `POST /api/ingest` (header `x-api-key`). Nothing personal: a random device id, a session id, and events such as `session_start`, `heartbeat` (active seconds), `level_start`, `node_connected`, `wrong_node`, `danger_exit`, `lesson_answer`, `level_complete`, `badge_earned`, `sandbox_*`.

## Summaries
- **Standard summary** (always on): rule-based, runs entirely on the server.
- **Friendly AI summary** (optional): set `ANTHROPIC_API_KEY` (and optionally `INSIGHTS_MODEL`). Only anonymised numbers are sent - never nicknames, family codes or device ids. If the key is missing or the call fails, the standard summary is shown.

## Demo data
Teachers can click **Add demo children** on the overview to load pretend children (ids start with `demo-`) and **Remove demo children** to clear only those.

## Deploying
Any Node host works (Render, Railway, Fly, a school server). Set `PORT`, `DASH_TEACHER_PASSWORD`, `DINONET_API_KEY`, and point `DATA_DIR` at a persistent disk. Put it behind HTTPS before real children use it. Data is stored as `events.jsonl`, one JSON event per line.

## Privacy choices
Anonymous device ids, generated nicknames, no names/emails/photos, no in-game accounts, parents access only via a code. Delete a child's data by removing their lines from `events.jsonl` (or all data by deleting the file).
