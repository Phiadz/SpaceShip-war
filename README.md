# SpaceShip-war

2D Peer-to-Peer Sci-Fi Battleship game built with C# WPF, where classic naval ships are replaced by futuristic space fleets using pixel-art assets.

## 1. Gioi thieu de tai

Day la do an game chien thuat theo luot tren luoi 10x10, su dung mo hinh P2P (Peer-to-Peer) thay vi dedicated server.

- Mot instance dong vai tro Host va cho ket noi qua TCP.
- Mot instance dong vai tro Client va ket noi den Host.
- Client co the tu dong tim Host trong LAN thong qua UDP Broadcast Discovery.

Muc tieu chinh cua de tai:

1. Xay dung he thong networking bat dong bo on dinh cho game turn-based.
2. Dam bao UI WPF luon responsive khi ket noi, doi doi thu, gui nhan nuoc di.
3. Chia tach kien truc OOP ro rang de de mo rong, de test va de bao ve do an.

## 2. Cong nghe su dung

- C# (.NET) + WPF
- TCP (gameplay messages)
- UDP Broadcast (host discovery)
- async/await, Task, CancellationToken, SemaphoreSlim
- MVVM (ViewModel + Command)

## 3. Kien truc tong the

Project duoc tach thanh cac tang ro rang:

1. Presentation layer
- ViewModel, Commands, binding status/command cho WPF UI.

2. Application/Game Session layer
- Dieu phoi phase tran dau: Placement -> AwaitingReady -> Combat -> Finished.
- Quan ly turn va xu ly message READY/FIRE/RESULT/END.

3. Networking Infrastructure layer
- TCP connection manager, retry/timeout/reconnect policy.
- UDP discovery service (announce/listen host).
- Protocol parser/serializer cho message text.

4. Domain/Board adapter layer
- Xu ly board 10x10, hit/miss/sunk, enemy shadow board.

## 4. Cau truc thu muc

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

Message theo dang text, moi message la mot dong:

- READY|1
- FIRE|x|y
- RESULT|x|y|MISS/HIT/SUNK
- END|WIN/LOSE

Ly do chon text protocol:

1. De debug trong giai doan phat trien/do an.
2. De mo rong nhanh command moi.
3. De giai thich ro trong van dap.

## 6. Luong hoat dong chuong trinh

1. Host mo TCP listener va (tuy chon) bat dau UDP announcement.
2. Client lang nghe discovery de tim host trong LAN hoac nhap IP thu cong.
3. Client ket noi TCP den Host (co retry + timeout).
4. Hai ben xac nhan READY.
5. Vao combat turn-based:
- Ben den luot gui FIRE.
- Ben nhan tinh ket qua board local va gui RESULT.
- Ben gui cap nhat enemy shadow board, doi luot.
6. Khi ket thuc tran, gui END va dua session sang Finished.

## 7. Tinh nang networking da co

- Async hoan toan cho connect, send, receive.
- UI-safe events qua Dispatcher (khong update control tu worker thread).
- Retry connect voi backoff.
- Timeout cho connect va send.
- Auto reconnect cho client khi mat ket noi ngoai y muon.
- Event thong bao tien trinh reconnect de hien thi tren UI.

## 8. Huong dan chay demo (2 may trong cung LAN)

1. May A (Host):
- Start Host
- Start Announce
- Ready sau khi dat doi hinh

2. May B (Client):
- Listen Discovery
- Chon host tim duoc (hoac nhap IP)
- Connect
- Ready

3. Combat:
- Den luot thi ban vao toa do doi thu
- Theo doi status va event log tren UI

## 9. Trang thai hien tai cua repository

Repository hien chua source modules theo kien truc OOP va XAML/ViewModel mau cho networking flow.
Neu ban muon chay thanh executable day du, hay tich hop cac module trong solution WPF chinh cua ban (MainWindow/App startup) va map board UI theo gameplay logic.

## 10. Dinh huong mo rong

1. Them board UI interactive 10x10 (drag/drop fleet placement).
2. Them save/load state va reconnect resume game.
3. Them unit test cho protocol parser va session coordinator.
4. Them security layer (validation/chong packet gia).

## 11. Tac gia va muc dich

Du an phuc vu hoc tap va trinh bay nang luc:

- Kien truc phan tang OOP
- Network programming trong C#
- Xu ly bat dong bo va dong bo UI WPF
- Thiet ke game turn-based P2P
