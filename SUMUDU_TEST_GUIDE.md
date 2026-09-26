# Dino Net – test guide (branch `full-build`)

## What is new
- **Levels 5–10** (bundled concepts: signal strength, pieces, lost packets/ACK, broken roads, addresses, firewall gate) – all open from the menu.
- **Main menu**: Play Game (adaptive: picks the recommended level), Choose Level, My Progress, Tutorial, **Sandbox**, Exit.
- **Sandbox**: free play, no timer/hazards; place dinos, link them, send packets, toggle shield / strong password.
- **Stars, badges, streaks**, Golden Firefly analysis, adaptive coach, "the internet is unsafe" volcano message.
- **Voice-over** (offline Windows voice, `Assets/Resources/VO`), procedural music/SFX (`Resources/DinoAudio`), prehistoric skies, dino animations (`DinoAnimator`).
- **Session telemetry** → external dashboard (no in-game login: anonymous device id + family code shown in game).

## Getting started (step by step)

**Get the game**
1. Open PowerShell (Windows key, type `PowerShell`, Enter).
2. Get the branch (stash any local changes first with `git stash`):
```powershell
cd "<your Dino-Net folder>"
git fetch
git checkout full-build
git pull
```
3. Open the `Dino Net` folder in Unity 6 via Unity Hub. First import takes a few minutes; wait until the console stops compiling.

**Play in the editor (no dashboard needed)**
4. Open `Assets/Scenes/MainMenu.unity` and press Play (with a headset over Link, use the pointer on the menu buttons).
5. Work through the checklist below: menu, Choose Level, My Progress, levels 5-10, Sandbox.
6. The game works without a dashboard; session events queue up and are sent later.

**Optional: run the dashboard**
7. Install Node (nodejs.org, LTS) if `node --version` fails.
8. In PowerShell:
```powershell
cd "<your Dino-Net folder>"
node dashboard/server.js
```
The default game key `dino-demo-key` already matches `telemetry.json`; the default teacher password is `teacher`.
9. Leave that window open. Open `http://localhost:3000`, log in as teacher, play ~30 seconds, refresh: the session appears.
10. Parents log in with the family code shown on the My Progress screen in the game.

**Standalone Quest build**
11. Find the PC's IP with `ipconfig` (IPv4 Address).
12. Set `url` in `Assets/Resources/telemetry.json` to `http://<that IP>:3000`. PC and headset must be on the same Wi-Fi; allow Node through the Windows firewall.
13. Do not commit that change - `telemetry.json` stays on the demo value in git.

**Please report back**
Console errors, anything that looks or sounds wrong in VR (comfort, voice-over volume), and any level where the packet or a lesson gets stuck (level number + what happened).

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

## Verified so far (editor Play Mode, scripted)
- Playtest driver passes on levels 5-9; level 10 played through all 7 nodes (only a node-count check failed, since widened - not re-run).
- Dashboard smoke test and browser test pass (teacher + parent login).

## Not verified
- Nothing has been tested in the headset or as a standalone Quest build (editor Play Mode only, no console errors).
- `PlaytestDriver` now accepts 3–7 route nodes for levels 5+, but its orb-teleport flow hasn't been run against the new mechanics (blocked roads, gate, lost packet).
- Generated scenes (`Level5–10`, `Sandbox`) come from `DinoNet > Build …` menu items in `LevelSceneBuilder`; rebuild rather than hand-edit.
