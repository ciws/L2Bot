# L2Bot Roadmap — 10 days

**[Русская версия](../ru/ROADMAP.md)**

Goal: turn the client from a "radar with manual control" into a real bot with an autonomous farming loop. Every day ends with a meaningful commit(s) to `master` and a release tag with English and Russian release notes. Every push is verified by a GitHub Actions build.

Legend: 🖥 C# client • 💉 C++ DLL • 🔧 infrastructure

## Day 1 — Foundation: .NET 8 + CI build check 🔧
- Upgrade client `net6.0-windows` → `net8.0-windows` (v6 is out of support)
- `build.yml` workflow: build the whole solution on every push to master (no release)
- Clean up client warnings
- **Commit:** `build: net8 + ci build check on every push`

## Day 2 — Bot engine skeleton 🖥
- `BotEngine`: state machine `Idle → Target → Approach → Fight → Loot → Rest`
- Start/Stop button in the main window, current state indicator
- No logic yet — just the skeleton and stub transitions
- **Commit:** `feat: bot engine state machine skeleton + start/stop ui`

## Day 3 — Target selection 🖥 ✅ (v2.1.2)
- Find the nearest mob within radius via `WorldHandler` (filters: NPC/mob, alive, not fighting someone else)
- Radius setting in `config.json`
- Approach the target via `AsyncPathMover` using geodata
- **Commit:** `feat: target selection and approach`
- Settings: `TargetSearchRadius` (1500 world units) and `TargetApproachDistance` (80); restart the client after editing `config.json`.
- Occupied mobs are detected from observed attack events in either direction, with a 15-second timeout since the last attack. Fights that started before observation or have no attack packets for 15 seconds may not be detected.
- Day 3 acquires and approaches a target, then holds in `Fight` without attacking. A missing/dead/occupied target triggers a new search; Stop cancels the route.
- Logic checks: `dotnet run --project tests/BotEngine.Tests -c Release`. Full client/DLL build runs in Windows CI; live game validation is still required.

## Day 4 — Combat 🖥
- Attack loop until the target dies, timeouts
- Anti-stuck: target unreachable for N seconds → next target
- `Fight → Loot` transition on the death event
- **Commit:** `feat: combat loop with anti-stuck`

## Day 5 — Looting 🖥
- Pick up drop within radius after a kill (`DropCreatedEvent` events)
- Priority: nearest drop first
- **Commit:** `feat: looting after kill`

## Day 6 — Rest and consumables 🖥
- Sit when HP/MP below a threshold, stand when recovered
- Auto HP/MP potions by threshold (`RequestUseItem`)
- Thresholds in `config.json`
- **Commit:** `feat: rest and potion conditions`

## Day 7 — Buff watcher 🖥
- Timer-based self-buff upkeep (`RequestUseSkill` on schedule)
- Buff list and durations in the config
- **Commit:** `feat: buff watcher`

## Day 8 — Settings panel 🖥
- Bot settings UI tab (radius, thresholds, buffs, lists)
- Profile saving to JSON
- **Commit:** `feat: bot settings panel with json profiles`

## Day 9 — Death and safety 🖥
- Death detection → stop the engine + loud log entry
- "Stop" / "wait for resurrect" option
- Client log panel improvements
- **Commit:** `feat: death handling and safety stops`

## Day 10 — Polish and v2.2.0 release 🔧
- README update (new features), SETUP refresh
- Final check, tag `v2.2.0` → CI builds the release automatically
- **Commit:** `release: v2.2.0`

## Beyond 10 days
- 🗺 Waypoints and farm spots on the map (v2.3)
- ⚔ Combat orders: skill priorities, rotations
- 👥 Multi-window game support
