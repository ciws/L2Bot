<div align="center">

# 🤖 L2Bot 2.0

**Yet another ingame bot for Lineage 2 (Interlude)**

[![Release](https://img.shields.io/github/v/release/ciws/L2Bot)](https://github.com/ciws/L2Bot/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows-blue)](#)
[![Client](https://img.shields.io/badge/client-.NET%206%20WPF-purple)](#)
[![Core](https://img.shields.io/badge/core-C%2B%2B-orange)](#)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

**[🇬🇧 English](#-english)** • **[🇷🇺 Русский](#-русский)**

![L2Bot 2.0](https://github.com/k0t9i/L2Bot2.0/assets/7733997/8b4356b0-f362-4ba8-8ca2-8c5cb949d873)

</div>

---

## 🇬🇧 English

### ✨ What is this

L2Bot 2.0 is an ingame bot for Lineage 2 Interlude: a native DLL is injected into the game process, and a WPF client controls it from the outside — map, entities, chat, skills and automated pathfinding.

### 🧩 Project components

| Component | Stack | Description |
|---|---|---|
| 🧠 [`L2BotCore`](L2BotCore) | C++ | Bot framework core — entities, services, events, repositories |
| 💉 [`L2BotDll`](L2BotDll) | C++ | Injectable DLL, Interlude realization |
| 🪝 [`InjectionLibrary`](InjectionLibrary) | C++ | Code injection helpers — hooks, trampolines, process manipulation |
| 🖥️ [`Client`](Client) | C#, WPF, .NET 6 | GUI client with world map and pathfinding *(work in progress)* |

### 🔄 How it works

1. 🎮 You launch the game and log into the world
2. 🖥️ The client loads `L2BotDll.dll` via `LoadLibrary`
3. 🪝 The DLL lands in the game process through a `WH_CALLWNDPROC` Windows hook and activates only inside Lineage 2 (mutex `L2BotHookMutex`)
4. 🔌 Client and injected code talk over the named pipe `PipeL2Bot` using JSON messages

### 🗺️ Pathfinding & geodata

Pathfinding is powered by [L2jGeodataPathFinder](https://github.com/k0t9i/L2jGeodataPathFinder) — a native DLL running A* with post smoothing over real L2J geodata (`XX_YY.l2j`).

- 📦 Geodata **ships with this repository** (`Client/Assets/geodata/`, 165 regions, ~545 MB, thanks to [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata))
- 🌐 If any geodata files are missing at startup, the client **downloads them automatically** with a progress window — interrupted downloads resume on the next run

![Pathfinding](https://github.com/k0t9i/L2Bot2.0/assets/7733997/104e5ff2-7435-4def-be5c-3223f02e37c5)

### 🚀 Quick start

**Just want to run it?** Grab the ready-made build: 📥 [L2Bot2.0-win-x86.zip](https://github.com/ciws/L2Bot/releases/latest)

**Want to build from source?** Full guide: 📖 [docs/en/SETUP.md](docs/en/SETUP.md) — requirements, geodata, building `L2JGeoDataPathFinder.dll` (x86) and `L2BotDll.dll` (Win32), first run.

> ⚠️ Windows only — DLL injection and the WPF client require it.

### 📚 Documentation

- 📖 [Build & setup guide](docs/en/SETUP.md)
- 🗺️ [Roadmap — 10 days](docs/en/ROADMAP.md)
- 🔧 [Stability update notes](docs/en/GITHUB_STABILITY_UPDATE.md)

### 📄 License

[MIT](LICENSE)

---

## 🇷🇺 Русский

### ✨ Что это такое

L2Bot 2.0 — внутриигровой бот для Lineage 2 Interlude: нативная DLL внедряется в процесс игры, а WPF-клиент управляет ею снаружи — карта, сущности, чат, скиллы и автоматический поиск пути.

### 🧩 Состав проекта

| Компонент | Стек | Описание |
|---|---|---|
| 🧠 [`L2BotCore`](L2BotCore) | C++ | Ядро-фреймворк бота — сущности, сервисы, события, репозитории |
| 💉 [`L2BotDll`](L2BotDll) | C++ | Инжектируемая DLL, реализация под Interlude |
| 🪝 [`InjectionLibrary`](InjectionLibrary) | C++ | Библиотека инжекции — хуки, трамплины, работа с процессами |
| 🖥️ [`Client`](Client) | C#, WPF, .NET 6 | GUI-клиент с картой мира и поиском пути *(в разработке)* |

### 🔄 Как это работает

1. 🎮 Запускаешь игру и заходишь персонажем в мир
2. 🖥️ Клиент загружает `L2BotDll.dll` через `LoadLibrary`
3. 🪝 DLL попадает в процесс игры через Windows-хук `WH_CALLWNDPROC` и активируется только внутри Lineage 2 (мьютекс `L2BotHookMutex`)
4. 🔌 Клиент и инжектированный код общаются по именованному каналу `PipeL2Bot` сообщениями в JSON

### 🗺️ Поиск пути и геодата

Поиск пути выполняет [L2jGeodataPathFinder](https://github.com/k0t9i/L2jGeodataPathFinder) — нативная DLL, строящая маршрут по настоящей L2J-геодате (`XX_YY.l2j`) алгоритмом A* с пост-сглаживанием.

- 📦 Геодата **включена в репозиторий** (`Client/Assets/geodata/`, 165 регионов, ~545 МБ, спасибо [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata))
- 🌐 Если каких-то файлов геодаты не хватает, клиент **скачивает их сам** — с окном прогресса, а прерванная загрузка продолжится при следующем запуске

![Поиск пути](https://github.com/k0t9i/L2Bot2.0/assets/7733997/104e5ff2-7435-4def-be5c-3223f02e37c5)

### 🚀 Быстрый старт

**Просто запустить?** Бери готовую сборку: 📥 [L2Bot2.0-win-x86.zip](https://github.com/ciws/L2Bot/releases/latest)

**Собрать из исходников?** Полная инструкция: 📖 [docs/ru/SETUP.md](docs/ru/SETUP.md) — требования, геодата, сборка `L2JGeoDataPathFinder.dll` (x86) и `L2BotDll.dll` (Win32), первый запуск.

> ⚠️ Только Windows — инжекция DLL и WPF-клиент требуют её.

### 📚 Документация

- 📖 [Инструкция по сборке и запуску](docs/ru/SETUP.md)
- 🗺️ [Дорожная карта — 10 дней](docs/ru/ROADMAP.md)
- 🔧 [Заметки об обновлении стабильности](docs/ru/GITHUB_STABILITY_UPDATE.md)

### 📄 Лицензия

[MIT](LICENSE)
