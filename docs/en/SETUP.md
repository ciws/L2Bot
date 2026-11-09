# Building and first run of L2Bot 2.0

The bot works on Windows only (DLL injection + WPF client). Chronicles: **Interlude**.

## Requirements

- Windows 10/11
- Visual Studio 2022 with the "Desktop development with C++" workload
- .NET 8 SDK (for the client)
- Lineage 2 Interlude game client

## Project structure

| Project | Language | Purpose |
|---|---|---|
| `L2BotCore` | C++ | Bot framework core (linked into L2BotDll) |
| `L2BotDll` | C++ | Injectable DLL, Interlude realization |
| `InjectionLibrary` | C++ | Injection library (hooks, trampolines) |
| `Client` | C# WPF | GUI client: map, controls, pathfinding |

The client and the injected code communicate over the named pipe `PipeL2Bot` using JSON messages.

## Step 1. Geodata

Geodata is **included in the repository** (`Client/Assets/geodata/`, ~545 MB, L2J format from [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata)): 165 regions `XX_YY.l2j` + `geo_index.txt`. After a `git clone` it is already in place and `Client.csproj` copies it to the output.

If some files are missing at startup (deleted, incomplete clone), the client downloads them automatically from the same repository — a progress window is shown. Cancelling closes the application (pathfinding does not work without geodata). This is exactly the format `L2JGeoDataPathFinder.dll` reads.

## Step 2. Building L2JGeoDataPathFinder.dll

Pathfinding is performed by a separate native DLL: https://github.com/k0t9i/L2jGeodataPathFinder

1. `git clone https://github.com/k0t9i/L2jGeodataPathFinder.git`
2. Open `L2jGeodataPathFinder.sln`, build **Release | x86**
3. Copy `L2JGeoDataPathFinder.dll` into the `Client/` folder of this repository — `Client.csproj` picks it up into the output automatically

> Important: exactly **x86** — the client is built with `PlatformTarget=x86`; a DLL of a different bitness will not load.

## Step 3. Building the C++ part

1. Open `L2Bot.sln`
2. Configuration **Release | Win32** (the game is 32-bit, the client is x86)
3. Build `InjectionLibrary`, `L2BotCore`, `L2BotDll`
4. Put `L2BotDll.dll` next to `Client.exe` (into the client output)

## Step 4. Building the client

```bat
cd Client
dotnet build -c Release
```

The output (`Release\bin\`) automatically receives: `config.json`, `Assets\icons`, `Assets\maps`, `Assets\data`, `Assets\geodata`, `L2BotDll.dll` (if placed in the project folder) and `L2JGeoDataPathFinder.dll`.

## Step 5. Run

1. Start the Lineage 2 Interlude client and log into the world
2. Start `Client.exe`
3. The client loads `L2BotDll.dll` via `LoadLibrary`; the DLL installs a `WH_CALLWNDPROC` Windows hook, lands in the game process and activates only inside Lineage 2 (mutex `L2BotHookMutex`)
4. The client and the injected code connect over the `PipeL2Bot` pipe

## Configuration (`Client/config.json`)

| Parameter | Default | Description |
|---|---|---|
| `DLLName` | `L2BotDll.dll` | Injected DLL name |
| `ConnectionPipeName` | `PipeL2Bot` | Named pipe name |
| `MaxPassableHeight` | `30` | Max height difference for pathfinding |
| `GeoDataDirectory` | `geodata` | Subfolder inside `Assets/` with geodata |
| `PathNumberOfAttempts` | `5` | Path building attempts |
| `NodeWaitingTime` | `3` | Node wait time, seconds |
| `NodeDistanceTolerance` | `8` | Node reach tolerance |
| `NextNodeDistanceTolerance` | `16` | Next-node switch tolerance |

## Troubleshooting

- Injected DLL log: `app.log` next to `L2BotDll.dll`
- `Unable to load library L2BotDll.dll` — bitness mismatch (Win32/x86 required) or the DLL is not next to `Client.exe`
- Pathfinding is silent / path is empty — check that the output contains `Assets\geodata\*.l2j` and `L2JGeoDataPathFinder.dll` (x86)
