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

## 9. Trạng thái hiện tại của repository

Repository hiện chứa source modules theo kiến trúc OOP và XAML/ViewModel mẫu cho networking flow.
Nếu bạn muốn chạy thành executable đầy đủ, hãy tích hợp các module trong solution WPF chính của bạn (MainWindow/App startup) và map board UI theo gameplay logic.

## 10. Định hướng mở rộng

1. Thêm board UI interactive 10x10 (drag/drop fleet placement).
2. Thêm save/load state và reconnect resume game.
3. Thêm unit test cho protocol parser và session coordinator.
4. Thêm security layer (validation/chống packet giả).

## 11. Tác giả và mục đích

Dự án phục vụ học tập và trình bày năng lực:

- Kiến trúc phân tầng OOP
- Network programming trong C#
- Xử lý bất đồng bộ và đồng bộ UI WPF
- Thiết kế game turn-based P2P
