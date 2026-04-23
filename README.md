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

## 9. Trạng thái hiện tại - ĐÃ HOÀN THÀNH

### Networking Layer
- TCP Manager: Kết nối bất đồng bộ, retry với backoff, timeout, auto-reconnect
- UDP Discovery: Host announcement, client discovery trong LAN
- Protocol: Text-based message parser (READY|1, FIRE|x|y, RESULT|x|y|outcome, END|WIN/LOSE)
- Event system: Tất cả callback được marshal qua Dispatcher (UI-safe)
- Resilience: ConnectTimeout, RetryCount, SendTimeout, AutoReconnect policy

### Game Session Layer
- Phase management: Placement → AwaitingReady → Combat → Finished
- Turn-based logic: Local turn + remote turn tracking
- Session reset: Full state cleanup (board, ready flags, turn)
- Message orchestration: READY sync → Combat logic → END resolution

### Domain/Board Layer
- SampleGameCombatStateAdapter: Hit/Miss/Sunk resolution
- Enemy shadow board: Tracking kết quả bản gửi đi
- Local board: Hit tracking, sunk detection
- Board validation: Coordinate bounds checking

### Presentation Layer
- MainGameViewModel: Orchestrating tất cả service layers
- BoardCellViewModel: 10x10 grid cells (local + enemy)
- FleetAssetViewModel: 6 ships hiển thị (Carrier, Destroyer, Cruiser, Frigate, Scout, Battleship)
- DiscoveredHostViewModel: Host discovery list binding
- Commands: AsyncRelayCommand cho Start Host, Connect, Ready, Fire, Stop All
- Status display: Connection state, session phase, turn indicator

### Infrastructure
- ShipCatalog: Centralized config (name, length, asset path)
- Asset loading: Runtime path resolution từ bin/net8.0-windows/Assets
- run-two-instances.ps1: Fresh rebuild + dual-instance launcher
- .csproj: Assets auto-copy to output (PreserveNewest)
- Dispatcher integration: All events posted safely to UI thread

### Test & Validation
- 2-instance demo script
- Placement → Combat → End-to-end flow testable
- Reconnect scenario testable (Stop/Start)

## 10. Chuẩn bị cho mở rộng Phase 2

### Economy System (Chưa có)
- PlayerEconomy model (Credits, RoundIncome, Spent)
- Score calculation (Miss=0, Hit=+10, Sunk=+25, Win bonus=+50)
- Credit accumulation per turn/round

### Shop + Loadout System (Chưa có)
- ShopItem model (Id, Name, Cost, Category, AssetPath, MaxPerMatch)
- ShipLoadout model (ShipName + weapon list)
- BudgetRules (StartBudget, MaxPerShip, MaxPerMatch)
- OwnedUpgrade tracking
- WeaponCatalog mapping

### Protocol Extension (Chưa có)
- CREDITS|amount (update credit sau mỗi shot)
- LOADOUT|shipName|weaponIds (sync loadout trước Combat)
- ECONOMY|credits|spent (economy state sync)

### Session Phase Extension (Chưa có)
- PreBattleBuy phase (mua vật phẩm)
- LoadoutAssign phase (gắn weapon vào tàu)
- PostMatchRewards phase (tính điểm + bonus)

### UI Panels (Chưa có)
- Shop panel: item list + Buy button + cost display
- Budget indicator: RemainingBudget bar
- Loadout panel: tàu + weapon đã gắn
- Reward screen: show điểm earned
- Economy dashboard: total credits, spent, remaining

## 11. Tác giả và mục đích

Dự án phục vụ học tập và trình bày năng lực:

- **Kiến trúc phân tầng OOP**: Presentation → Session → Networking → Domain
- **Network programming**: TCP (reliable gameplay), UDP (discovery)
- **Async/await pattern**: UI responsive khi I/O blocking
- **Dispatcher marshaling**: UI-safe event handling từ worker threads
- **Turn-based game design**: P2P state sync, phase management
- **MVVM + WPF**: Command binding, observable properties
- **Testability**: Interface-driven design (mock-friendly)
- **Scalability**: Easy to extend (economy, shop, persistence)
