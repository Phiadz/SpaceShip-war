using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using Battleship2D.Game.Board;
using Battleship2D.Game.Session;
using Battleship2D.Networking;
using Battleship2D.Networking.Abstractions;
using Battleship2D.Networking.Discovery;
using Battleship2D.Networking.Events;
using Battleship2D.Networking.Protocol;
using Battleship2D.Presentation.Commands;

namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// ViewModel chinh cua man hinh game: networking + placement + combat UI.
/// </summary>
public sealed class MainGameViewModel : ObservableObject, IAsyncDisposable
{
    private const string AssetsRoot = "Assets";
    private const string AssetPackFolder = "space_breaker_asset";
    private const string ShipsFolder = "Ships";

    // =========================
    // S U A   O   D A Y
    // =========================
    // Day la bang cau hinh tau duy nhat cho ca Placement Controls va Fleet Assets.
    // - PlacementLength co gia tri (2..5): tau xuat hien trong o chon dat tau.
    // - PlacementLength = null: chi hien thi trong Fleet Assets, khong dat duoc tren board.
    // - AssetPathParts: duong dan TU THU MUC OUTPUT (bin/.../net8.0-windows).
    //   Vi du: Assets/space_breaker_asset/Ships/Small/body_01.png
    private static readonly (string Name, int? PlacementLength, string[] AssetPathParts)[] ShipCatalog =
    {
        ("Carrier", 5, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Big", "body_03.png" }),
        ("Destroyer", 4, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Medium", "body_02.png" }),
        ("Cruiser", 3, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Medium", "body_03.png" }),
        ("Frigate", 3, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Small", "body_02.png" }),
        ("Scout", 2, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Small", "body_01.png" }),
        ("Battleship", 5, new[] { AssetsRoot, AssetPackFolder, ShipsFolder, "Big", "body_02.png" })
    };

    private readonly INetworkManager _networkManager;
    private readonly IHostDiscoveryService _discoveryService;
    private readonly IGameSessionCoordinator _sessionCoordinator;
    private readonly SampleGameCombatStateAdapter _combatAdapter;
    private readonly Random _random = new();
    private readonly List<HashSet<(int X, int Y)>> _placedShips = new();

    private string _hostIp = "127.0.0.1";
    private int _hostPort = 5050;
    private int _discoveryPort = 7777;
    private string _playerName = Environment.MachineName;
    private string _connectionStatus = "Disconnected";
    private string _sessionStatus = "Placement";
    private string _lastEvent = "Ready";
    private string _turnHint = "Placement phase: choose ship, rotate, then click local board.";
    private string _gameResult = "In Progress";
    private bool _isMyTurn;
    private bool _isBusy;
    private bool _isPlacementHorizontal = true;
    private PlacementShipViewModel? _selectedPlacementShip;

    public MainGameViewModel(
        INetworkManager networkManager,
        IHostDiscoveryService discoveryService,
        IGameSessionCoordinator sessionCoordinator,
        SampleGameCombatStateAdapter combatAdapter)
    {
        _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _sessionCoordinator = sessionCoordinator ?? throw new ArgumentNullException(nameof(sessionCoordinator));
        _combatAdapter = combatAdapter ?? throw new ArgumentNullException(nameof(combatAdapter));

        _networkManager.ConnectionStateChanged += OnConnectionStateChanged;
        _networkManager.NetworkError += OnNetworkError;
        _networkManager.ReconnectAttempted += OnReconnectAttempted;
        _discoveryService.HostDiscovered += OnHostDiscovered;
        _discoveryService.DiscoveryError += OnDiscoveryError;
        _sessionCoordinator.PhaseChanged += OnPhaseChanged;
        _sessionCoordinator.TurnChanged += OnTurnChanged;
        _sessionCoordinator.IncomingShotResolved += OnIncomingShotResolved;
        _sessionCoordinator.OutgoingShotResolved += OnOutgoingShotResolved;
        _sessionCoordinator.GameEnded += OnGameEnded;

        StartHostCommand = new AsyncRelayCommand(StartHostAsync, () => !_isBusy);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !_isBusy);
        StartDiscoveryListenCommand = new AsyncRelayCommand(StartDiscoveryListenAsync, () => !_isBusy);
        StartHostAnnouncementCommand = new AsyncRelayCommand(StartHostAnnouncementAsync, () => !_isBusy);
        ConfirmReadyCommand = new AsyncRelayCommand(ConfirmReadyAsync, CanConfirmReady);
        FireRandomCommand = new AsyncRelayCommand(FireRandomAsync, () => !_isBusy && _sessionCoordinator.IsLocalTurn);
        StopAllCommand = new AsyncRelayCommand(StopAllAsync, () => !_isBusy);
        RotatePlacementCommand = new RelayCommand(RotatePlacement, CanEditPlacement);
        ResetPlacementCommand = new RelayCommand(ResetPlacement, CanEditPlacement);

        BuildBoards();
        LoadFleetAssets();
        InitializePlacementShips();
        ResetPlacement();
    }

    public static MainGameViewModel CreateDefault()
    {
        var networkOptions = new NetworkManagerOptions
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectRetryCount = 2,
            RetryBackoffBase = TimeSpan.FromMilliseconds(800),
            SendTimeout = TimeSpan.FromSeconds(4),
            AutoReconnectEnabled = true,
            AutoReconnectMaxAttempts = 5,
            AutoReconnectInitialDelay = TimeSpan.FromSeconds(1)
        };

        var network = new NetworkManager(options: networkOptions);
        var protocol = new GameProtocol();
        var discovery = new HostDiscoveryService();
        var combat = new SampleGameCombatStateAdapter();
        var session = new GameSessionCoordinator(network, protocol, combat);
        return new MainGameViewModel(network, discovery, session, combat);
    }

    public ObservableCollection<DiscoveredHostViewModel> DiscoveredHosts { get; } = new();
    public ObservableCollection<BoardCellViewModel> LocalBoardCells { get; } = new();
    public ObservableCollection<BoardCellViewModel> EnemyBoardCells { get; } = new();
    public ObservableCollection<FleetAssetViewModel> FleetAssets { get; } = new();
    public ObservableCollection<PlacementShipViewModel> PlacementShips { get; } = new();

    public AsyncRelayCommand StartHostCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand StartDiscoveryListenCommand { get; }
    public AsyncRelayCommand StartHostAnnouncementCommand { get; }
    public AsyncRelayCommand ConfirmReadyCommand { get; }
    public AsyncRelayCommand FireRandomCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }
    public RelayCommand RotatePlacementCommand { get; }
    public RelayCommand ResetPlacementCommand { get; }

    public string HostIp
    {
        get => _hostIp;
        set => SetProperty(ref _hostIp, value);
    }

    public int HostPort
    {
        get => _hostPort;
        set => SetProperty(ref _hostPort, value);
    }

    public int DiscoveryPort
    {
        get => _discoveryPort;
        set => SetProperty(ref _discoveryPort, value);
    }

    public string PlayerName
    {
        get => _playerName;
        set => SetProperty(ref _playerName, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string SessionStatus
    {
        get => _sessionStatus;
        private set => SetProperty(ref _sessionStatus, value);
    }

    public string LastEvent
    {
        get => _lastEvent;
        private set => SetProperty(ref _lastEvent, value);
    }

    public string TurnHint
    {
        get => _turnHint;
        private set => SetProperty(ref _turnHint, value);
    }

    public string GameResult
    {
        get => _gameResult;
        private set => SetProperty(ref _gameResult, value);
    }

    public bool IsMyTurn
    {
        get => _isMyTurn;
        private set
        {
            if (SetProperty(ref _isMyTurn, value))
            {
                RefreshEnemyBoardClickability();
                FireRandomCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsPlacementHorizontal
    {
        get => _isPlacementHorizontal;
        private set
        {
            if (SetProperty(ref _isPlacementHorizontal, value))
            {
                OnPropertyChanged(nameof(PlacementOrientationText));
            }
        }
    }

    public string PlacementOrientationText => IsPlacementHorizontal ? "Horizontal" : "Vertical";

    public PlacementShipViewModel? SelectedPlacementShip
    {
        get => _selectedPlacementShip;
        set
        {
            if (SetProperty(ref _selectedPlacementShip, value))
            {
                RefreshLocalBoardClickability();
            }
        }
    }

    public bool IsPlacementComplete => PlacementShips.All(s => s.IsPlaced);

    private bool CanConfirmReady()
    {
        return !_isBusy &&
               _networkManager.IsConnected &&
               _sessionCoordinator.Phase is SessionPhase.Placement or SessionPhase.AwaitingReady &&
               IsPlacementComplete;
    }

    private bool CanEditPlacement()
    {
        return !_isBusy && _sessionCoordinator.Phase == SessionPhase.Placement && !_sessionCoordinator.IsLocalReady;
    }

    private void BuildBoards()
    {
        LocalBoardCells.Clear();
        EnemyBoardCells.Clear();

        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                var localCell = new BoardCellViewModel(x, y, isEnemyBoard: false, onFireAsync: OnLocalCellPlacementAsync);
                LocalBoardCells.Add(localCell);

                var enemyCell = new BoardCellViewModel(x, y, isEnemyBoard: true, onFireAsync: OnEnemyCellFireAsync);
                EnemyBoardCells.Add(enemyCell);
            }
        }

        RefreshLocalBoardClickability();
        RefreshEnemyBoardClickability();
    }

    private void InitializePlacementShips()
    {
        PlacementShips.Clear();

        // Chi lay tau co PlacementLength de dua vao combobox dat tau.
        foreach (var ship in ShipCatalog.Where(s => s.PlacementLength.HasValue))
        {
            PlacementShips.Add(new PlacementShipViewModel(ship.Name, ship.PlacementLength!.Value));
        }

        SelectedPlacementShip = PlacementShips.FirstOrDefault();
    }

    private void LoadFleetAssets()
    {
        FleetAssets.Clear();

        // Fleet Assets se hien thi tat ca tau co hinh ton tai trong ShipCatalog.
        foreach (var ship in ShipCatalog)
        {
            AddFleetAssetIfExists(ship.Name, ship.AssetPathParts);
        }
    }

    private void AddFleetAssetIfExists(string name, params string[] pathParts)
    {
        var fullPath = Path.Combine(new[] { AppContext.BaseDirectory }.Concat(pathParts).ToArray());
        if (File.Exists(fullPath))
        {
            FleetAssets.Add(new FleetAssetViewModel(name, fullPath));
        }
    }

    private async Task OnLocalCellPlacementAsync(BoardCellViewModel cell)
    {
        try
        {
            PlaceSelectedShip(cell.X, cell.Y);
        }
        catch (Exception ex)
        {
            LastEvent = $"Placement error: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private void PlaceSelectedShip(int startX, int startY)
    {
        if (!CanEditPlacement())
        {
            return;
        }

        if (SelectedPlacementShip is null || SelectedPlacementShip.IsPlaced)
        {
            LastEvent = "Select an unplaced ship first.";
            return;
        }

        var candidateCells = BuildPlacementCells(startX, startY, SelectedPlacementShip.Length, IsPlacementHorizontal);
        if (candidateCells is null)
        {
            LastEvent = "Invalid placement: ship goes out of board.";
            return;
        }

        if (_placedShips.Any(ship => ship.Overlaps(candidateCells)))
        {
            LastEvent = "Invalid placement: ships cannot overlap.";
            return;
        }

        var shipSet = new HashSet<(int X, int Y)>(candidateCells);
        _placedShips.Add(shipSet);
        foreach (var pos in shipSet)
        {
            LocalBoardCells.First(c => c.X == pos.X && c.Y == pos.Y).SetLocalShipPresent(true);
        }

        SelectedPlacementShip.IsPlaced = true;
        SelectedPlacementShip = PlacementShips.FirstOrDefault(s => !s.IsPlaced);

        OnPropertyChanged(nameof(IsPlacementComplete));
        ConfirmReadyCommand.NotifyCanExecuteChanged();
        RefreshLocalBoardClickability();

        LastEvent = SelectedPlacementShip is null
            ? "Fleet placement complete. You can press READY now."
            : $"Placed ship length {shipSet.Count}. Continue placing remaining ships.";
    }

    private static List<(int X, int Y)>? BuildPlacementCells(int startX, int startY, int length, bool horizontal)
    {
        var cells = new List<(int X, int Y)>();
        for (var i = 0; i < length; i++)
        {
            var x = horizontal ? startX + i : startX;
            var y = horizontal ? startY : startY + i;

            if (x < 0 || x > 9 || y < 0 || y > 9)
            {
                return null;
            }

            cells.Add((x, y));
        }

        return cells;
    }

    private void RotatePlacement()
    {
        IsPlacementHorizontal = !IsPlacementHorizontal;
        LastEvent = $"Placement orientation: {PlacementOrientationText}";
    }

    private void ResetPlacement()
    {
        _placedShips.Clear();

        foreach (var ship in PlacementShips)
        {
            ship.IsPlaced = false;
        }

        foreach (var cell in LocalBoardCells)
        {
            cell.SetLocalShipPresent(false);
            cell.SetClickable(false);
        }

        SelectedPlacementShip = PlacementShips.FirstOrDefault();
        OnPropertyChanged(nameof(IsPlacementComplete));
        RefreshLocalBoardClickability();
        ConfirmReadyCommand.NotifyCanExecuteChanged();
        LastEvent = "Placement reset. Place your fleet again.";
    }

    private void RefreshLocalBoardClickability()
    {
        var canPlace = CanEditPlacement() && SelectedPlacementShip is not null && !SelectedPlacementShip.IsPlaced;
        foreach (var cell in LocalBoardCells)
        {
            cell.SetClickable(canPlace);
        }
    }

    private void RefreshEnemyBoardClickability()
    {
        var canFire = !_isBusy && _sessionCoordinator.Phase == SessionPhase.Combat && _sessionCoordinator.IsLocalTurn;
        foreach (var cell in EnemyBoardCells)
        {
            var known = _combatAdapter.GetKnownEnemyCellResult(cell.X, cell.Y);
            cell.SetEnemyClickable(canFire && known is null);
        }
    }

    private async Task StartHostAsync()
    {
        await RunBusyAsync(async () =>
        {
            await PrepareFreshMatchStateAsync();
            LastEvent = "Host listening...";
            await _networkManager.StartHostAsync(HostPort);
        });
    }

    private async Task ConnectAsync()
    {
        await RunBusyAsync(async () =>
        {
            await PrepareFreshMatchStateAsync();
            LastEvent = $"Connecting to {HostIp}:{HostPort}...";
            await _networkManager.ConnectToHostAsync(HostIp, HostPort);
        });
    }

    private async Task StartDiscoveryListenAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = "Discovery listening started.";
            await _discoveryService.StartListeningAsync(DiscoveryPort);
        });
    }

    private async Task StartHostAnnouncementAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = "Host announcement started.";
            await _discoveryService.StartHostAnnouncementAsync(PlayerName, HostPort, DiscoveryPort, TimeSpan.FromSeconds(1));
        });
    }

    private async Task ConfirmReadyAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!IsPlacementComplete)
            {
                LastEvent = "Place all ships before READY.";
                return;
            }

            _combatAdapter.ApplyCustomFleet(_placedShips.Select(s => (IReadOnlyCollection<(int X, int Y)>)s).ToList());
            await _sessionCoordinator.ConfirmReadyAsync();
            LastEvent = "READY sent. Waiting opponent...";
            TurnHint = "Waiting both players READY...";
        });
    }

    private async Task OnEnemyCellFireAsync(BoardCellViewModel cell)
    {
        await RunBusyAsync(async () =>
        {
            await _sessionCoordinator.FireAsync(cell.X, cell.Y);
            LastEvent = $"FIRE sent: ({cell.X},{cell.Y})";
        });
    }

    private async Task FireRandomAsync()
    {
        await RunBusyAsync(async () =>
        {
            var target = PickRandomUnknownEnemyCell();
            await _sessionCoordinator.FireAsync(target.X, target.Y);
            LastEvent = $"FIRE sent: ({target.X},{target.Y})";
        });
    }

    private async Task StopAllAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _discoveryService.StopAsync();
            await _networkManager.StopAsync();
            await PrepareFreshMatchStateAsync();
            LastEvent = "All services stopped. Session reset for a new match.";
        });
    }

    private async Task PrepareFreshMatchStateAsync()
    {
        await _sessionCoordinator.ResetSessionAsync();
        _combatAdapter.ResetForNewMatch();
        ResetBoardsForNewMatch();
        ResetPlacement();

        GameResult = "In Progress";
        SessionStatus = SessionPhase.Placement.ToString();
        TurnHint = "Placement phase: choose ship, rotate, then click local board.";
        IsMyTurn = false;
    }

    private void ResetBoardsForNewMatch()
    {
        foreach (var cell in LocalBoardCells)
        {
            cell.SetLocalShipPresent(false);
            cell.SetClickable(false);
        }

        foreach (var cell in EnemyBoardCells)
        {
            // Dung lai API reset mau/marker de xoa dau vet hit/miss cua tran cu.
            cell.SetLocalShipPresent(false);
            cell.SetEnemyClickable(false);
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LastEvent = $"Error: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        RefreshLocalBoardClickability();
        RefreshEnemyBoardClickability();

        StartHostCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        StartDiscoveryListenCommand.NotifyCanExecuteChanged();
        StartHostAnnouncementCommand.NotifyCanExecuteChanged();
        ConfirmReadyCommand.NotifyCanExecuteChanged();
        FireRandomCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
        RotatePlacementCommand.NotifyCanExecuteChanged();
        ResetPlacementCommand.NotifyCanExecuteChanged();
    }

    private (int X, int Y) PickRandomUnknownEnemyCell()
    {
        var unknownCells =
            from x in Enumerable.Range(0, 10)
            from y in Enumerable.Range(0, 10)
            where _combatAdapter.GetKnownEnemyCellResult(x, y) is null
            select (X: x, Y: y);

        var all = unknownCells.ToArray();
        if (all.Length == 0)
        {
            throw new InvalidOperationException("No unknown target left.");
        }

        return all[_random.Next(all.Length)];
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        ConnectionStatus = $"{e.State} ({e.Role})";
        LastEvent = string.IsNullOrWhiteSpace(e.PeerEndPoint)
            ? ConnectionStatus
            : $"{ConnectionStatus} - Peer: {e.PeerEndPoint}";

        ConfirmReadyCommand.NotifyCanExecuteChanged();
    }

    private void OnReconnectAttempted(object? sender, ReconnectAttemptedEventArgs e)
    {
        LastEvent = e.Success
            ? $"Reconnect success at attempt {e.AttemptNumber}/{e.MaxAttempts}."
            : $"Reconnect attempt {e.AttemptNumber}/{e.MaxAttempts} failed.";
    }

    private void OnNetworkError(object? sender, NetworkErrorEventArgs e)
    {
        LastEvent = $"Network error at {e.Operation}: {e.Exception.Message}";
    }

    private void OnHostDiscovered(object? sender, HostDiscoveredEventArgs e)
    {
        var key = $"{e.HostIp}:{e.HostPort}";
        var old = DiscoveredHosts.FirstOrDefault(h => $"{h.HostIp}:{h.HostPort}" == key);
        if (old is not null)
        {
            DiscoveredHosts.Remove(old);
        }

        DiscoveredHosts.Add(new DiscoveredHostViewModel(e.HostName, e.HostIp, e.HostPort, e.DiscoveredAtUtc.ToLocalTime().ToString("HH:mm:ss")));
        HostIp = e.HostIp;
        HostPort = e.HostPort;
        LastEvent = $"Discovered host: {e.HostName} at {e.HostIp}:{e.HostPort}";
    }

    private void OnDiscoveryError(object? sender, NetworkErrorEventArgs e)
    {
        LastEvent = $"Discovery error at {e.Operation}: {e.Exception.Message}";
    }

    private void OnPhaseChanged(object? sender, SessionPhaseChangedEventArgs e)
    {
        SessionStatus = e.NewPhase.ToString();
        TurnHint = e.NewPhase switch
        {
            SessionPhase.Placement => "Placement phase: place your fleet.",
            SessionPhase.AwaitingReady => "Awaiting both players READY...",
            SessionPhase.Combat => IsMyTurn ? "Combat: YOUR TURN" : "Combat: OPPONENT TURN",
            SessionPhase.Finished => "Game finished.",
            _ => TurnHint
        };

        RefreshLocalBoardClickability();
        RefreshEnemyBoardClickability();
        ConfirmReadyCommand.NotifyCanExecuteChanged();
        RotatePlacementCommand.NotifyCanExecuteChanged();
        ResetPlacementCommand.NotifyCanExecuteChanged();
    }

    private void OnTurnChanged(object? sender, TurnChangedEventArgs e)
    {
        IsMyTurn = e.IsLocalTurn;
        if (_sessionCoordinator.Phase == SessionPhase.Combat)
        {
            TurnHint = e.IsLocalTurn ? "Combat: YOUR TURN" : "Combat: OPPONENT TURN";
        }
    }

    private void OnIncomingShotResolved(object? sender, ShotResolvedEventArgs e)
    {
        var localCell = LocalBoardCells.FirstOrDefault(c => c.X == e.X && c.Y == e.Y);
        localCell?.ApplyIncomingOutcome(e.Outcome);
        PlayOutcomeSound(e.Outcome, outgoing: false);
        LastEvent = $"Incoming shot at ({e.X},{e.Y}) => {e.Outcome}";
    }

    private void OnOutgoingShotResolved(object? sender, ShotResolvedEventArgs e)
    {
        var enemyCell = EnemyBoardCells.FirstOrDefault(c => c.X == e.X && c.Y == e.Y);
        enemyCell?.ApplyOutgoingOutcome(e.Outcome);
        RefreshEnemyBoardClickability();
        PlayOutcomeSound(e.Outcome, outgoing: true);
        LastEvent = $"Outgoing shot at ({e.X},{e.Y}) => {e.Outcome}";
    }

    private void OnGameEnded(object? sender, GameEndedEventArgs e)
    {
        GameResult = e.IsLocalWinner ? "Victory" : "Defeat";
        TurnHint = e.IsLocalWinner ? "You win!" : "You lose!";
        LastEvent = $"Game ended: {GameResult}. {e.Reason}";
        SystemSounds.Exclamation.Play();

        RefreshLocalBoardClickability();
        RefreshEnemyBoardClickability();
    }

    private static void PlayOutcomeSound(ShotOutcome outcome, bool outgoing)
    {
        switch (outcome)
        {
            case ShotOutcome.Miss:
                SystemSounds.Beep.Play();
                break;
            case ShotOutcome.Hit:
                SystemSounds.Asterisk.Play();
                break;
            case ShotOutcome.Sunk:
                SystemSounds.Hand.Play();
                break;
            default:
                if (outgoing)
                {
                    SystemSounds.Beep.Play();
                }
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _networkManager.ConnectionStateChanged -= OnConnectionStateChanged;
        _networkManager.NetworkError -= OnNetworkError;
        _networkManager.ReconnectAttempted -= OnReconnectAttempted;
        _discoveryService.HostDiscovered -= OnHostDiscovered;
        _discoveryService.DiscoveryError -= OnDiscoveryError;
        _sessionCoordinator.PhaseChanged -= OnPhaseChanged;
        _sessionCoordinator.TurnChanged -= OnTurnChanged;
        _sessionCoordinator.IncomingShotResolved -= OnIncomingShotResolved;
        _sessionCoordinator.OutgoingShotResolved -= OnOutgoingShotResolved;
        _sessionCoordinator.GameEnded -= OnGameEnded;

        await _sessionCoordinator.DisposeAsync();
        await _discoveryService.DisposeAsync();
        await _networkManager.DisposeAsync();
    }
}
