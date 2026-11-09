# L2Bot 2.0

**[Русская версия](README.ru.md)**

Yet another ingame bot for Lineage 2 (Interlude).

![L2Bot 2.0](https://github.com/k0t9i/L2Bot2.0/assets/7733997/8b4356b0-f362-4ba8-8ca2-8c5cb949d873)

## Project components

#### L2BotCore (C++)
[The bot framework](L2BotCore). Its realization can be an ingame bot, a radar etc.

#### L2BotDll (C++)
[Injectable DLL](L2BotDll) with framework realization for Lineage 2 Interlude. It is loaded by the client and propagates itself into the game process via a `WH_CALLWNDPROC` Windows hook, activating only inside the Lineage 2 process.

#### InjectionLibrary (C++)
[Helper library](InjectionLibrary) for code injection (hooks, trampolines, process manipulation).

#### Client (C#, WPF, .NET 6)
[Client application](Client) with a world map, entity panels and pathfinding. Work in progress.

## Intercommunications

Communication between the client and the injected code occurs through a named pipe (`PipeL2Bot`) with JSON messages.

## Pathfinding & geodata

Pathfinding is done using [L2jGeodataPathFinder](https://github.com/k0t9i/L2jGeodataPathFinder) — a native DLL that searches paths (A* with post smoothing) over geodata in L2J format (`XX_YY.l2j` files).

Geodata is **not stored in this repository** (~545 MB). The client downloads it automatically on the first startup from the public [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata) repository — a progress window is shown, and interrupted downloads resume on the next run. See [docs/SETUP.md](docs/SETUP.md) for a manual download option.

![Pathfinding](https://github.com/k0t9i/L2Bot2.0/assets/7733997/104e5ff2-7435-4def-be5c-3223f02e37c5)

## Build & run

Windows only. See **[docs/SETUP.md](docs/SETUP.md)** for the full guide: requirements, geodata, building `L2JGeoDataPathFinder.dll` (x86) and `L2BotDll.dll` (Win32), and the first run.

## License

[MIT](LICENSE)
