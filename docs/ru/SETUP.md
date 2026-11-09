# Сборка и первый запуск L2Bot 2.0

Бот работает только под Windows (инжекция DLL + WPF-клиент). Хроники: **Interlude**.

## Требования

- Windows 10/11
- Visual Studio 2022 с workload «Desktop development with C++»
- .NET 8 SDK (для клиента)
- Клиент Lineage 2 Interlude

## Структура проекта

| Проект | Язык | Назначение |
|---|---|---|
| `L2BotCore` | C++ | Ядро-фреймворк бота (линкуется в L2BotDll) |
| `L2BotDll` | C++ | Инжектируемая DLL, реализация под Interlude |
| `InjectionLibrary` | C++ | Библиотека инжекции (хуки, trampoline) |
| `Client` | C# WPF | GUI-клиент: карта, управление, pathfinding |

Связь клиента с инжектированным кодом — named pipe `PipeL2Bot`, сообщения в JSON.

## Шаг 1. Геодата

Геодата **включена в репозиторий** (`Client/Assets/geodata/`, ~545 МБ, формат L2J из [LGK-Games/Geodata](https://github.com/LGK-Games/Geodata)): 165 регионов `XX_YY.l2j` + `geo_index.txt`. После `git clone` она уже на месте, а `Client.csproj` копирует её в output.

Если при запуске каких-то файлов не хватает (удалены, неполный клон), клиент сам докачает их из того же репозитория — появится окно с прогрессом. Отмена закрывает приложение (без геодаты pathfinding не работает). Это именно тот формат, который читает `L2JGeoDataPathFinder.dll`.

## Шаг 2. Сборка L2JGeoDataPathFinder.dll

Pathfinding делается отдельной нативной DLL: https://github.com/k0t9i/L2jGeodataPathFinder

1. `git clone https://github.com/k0t9i/L2jGeodataPathFinder.git`
2. Открыть `L2jGeodataPathFinder.sln`, собрать **Release | x86**
3. Скопировать `L2JGeoDataPathFinder.dll` в папку `Client/` этого репозитория — `Client.csproj` сам подхватит её в output

> Важно: именно **x86** — клиент собран под `PlatformTarget=x86`, DLL другой битности не загрузится.

## Шаг 3. Сборка C++-части

1. Открыть `L2Bot.sln`
2. Конфигурация **Release | Win32** (игра 32-битная, клиент x86)
3. Собрать `InjectionLibrary`, `L2BotCore`, `L2BotDll`
4. Положить `L2BotDll.dll` рядом с `Client.exe` (в output клиента)

## Шаг 4. Сборка клиента

```bat
cd Client
dotnet build -c Release
```

В output (`bin\Release\net6.0-windows\`) автоматически копируются: `config.json`, `Assets\icons`, `Assets\maps`, `Assets\data`, `Assets\geodata`, `L2BotDll.dll` (если положил в папку проекта) и `L2JGeoDataPathFinder.dll`.

## Шаг 5. Запуск

1. Запусти клиент Lineage 2 Interlude, зайди персонажем в мир
2. Запусти `Client.exe`
3. Клиент загружает `L2BotDll.dll` через `LoadLibrary`; DLL ставит Windows-хук `WH_CALLWNDPROC`, попадает в процесс игры и активируется только внутри Lineage 2 (мьютекс `L2BotHookMutex`)
4. Клиент и инжектированный код соединяются по пайпу `PipeL2Bot`

## Конфигурация (`Client/config.json`)

| Параметр | Значение по умолчанию | Описание |
|---|---|---|
| `DLLName` | `L2BotDll.dll` | Имя инжектируемой DLL |
| `ConnectionPipeName` | `PipeL2Bot` | Имя named pipe |
| `MaxPassableHeight` | `30` | Макс. перепад высот для pathfinding |
| `GeoDataDirectory` | `geodata` | Подпапка в `Assets/` с геодатой |
| `PathNumberOfAttempts` | `5` | Попыток построить путь |
| `NodeWaitingTime` | `3` | Ожидание узла пути, сек |
| `NodeDistanceTolerance` | `8` | Допуск достижения узла |
| `NextNodeDistanceTolerance` | `16` | Допуск перехода к след. узлу |

## Диагностика

- Лог инжектированной DLL: `app.log` рядом с `L2BotDll.dll`
- Ошибка `Unable to load library L2BotDll.dll` — не совпала битность (нужна Win32/x86) или DLL не лежит рядом с `Client.exe`
- Pathfinding молчит / путь пустой — проверь, что в output есть `Assets\geodata\*.l2j` и `L2JGeoDataPathFinder.dll` (x86)
