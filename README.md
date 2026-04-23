# SpaceShip-war

Trò chơi chiến thuật theo lượt 2D kiểu Sci-Fi Battleship xây dựng bằng C# WPF, trong đó tàu chiến hải quân truyền thống được thay bằng hạm đội không gian dùng pixel-art.

## 1. Giới thiệu đề tài

Đây là đồ án game chiến thuật theo lượt trên lưới 10x10, sử dụng mô hình P2P (Peer-to-Peer) thay vì dedicated server.

- Một instance đóng vai trò Host và chờ kết nối qua TCP.
- Một instance đóng vai trò Client và kết nối đến Host.
- Client có thể tự động tìm Host trong LAN thông qua UDP Broadcast Discovery.

Mục tiêu chính của đề tài:

1. Xây dựng hệ thống networking bất đồng bộ ổn định cho game turn-based.
2. Đảm bảo UI WPF luôn responsive khi kết nối, đợi đối thủ, gửi nhận nước đi.
3. Chia tách kiến trúc OOP rõ ràng để dễ mở rộng, dễ test và dễ bảo vệ đồ án.

## 2. Công nghệ sử dụng

- C# (.NET) + WPF
- TCP (gameplay messages)
- UDP Broadcast (host discovery)
- async/await, Task, CancellationToken, SemaphoreSlim
- MVVM (ViewModel + Command)

## 3. Kiến trúc tổng thể

Project được tách thành các tầng rõ ràng:

1. Presentation layer
- ViewModel, Commands, binding trạng thái và thao tác cho WPF UI.

2. Application/Game Session layer
- Điều phối phase trận đấu: Placement -> AwaitingReady -> Combat -> Finished.
- Quản lý lượt chơi và xử lý message READY/FIRE/RESULT/END.

3. Networking Infrastructure layer
- TCP connection manager, retry/timeout/reconnect policy.
- UDP discovery service (announce/listen host).
- Protocol parser/serializer cho message text.

4. Domain/Board adapter layer
- Xử lý board 10x10, hit/miss/sunk, enemy shadow board.

## 4. Cấu trúc thư mục

```text
SpaceShip-war/
├─ Assets/                                # Pixel-art assets by [Zintoki](https://zintoki.itch.io/space-breaker)
├─ Source/
│  ├─ Game/
│  │  ├─ Board/                           # Combat board state adapter
│  │  │  └─ SampleGameCombatStateAdapter.cs  # 10x10 board, hit/miss/sunk logic, enemy shadow board
│  │  ├─ Economy/                         # Economy system
│  │  │  ├─ PlayerEconomy.cs              # Credit management
│  │  │  ├─ ShopItem.cs                   # Item definition with dynamic asset binding
│  │  │  ├─ WeaponCatalog.cs              # Centralized 7-item catalog
│  │  │  ├─ BudgetRules.cs                # Economy configuration
│  │  │  └─ ShipLoadout.cs                # Ship equipment tracking
│  │  └─ Session/                         # Game session management & phase coordination
│  │     ├─ GameSessionCoordinator.cs     # Phase manager (Placement → AwaitingReady → Combat → Finished)
│  │     ├─ IGameSessionCoordinator.cs    # Session coordinator interface contract
│  │     ├─ IGameCombatStateAdapter.cs    # Board combat logic interface (hit/miss/sunk)
│  │     ├─ SessionPhase.cs               # Enum: Placement, AwaitingReady, Combat, Finished
│  │     └─ Events.cs                     # Session event definitions (phase changed, turn changed)
│  ├─ Networking/
│  │  ├─ NetworkManager.cs                 # TCP/UDP connection manager, send/receive, reconnect logic
│  │  ├─ NetworkManagerOptions.cs          # Configuration (timeout, retry count, ports)
│  │  ├─ NetworkRole.cs                    # Enum: Host or Client
│  │  ├─ ConnectionState.cs                # Enum: Idle, Connecting, Connected, Disconnected, Error
│  │  ├─ Abstractions/                     # Interface contracts
│  │  │  ├─ INetworkManager.cs             # Core network operations contract
│  │  │  ├─ IHostDiscoveryService.cs       # LAN host discovery contract
│  │  │  └─ IGameProtocol.cs               # Message parser/serializer contract
│  │  ├─ Discovery/                        # UDP broadcast host discovery
│  │  │  └─ HostDiscoveryService.cs        # Announce/listen for hosts in LAN
│  │  ├─ Protocol/                         # Message protocol handling
│  │  │  ├─ GameProtocol.cs                # Text parser (READY, FIRE, RESULT, END)
│  │  │  ├─ GameMessage.cs                 # Message data structure
│  │  │  ├─ GameMessageType.cs             # Enum: message types
│  │  │  └─ ShotOutcome.cs                 # Enum: MISS/HIT/SUNK outcomes
│  │  └─ Events/                           # Custom event arguments
│  │     ├─ ConnectionStateChangedEventArgs.cs  # Connection state change notification
│  │     ├─ HostDiscoveredEventArgs.cs     # Host found in LAN
│  │     ├─ MessageReceivedEventArgs.cs    # Game message received
│  │     ├─ NetworkErrorEventArgs.cs       # Network error occurred
│  │     └─ ReconnectAttemptedEventArgs.cs # Reconnect attempt progress
│  └─ Presentation/                        # UI layer (WPF)
│     ├─ MainWindow.xaml                   # Root window layout (board + command center)
│     ├─ MainWindow.Sample.xaml            # Reference design (layout template)
│     ├─ ViewModels/
│     │  ├─ MainGameViewModel.cs           # Master ViewModel - orchestrates all services
│     │  ├─ BoardCellViewModel.cs          # 10x10 grid cell binding
│     │  ├─ DiscoveredHostViewModel.cs     # Host discovery list
│     │  ├─ FleetAssetViewModel.cs         # Ship preview + interaction
│     │  ├─ PlacementShipViewModel.cs      # Draggable ship during placement
│     │  ├─ VisualPlacedShipViewModel.cs   # Visual ship display (inspector preview)
│     │  ├─ InventoryItemViewModel.cs      # Phase 1 - Purchased item tracking
│     │  ├─ EquippedWeaponViewModel.cs     # Phase 1 - Equipped weapon display
│     │  ├─ ObservableObject.cs            # MVVM base class
│     │  └─ ViewModelBootstrapper.cs       # ViewModel initialization
│     ├─ Commands/
│     │  ├─ RelayCommand.cs                # Synchronous command
│     │  ├─ AsyncRelayCommand.cs           # Async command without parameter
│     │  └─ AsyncRelayCommandOfT.cs        # Generic async command with parameter
│     └─ Converters/                       # Value converters for WPF
│        ├─ EquippedToOpacityConverter.cs  # Maps IsEquipped bool → opacity (0.4 for equipped, 1.0 for unequipped)
│        └─ NullToVisibilityConverter.cs   # Maps null → Visible, not-null → Collapsed (for placeholder text)
├─ bin/
├─ obj/
├─ SpaceShipWar.csproj
├─ App.xaml
├─ App.xaml.cs
├─ run-two-instances.ps1
└─ README.md
```

## 5. Protocol message (TCP)

Message theo dạng text, mỗi message là một dòng:

- READY|1
- FIRE|x|y
- RESULT|x|y|MISS/HIT/SUNK
- END|WIN/LOSE

Lý do chọn text protocol:

1. Dễ debug trong giai đoạn phát triển/đồ án.
2. Dễ mở rộng nhanh command mới.
3. Dễ giải thích rõ trong vấn đáp.

## 6. Luồng hoạt động chương trình

1. Host mở TCP listener và (tùy chọn) bắt đầu UDP announcement.
2. Client lắng nghe discovery để tìm host trong LAN hoặc nhập IP thủ công.
3. Client kết nối TCP đến Host (có retry + timeout).
4. Hai bên xác nhận READY.
5. Vao combat turn-based:
- Bên đến lượt gửi FIRE.
- Bên nhận tính kết quả board local và gửi RESULT.
- Bên gửi cập nhật enemy shadow board, đổi lượt.
6. Khi kết thúc trận, gửi END và đưa session sang Finished.

## 7. Tính năng networking đã có

- Async hoàn toàn cho connect, send, receive.
- UI-safe events qua Dispatcher (không update control từ worker thread).
- Retry connect với backoff.
- Timeout cho connect và send.
- Auto reconnect cho client khi mất kết nối ngoài ý muốn.
- Event thông báo tiến trình reconnect để hiển thị trên UI.

## 8. Hướng dẫn chạy demo (2 máy trong cùng LAN)

1. Máy A (Host):
- Start Host
- Start Announce
- Ready sau khi đặt đội hình

2. Máy B (Client):
- Listen Discovery
- Chọn host tìm được (hoặc nhập IP)
- Connect
- Ready

3. Combat:
- Đến lượt thì bắn vào tọa độ đối thủ
- Theo dõi status và event log trên UI

## 9. 📊 Trạng thái hiện tại - Phase 1 HOÀN THÀNH

### Economy System (NEW) 
- **PlayerEconomy class**: Manage credits (initial: 100 CR), track Spent/Remaining
- **ShopItem class**: Dynamic ImagePath property cho runtime asset resolution
- **WeaponCatalog**: 7+ items (Laser, Plasma, Shield, Cannon, Missile, Drone, Turret)
- **Command Center UI** (3-panel bottom layout):
  - **Ship Inspector** (Green): Preview tàu + equipped weapons
  - **Inventory** (Blue): Purchased items với opacity toggle
  - **Armory** (Red): Shop grid, double-click buy, single-click preview description
- **Commands**: BuyItemCommand (AsyncRelayCommand<T>), SelectShopItemCommand, InspectShipCommand
- **Value Converters**: EquippedToOpacityConverter, NullToVisibilityConverter

### Networking Layer 
- TCP Manager: Kết nối bất đồng bộ, retry, timeout, auto-reconnect
- UDP Discovery: Host announcement, LAN client discovery
- Protocol: Text-based parser (READY|1, FIRE|x|y, RESULT|x|y|outcome, END|WIN/LOSE)
- Event system: Dispatcher-safe callbacks
- Resilience: Retry backoff, timeout handling, auto-reconnect

### Game Session Layer 
- Phase management: Placement → AwaitingReady → Combat → Finished
- Turn-based logic: Local + remote turn tracking
- Session reset: Full state cleanup
- Message orchestration: READY sync → Combat → END

### Domain/Board Layer 
- Hit/Miss/Sunk resolution, enemy shadow board, local board tracking

### Presentation Layer 
- MainGameViewModel: Full orchestration + economy integration
- InventoryItemViewModel (NEW), EquippedWeaponViewModel (NEW)
- AsyncRelayCommand<T> (NEW) for parameterized commands
- 10 ViewModel classes + 4 Command classes + 2 Converters
- Status: connection, phase, turn, budget, last event, game result

### Infrastructure 
- Asset loading: Runtime path resolution
- run-two-instances.ps1: Fresh rebuild + dual-instance
- Dispatcher integration, asset auto-copy

### Test & Validation 
- End-to-end Placement → Combat
- Reconnect scenarios
- **NEW**: Economy flow (buy, budget, inventory)

## 10. Chuẩn bị cho mở rộng Phase 2

### Shipment & Sync với Network
- Gửi **LOADOUT** message kèm Inventory state lúc ConfirmReady
- Nhận LOADOUT từ đối thủ, update UI SelectedInspectorShip
- Gửi **CREDITS** message khi hit/sunk/win

### Ship Hardpoint System (TBD)
- **Hardpoints**: Tàu có fixed slots (Small, Medium, Heavy)
- **Drag-drop** weapon vào slot hoặc double-click inventory
- **Visual overlay**: Vẽ weapon asset tại đúng hardpoint position
- **Budget per ship**: MaxPerShip (50 CR) + global MaxPerMatch (100 CR)

### In-Combat Economy (TBD)
- Mở Shop lúc Combat
- Mua vũ khí giữa lượt
- Nhận tiền real-time (Hit=+10, Sunk=+25, Win=+50)

### Protocol Extension (TBD)
- `CREDITS|amount` (sync tiền sau shot)
- `LOADOUT|ship|weaponIds` (sync loadout at READY)
- `UPGRADE|ship|weaponId` (announce buy in-combat)

## 11. Cấu trúc Project

**Source/Game/Economy/** (NEW Phase 1)
- PlayerEconomy.cs, ShopItem.cs, BudgetRules.cs, ShipLoadout.cs, WeaponCatalog.cs

**Source/Presentation/ViewModels/** (Phase 1 expansion)
- NEW: InventoryItemViewModel.cs, EquippedWeaponViewModel.cs

**Source/Presentation/Commands/** (Phase 1 addition)
- NEW: AsyncRelayCommand<T>.cs (generic version for parameterized commands)

**Source/Presentation/Converters/** (NEW)
- EquippedToOpacityConverter.cs, NullToVisibilityConverter.cs

**MainWindow.xaml** (Phase 1 redesign)
- Command Center bottom section: 3-panel grid (Ship Inspector, Inventory, Armory)
- Double-click binding cho Shop items
- Single-click binding cho preview

## 12. Tác giả và mục đích

Dự án phục vụ học tập và trình bày năng lực:

- **Kiến trúc phân tầng OOP**: Presentation → Session → Networking → Domain
- **Network programming**: TCP (reliable gameplay), UDP (discovery)
- **Async/await pattern**: UI responsive khi I/O blocking
- **Dispatcher marshaling**: UI-safe event handling từ worker threads
- **Turn-based game design**: P2P state sync, phase management
- **MVVM + WPF**: Command binding, observable properties
- **Testability**: Interface-driven design (mock-friendly)
- **Scalability**: Easy to extend (economy, shop, persistence)
