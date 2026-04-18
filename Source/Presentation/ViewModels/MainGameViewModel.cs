using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
/// ViewModel mau cho WPF: ket noi cac service Network + Discovery + Session.
/// Co cac command cho Host/Client/Discovery/Ready/Fire de test end-to-end nhanh.
/// </summary>
public sealed class MainGameViewModel : ObservableObject, IAsyncDisposable
{
    private readonly INetworkManager _networkManager;
    private readonly IHostDiscoveryService _discoveryService;
    private readonly IGameSessionCoordinator _sessionCoordinator;
    private readonly SampleGameCombatStateAdapter _combatAdapter;
    private readonly Random _random = new();

    private string _hostIp = "127.0.0.1";
    private int _hostPort = 5050;
    private int _discoveryPort = 7777;
    private string _playerName = Environment.MachineName;
    private string _connectionStatus = "Disconnected";
    private string _sessionStatus = "Placement";
    private string _lastEvent = "Ready";
    private bool _isMyTurn;
    private bool _isBusy;

    /// <summary>
    /// Khoi tao ViewModel va dang ky event tu cac service.
    /// </summary>
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

        StartHostCommand = new AsyncRelayCommand(StartHostAsync, () => !_isBusy);
        ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !_isBusy);
        StartDiscoveryListenCommand = new AsyncRelayCommand(StartDiscoveryListenAsync, () => !_isBusy);
        StartHostAnnouncementCommand = new AsyncRelayCommand(StartHostAnnouncementAsync, () => !_isBusy);
        ConfirmReadyCommand = new AsyncRelayCommand(ConfirmReadyAsync, () => !_isBusy && _networkManager.IsConnected);
        FireRandomCommand = new AsyncRelayCommand(FireRandomAsync, () => !_isBusy && _sessionCoordinator.IsLocalTurn);
        StopAllCommand = new AsyncRelayCommand(StopAllAsync, () => !_isBusy);
    }

    /// <summary>
    /// Factory helper tao bo service mac dinh de demo nhanh khi chua co DI container.
    /// </summary>
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

    public AsyncRelayCommand StartHostCommand { get; }
    public AsyncRelayCommand ConnectCommand { get; }
    public AsyncRelayCommand StartDiscoveryListenCommand { get; }
    public AsyncRelayCommand StartHostAnnouncementCommand { get; }
    public AsyncRelayCommand ConfirmReadyCommand { get; }
    public AsyncRelayCommand FireRandomCommand { get; }
    public AsyncRelayCommand StopAllCommand { get; }

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

    public bool IsMyTurn
    {
        get => _isMyTurn;
        private set
        {
            if (SetProperty(ref _isMyTurn, value))
            {
                FireRandomCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Mo listener TCP de doi doi thu ket noi.
    /// Chay async nen UI khong bi tre trong luc cho Accept.
    /// </summary>
    private async Task StartHostAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = "Host listening...";
            await _networkManager.StartHostAsync(HostPort).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Ket noi den host theo IP/port nguoi dung nhap.
    /// Retry/timeout duoc thuc thi ben trong NetworkManager.
    /// </summary>
    private async Task ConnectAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = $"Connecting to {HostIp}:{HostPort}...";
            await _networkManager.ConnectToHostAsync(HostIp, HostPort).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Bat dau lang nghe UDP broadcast de tim host trong LAN.
    /// </summary>
    private async Task StartDiscoveryListenAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = "Discovery listening started.";
            await _discoveryService.StartListeningAsync(DiscoveryPort).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Bat dau phat thong bao host discovery theo chu ky.
    /// Thuong dung tren may dong vai tro Host.
    /// </summary>
    private async Task StartHostAnnouncementAsync()
    {
        await RunBusyAsync(async () =>
        {
            LastEvent = "Host announcement started.";
            await _discoveryService
                .StartHostAnnouncementAsync(PlayerName, HostPort, DiscoveryPort, TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gui READY den peer de bat dau dong bo phase vao Combat.
    /// </summary>
    private async Task ConfirmReadyAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _sessionCoordinator.ConfirmReadyAsync().ConfigureAwait(false);
            LastEvent = "READY sent.";
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Ban ngau nhien 1 toa do chua co ket qua de demo flow FIRE/RESULT nhanh.
    /// </summary>
    private async Task FireRandomAsync()
    {
        await RunBusyAsync(async () =>
        {
            var target = PickRandomUnknownEnemyCell();
            await _sessionCoordinator.FireAsync(target.X, target.Y).ConfigureAwait(false);
            LastEvent = $"FIRE sent: ({target.X},{target.Y})";
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Dung toan bo discovery + network, dua app ve trang thai idle.
    /// </summary>
    private async Task StopAllAsync()
    {
        await RunBusyAsync(async () =>
        {
            await _discoveryService.StopAsync().ConfigureAwait(false);
            await _networkManager.StopAsync().ConfigureAwait(false);
            LastEvent = "All services stopped.";
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Wrapper gom logic bat/tat busy state cho command async.
    /// Muc tieu: tranh user bam lien tiep nhieu nut gay race-condition.
    /// </summary>
    private async Task RunBusyAsync(Func<Task> action)
    {
        SetBusy(true);
        try
        {
            await action().ConfigureAwait(false);
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

    /// <summary>
    /// Cap nhat busy state va thong bao WPF reevaluate trang thai button.
    /// </summary>
    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        StartHostCommand.NotifyCanExecuteChanged();
        ConnectCommand.NotifyCanExecuteChanged();
        StartDiscoveryListenCommand.NotifyCanExecuteChanged();
        StartHostAnnouncementCommand.NotifyCanExecuteChanged();
        ConfirmReadyCommand.NotifyCanExecuteChanged();
        FireRandomCommand.NotifyCanExecuteChanged();
        StopAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Chon ngau nhien 1 o enemy board chua co ket qua de tranh ban lap.
    /// </summary>
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

    /// <summary>
    /// Cap nhat status ket noi khi NetworkManager doi state.
    /// </summary>
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        ConnectionStatus = $"{e.State} ({e.Role})";
        LastEvent = string.IsNullOrWhiteSpace(e.PeerEndPoint)
            ? ConnectionStatus
            : $"{ConnectionStatus} - Peer: {e.PeerEndPoint}";
        ConfirmReadyCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Hien thi thong tin retry reconnect len UI de nguoi choi biet app dang tu phuc hoi.
    /// </summary>
    private void OnReconnectAttempted(object? sender, ReconnectAttemptedEventArgs e)
    {
        LastEvent = e.Success
            ? $"Reconnect success at attempt {e.AttemptNumber}/{e.MaxAttempts}."
            : $"Reconnect attempt {e.AttemptNumber}/{e.MaxAttempts} failed.";
    }

    /// <summary>
    /// Ghi log loi network de nguoi choi/co van dap de truy vet de dang.
    /// </summary>
    private void OnNetworkError(object? sender, NetworkErrorEventArgs e)
    {
        LastEvent = $"Network error at {e.Operation}: {e.Exception.Message}";
    }

    /// <summary>
    /// Khi tim thay host trong LAN, cap nhat danh sach va tu dong dien IP/Port de connect nhanh.
    /// </summary>
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

    /// <summary>
    /// Hien thi loi discovery.
    /// </summary>
    private void OnDiscoveryError(object? sender, NetworkErrorEventArgs e)
    {
        LastEvent = $"Discovery error at {e.Operation}: {e.Exception.Message}";
    }

    /// <summary>
    /// Cap nhat text phase de UI theo sat tien trinh tran dau.
    /// </summary>
    private void OnPhaseChanged(object? sender, SessionPhaseChangedEventArgs e)
    {
        SessionStatus = e.NewPhase.ToString();
        LastEvent = $"Session phase: {e.OldPhase} -> {e.NewPhase}";
    }

    /// <summary>
    /// Cap nhat co toi luot minh hay khong va refresh command ban.
    /// </summary>
    private void OnTurnChanged(object? sender, TurnChangedEventArgs e)
    {
        IsMyTurn = e.IsLocalTurn;
        LastEvent = e.IsLocalTurn ? "Your turn." : "Opponent turn.";
    }

    /// <summary>
    /// Hien thi thong diep khi doi thu ban vao board local.
    /// </summary>
    private void OnIncomingShotResolved(object? sender, ShotResolvedEventArgs e)
    {
        LastEvent = $"Incoming shot at ({e.X},{e.Y}) => {e.Outcome}";
    }

    /// <summary>
    /// Hien thi thong diep ket qua phat ban cua local.
    /// </summary>
    private void OnOutgoingShotResolved(object? sender, ShotResolvedEventArgs e)
    {
        LastEvent = $"Outgoing shot at ({e.X},{e.Y}) => {e.Outcome}";
    }

    /// <summary>
    /// Huy dang ky event va dispose cac service theo dung thu tu.
    /// </summary>
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

        await _sessionCoordinator.DisposeAsync().ConfigureAwait(false);
        await _discoveryService.DisposeAsync().ConfigureAwait(false);
        await _networkManager.DisposeAsync().ConfigureAwait(false);
    }
}
