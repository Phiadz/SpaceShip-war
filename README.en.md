# SpaceShip-war

A 2D turn-based Sci-Fi Battleship game built with C# WPF, where traditional naval ships are replaced by futuristic space fleets using pixel-art assets.

## 1. Project Overview

This project is a 10x10 grid-based turn strategy game that uses a Peer-to-Peer (P2P) architecture instead of a dedicated server.

- One app instance acts as the Host and waits for TCP connections.
- One app instance acts as the Client and connects to the Host.
- The Client can automatically discover Hosts on LAN via UDP Broadcast Discovery.

Main goals:

1. Build a stable asynchronous networking system for a turn-based game.
2. Keep the WPF UI responsive during connect/listen/send/receive operations.
3. Apply clear OOP architecture for maintainability, testing, and thesis defense.

## 2. Tech Stack

- C# (.NET) + WPF
- TCP (gameplay messages)
- UDP Broadcast (host discovery)
- async/await, Task, CancellationToken, SemaphoreSlim
- MVVM (ViewModel + Command)

## 3. High-Level Architecture

The project is split into clear layers:

1. Presentation layer
- ViewModel, Commands, and WPF bindings for status and actions.

2. Application/Game Session layer
- Manages game phases: Placement -> AwaitingReady -> Combat -> Finished.
- Handles READY/FIRE/RESULT/END messages and turn flow.

3. Networking Infrastructure layer
- TCP connection manager with retry/timeout/reconnect policies.
- UDP discovery service (host announce/listen).
- Text protocol parser/serializer.

4. Domain/Board adapter layer
- 10x10 board logic, hit/miss/sunk resolution, enemy shadow board.

## 4. Folder Structure

```text
SpaceShip-war/
├─ Assets/                                # Pixel-art assets
├─ Source/
│  ├─ Game/
│  │  ├─ Board/
│  │  │  └─ SampleGameCombatStateAdapter.cs
│  │  └─ Session/
│  │     ├─ GameSessionCoordinator.cs
│  │     ├─ IGameSessionCoordinator.cs
│  │     ├─ IGameCombatStateAdapter.cs
│  │     └─ SessionPhase.cs
│  ├─ Networking/
│  │  ├─ NetworkManager.cs
│  │  ├─ NetworkManagerOptions.cs
│  │  ├─ Discovery/
│  │  │  └─ HostDiscoveryService.cs
│  │  ├─ Protocol/
│  │  │  ├─ GameProtocol.cs
│  │  │  └─ GameMessage.cs
│  │  ├─ Abstractions/
│  │  │  ├─ INetworkManager.cs
│  │  │  ├─ IHostDiscoveryService.cs
│  │  │  └─ IGameProtocol.cs
│  │  └─ Events/
│  └─ Presentation/
│     ├─ ViewModels/
│     │  └─ MainGameViewModel.cs
│     ├─ Commands/
│     └─ MainWindow.Sample.xaml
└─ README.md
```

## 5. TCP Protocol Messages

Messages are line-based plain text:

- READY|1
- FIRE|x|y
- RESULT|x|y|MISS/HIT/SUNK
- END|WIN/LOSE

Why text protocol:

1. Easy debugging during development.
2. Easy command extension.
3. Easy explanation for project defense.

## 6. Runtime Flow

1. Host starts TCP listener and optionally starts UDP announcement.
2. Client listens for discovery packets or enters Host IP manually.
3. Client connects to Host via TCP (with retry + timeout).
4. Both players confirm READY.
5. Combat starts:
- Current player sends FIRE.
- Receiver resolves hit/miss/sunk on local board and replies RESULT.
- Shooter updates enemy shadow board; turn switches.
6. Game ends with END message and session enters Finished phase.

## 7. Implemented Networking Features

- Fully asynchronous connect/send/receive operations.
- UI-safe events dispatched through WPF Dispatcher.
- Connection retry with backoff.
- Connect and send timeout handling.
- Client auto-reconnect after unexpected disconnect.
- Reconnect progress events for UI feedback.

## 8. Demo Guide (2 LAN Machines)

1. Machine A (Host):
- Start Host
- Start Announce
- Press Ready after fleet setup

2. Machine B (Client):
- Listen Discovery
- Select discovered host (or enter IP manually)
- Connect
- Press Ready

3. Combat:
- Fire at enemy coordinates when it is your turn
- Observe status and event logs on UI

## 9. Current Repository State

This repository contains modular source code (OOP architecture) and sample XAML/ViewModel for the networking flow.
To run as a complete executable game, integrate these modules into your main WPF solution startup (App/MainWindow) and connect board UI rendering to gameplay logic.

## 10. Future Improvements

1. Add fully interactive 10x10 board UI (drag/drop fleet placement).
2. Add save/load state and reconnect-resume support.
3. Add unit tests for protocol parser and session coordinator.
4. Add stronger security validation for inbound packets.

## 11. Author & Purpose

This project is built for learning and capability demonstration in:

- Layered OOP architecture
- C# network programming
- Async processing and WPF UI thread safety
- P2P turn-based game design
