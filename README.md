# L2Bot 2.0

**[English](#english)** | **[Русский](#русский)**

---

## English

Yet another ingame bot for Lineage 2 (Interlude).

![L2Bot 2.0](https://github.com/k0t9i/L2Bot2.0/assets/7733997/8b4356b0-f362-4ba8-8ca2-8c5cb949d873)

### Project components

#### L2BotCore (C++)
[The bot framework](L2BotCore). Its realization can be an ingame bot, a radar etc.

#### L2BotDll (C++)
[Injectable DLL](L2BotDll) with framework realization for Lineage 2 Interlude. It is loaded by the client and propagates itself into the game process via a `WH_CALLWNDPROC` Windows hook, activating only inside the Lineage 2 process.

#### InjectionLibrary (C++)
[Helper library](InjectionLibrary) for code injection (hooks, trampolines, process manipulation).

#### Client (C#, WPF, .NET 6)
[Client application](Client) with a world map, entity panels and pathfinding. Work in progress.

### Intercommunications

Communication between the client and the injected code occurs through a named pipe (`PipeL2Bot`) with JSON messages.

### Pathfinding & geodata

Pathfinding is done using [L2jGeodataPathFinder](https://github.com/k0t9i/L2jGeodataPathFinder) — a native DLL that searches paths (A* with post smoothing) over geodata in L2J format (`XX_YY.l2j` files).

Geodata is **included in this repository** (`Client/Assets/geodata/`, ~545 MB, L2J format from [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata)). If any geodata files are missing at startup, the client downloads them automatically — a progress window is shown, and interrupted downloads resume on the next run. See [docs/en/SETUP.md](docs/en/SETUP.md) for details.

![Pathfinding](https://github.com/k0t9i/L2Bot2.0/assets/7733997/104e5ff2-7435-4def-be5c-3223f02e37c5)

### Build & run

Windows only. Full guide: **[docs/en/SETUP.md](docs/en/SETUP.md)** — requirements, geodata, building `L2JGeoDataPathFinder.dll` (x86) and `L2BotDll.dll` (Win32), first run.

### License

[MIT](LICENSE)

---

## Русский

Ещё один внутриигровой бот для Lineage 2 (хроники Interlude).

![L2Bot 2.0](https://github.com/k0t9i/L2Bot2.0/assets/7733997/8b4356b0-f362-4ba8-8ca2-8c5cb949d873)

### Состав проекта

#### L2BotCore (C++)
[Фреймворк бота](L2BotCore). На его основе можно реализовать внутриигрового бота, радар и т.д.

#### L2BotDll (C++)
[Инжектируемая DLL](L2BotDll) с реализацией фреймворка для Lineage 2 Interlude. Загружается клиентом и через Windows-хук `WH_CALLWNDPROC` попадает в процесс игры, активируясь только внутри процесса Lineage 2.

#### InjectionLibrary (C++)
[Вспомогательная библиотека](InjectionLibrary) для инжекции кода (хуки, трамплины, работа с процессами).

#### Client (C#, WPF, .NET 6)
[Клиентское приложение](Client) с картой мира, панелями сущностей и поиском пути. В разработке.

### Взаимодействие компонентов

Клиент и инжектированный код общаются через именованный канал (`PipeL2Bot`) сообщениями в формате JSON.

### Поиск пути и геодата

Поиск пути выполняет [L2jGeodataPathFinder](https://github.com/k0t9i/L2jGeodataPathFinder) — нативная DLL, строящая маршрут (A* с пост-сглаживанием) по геодате в формате L2J (файлы `XX_YY.l2j`).

Геодата **включена в этот репозиторий** (`Client/Assets/geodata/`, ~545 МБ, формат L2J из [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata)). Если при запуске каких-то файлов геодаты не хватает, клиент скачивает их автоматически — отображается окно с прогрессом, а прерванная загрузка продолжится при следующем запуске. Подробности в [docs/ru/SETUP.md](docs/ru/SETUP.md).

![Поиск пути](https://github.com/k0t9i/L2Bot2.0/assets/7733997/104e5ff2-7435-4def-be5c-3223f02e37c5)

### Сборка и запуск

Только Windows. Полная инструкция: **[docs/ru/SETUP.md](docs/ru/SETUP.md)** — требования, геодата, сборка `L2JGeoDataPathFinder.dll` (x86) и `L2BotDll.dll` (Win32), первый запуск.

### Лицензия

[MIT](LICENSE)
