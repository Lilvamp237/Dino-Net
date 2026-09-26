# Dino Net – test guide (branch `full-build`)

## What is new
- **Levels 5–10** (bundled concepts: signal strength, pieces, lost packets/ACK, broken roads, addresses, firewall gate) – all open from the menu.
- **Main menu**: Play Game (adaptive: picks the recommended level), Choose Level, My Progress, Tutorial, **Sandbox**, Exit.
- **Sandbox**: free play, no timer/hazards; place dinos, link them, send packets, toggle shield / strong password.
- **Stars, badges, streaks**, Golden Firefly analysis, adaptive coach, "the internet is unsafe" volcano message.
- **Voice-over** (offline Windows voice, `Assets/Resources/VO`), procedural music/SFX (`Resources/DinoAudio`), prehistoric skies, dino animations (`DinoAnimator`).
- **Session telemetry** → external dashboard (no in-game login: anonymous device id + family code shown in game).

## Running the dashboard
```
cd dashboard
node server.js          # http://localhost:3000
```
See `dashboard/README.md` (teacher password, parent login by family code, optional `ANTHROPIC_API_KEY` for AI summaries).

The game reads `Dino Net/Assets/Resources/telemetry.json` (`url`, `apiKey`, `enabled`).
**On Quest, `localhost` is the headset** – set `url` to your PC's LAN IP (e.g. `http://192.168.x.x:3000`). Plain HTTP is already allowed (*Player Settings → Allow downloads over HTTP* = Always); switch it back for a release build. Over PC Link (editor play) `localhost` works.

## Test checklist
1. Menu buttons all work; Choose Level lists 10 levels; My Progress shows stars.
2. Play each of levels 5–10 once: lessons appear, voice-over plays, timer/stars shown at the end.
3. Blocked road (L7+ ) → firefly reroutes; lost packet → ACK hearts; gate opens with SHIELD.
4. Sandbox: place two dinos, link, send packet, toggle shield.
5. Dashboard shows the session (time in game, levels, mistakes, concepts).

## Not verified
- Nothing has been tested in the headset or as a standalone Quest build (editor Play Mode only, no console errors).
- `PlaytestDriver` now accepts 3–6 route nodes for levels 5+, but its orb-teleport flow hasn't been run against the new mechanics (blocked roads, gate, lost packet).
- Generated scenes (`Level5–10`, `Sandbox`) come from `DinoNet > Build …` menu items in `LevelSceneBuilder`; rebuild rather than hand-edit.
