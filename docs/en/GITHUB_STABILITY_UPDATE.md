# Stability Update — Transport, Lifecycle, and Build

Current documentation baseline: **v2.1.4**, updated on 2026-11-10.

## Platform and build

- The WPF client targets **.NET 8** (`net8.0-windows`) and **x86**.
- The injected DLL and native libraries use **Release | Win32** for the supported 32-bit Lineage 2 Interlude client. Existing x64 project configurations do not establish support for a 64-bit game runtime.
- Build output is `Release\bin\` at the repository root.
- `build.yml` runs regression checks and builds the native DLL and WPF client on Windows. `release.yml` also builds the native pathfinder and packages the Windows x86 release with geodata.
- Release archive: `L2Bot-win-x86.zip`. See [SETUP](SETUP.md) for requirements and build instructions.

## Connection and synchronization

- The client strips the newline from the pipe name received during the handshake, serializes writes, and assembles complete messages before parsing.
- `Invalidate` uses the JSON command format. Each session clears client state and receives a full snapshot with the hero first.
- Resync preserves event-driven item, skill, and buff caches in the DLL. Duplicate creates update existing client entities.
- JSON field names and string values are escaped.
- Repository updates and snapshot serialization share a reentrant world-state lock. Skill cast and cooldown deadlines no longer depend on background timer threads.

## Runtime and client behavior

- The injected runtime stays loaded until the game closes. DLL detach does not wait for a worker under the Windows loader lock.
- Hook trampolines use executable memory and flush the instruction cache. Thread enumeration uses the correct structure size and process filter.
- Client startup errors are visible, removed view models release subscriptions, and chat history is bounded.
- Superseded movement routes stop retrying. Inventory updates distinguish equipment types, soulshot autouse changes propagate, and buffs have client models and a Buffs tab.

## Validation and limits

The v2.1.3 work log records successful Windows CI checks covering native regressions, production C# commands through the C++ parser and dispatcher, actual named pipe handshake, repeated resync, reconnect, shutdown, and x86 native/WPF builds. These are recorded results from that release; current local Windows checks are described in the v2.1.4 notes below.

Live Interlude validation is still required for game structure offsets, five-byte hooks, and the external pathfinder. Close the game to unload the injected runtime. Geodata startup checks still require an online request; interrupted files restart downloading on the next run.

The engine selects and approaches targets, repeats basic attacks, and temporarily excludes unreachable or stalled targets. Stop cancels pending bot writes and requests an action interruption; hero death or disconnect stops the engine. Confirmed target death enters `Loot`, with automatic pickup planned for day 5 of the [roadmap](ROADMAP.md).

See the [v2.1.3 release notes](release-notes/v2.1.3.md) for the detailed change list.

Combat settings, local Windows validation, and current limits are described in the [v2.1.4 release notes](release-notes/v2.1.4.md).
