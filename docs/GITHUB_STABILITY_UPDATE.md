# Stability Update - Transport, Lifecycle, and Build Hardening

## Summary
This update focuses on runtime stability, shutdown safety, and build portability across the mixed C++/C# solution.

Primary goals addressed:
- Eliminate high-risk shutdown hangs and deadlock-prone startup behavior.
- Add explicit cancellation and reconnect control on the C# client side.
- Harden named pipe communication under disconnect/reconnect and stop conditions.
- Make project files build reliably in `x86` and `x64` configurations.

## Key Improvements

### C# Client Stability
- Reworked bot lifecycle from `async void` fire-and-forget to cancellable task-based flow.
- Added controlled startup/shutdown handling in WPF `App` and graceful bot stop on exit.
- Added cancellation-aware transport API (`ConnectAsync`, `StartReceiveAsync`, `SendAsync`).
- Hardened named pipe handshake/read behavior and disposal checks.
- Fixed path mover busy-spin bug (`Task.Delay` without await in sync loop).
- Improved send-path fault visibility by logging asynchronous send failures.

### C++ Runtime Stability
- Added transport stop hook and integrated stop signaling before worker thread joins.
- Converted stop flag coordination to atomic usage in world handler threads.
- Reworked named pipe overlapped I/O wait loops to avoid unbounded blocking during stop.
- Added explicit stop/cancel flow for named pipes (`CancelIoEx` + event signaling).
- Corrected connect path handling for overlapped `ConnectNamedPipe` edge cases.
- Moved heavy initialization and teardown work out of `DllMain` into a worker thread to reduce loader-lock risk.

### Build & Portability
- Replaced absolute include paths with relative project include paths.
- Added missing x64 language standard settings and include directories.
- Fixed x64 precompiled-header project configuration mismatches.
- Updated C# `Directory.Build.Props` to avoid strict dependency on `$(SolutionDir)` for CLI builds.

## Files Updated
- `Client/App.xaml.cs`
- `Client/Bot.cs`
- `Client/Directory.Build.Props`
- `Client/Domain/Service/WorldHandler.cs`
- `Client/Domain/Transports/TransportInterface.cs`
- `Client/Infrastructure/Service/AsyncPathMover.cs`
- `Client/Infrastructure/Transports/NamedPipeTransport.cs`
- `L2BotCore/Domain/Services/WorldHandler.h`
- `L2BotCore/Domain/Transports/TransportInterface.h`
- `L2BotCore/L2BotCore.vcxproj`
- `L2BotDll/L2BotDll.vcxproj`
- `L2BotDll/Transports/NamedPipe.h`
- `L2BotDll/Transports/NamedPipeTransport.h`
- `L2BotDll/dllmain.cpp`

## Validation Performed
Solution was built with MSBuild in all major matrix combinations:
- `Debug | x86` - PASS
- `Release | x86` - PASS
- `Debug | x64` - PASS
- `Release | x64` - PASS

## Release Artifacts
Generated locally:
- `artifacts/L2Bot2.0-release-x86.zip`
- `artifacts/L2Bot2.0-release-x64.zip`

## Known Warnings / Follow-up
- `Client` currently targets `net6.0-windows`, which is out of support (NETSDK1138).
- x64 native builds still emit pointer truncation/sign-size warnings in legacy hooking paths. These should be treated as technical debt if x64 runtime support is required.
