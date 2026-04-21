using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Battleship2D.Networking;
using Battleship2D.Networking.Abstractions;
using Battleship2D.Networking.Protocol;

namespace Battleship2D.Game.Session;

/// <summary>
/// Dieu phoi vong doi tran dau o muc session:
/// Placement -> Ready sync -> Combat turn-based -> Finished.
/// Lop nay tap trung vao flow va dong bo message, khong chua UI controls.
/// </summary>
public sealed class GameSessionCoordinator : IGameSessionCoordinator
{
    private readonly INetworkManager _networkManager;
    private readonly IGameProtocol _protocol;
    private readonly IGameCombatStateAdapter _combatAdapter;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private bool _awaitingShotResult;

    public GameSessionCoordinator(
        INetworkManager networkManager,
        IGameProtocol protocol,
        IGameCombatStateAdapter combatAdapter,
        Dispatcher? dispatcher = null)
    {
        _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
        _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        _combatAdapter = combatAdapter ?? throw new ArgumentNullException(nameof(combatAdapter));
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _networkManager.MessageReceived += OnNetworkMessageReceived;
        _networkManager.ConnectionStateChanged += OnNetworkConnectionStateChanged;
    }

    public event EventHandler<SessionPhaseChangedEventArgs>? PhaseChanged;
    public event EventHandler<TurnChangedEventArgs>? TurnChanged;
    public event EventHandler<ShotResolvedEventArgs>? IncomingShotResolved;
    public event EventHandler<ShotResolvedEventArgs>? OutgoingShotResolved;
    public event EventHandler<GameEndedEventArgs>? GameEnded;

    public SessionPhase Phase { get; private set; } = SessionPhase.Placement;
    public bool IsLocalReady { get; private set; }
    public bool IsRemoteReady { get; private set; }
    public bool IsLocalTurn { get; private set; }

    /// <summary>
    /// Dua session ve trang thai san sang cho tran moi (Placement, chua Ready, chua den luot ai).
    /// Ham nay khong dong/mo ket noi mang, chi reset state phien trong coordinator.
    /// </summary>
    public async Task ResetSessionAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IsLocalReady = false;
            IsRemoteReady = false;
            IsLocalTurn = false;
            _awaitingShotResult = false;
            SetPhaseNoLock(SessionPhase.Placement);
            RaiseTurnChanged(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Local player xac nhan da dat doi hinh xong.
    /// Ham gui READY ngay lap tuc va neu ca hai ben da san sang thi tu dong vao Combat.
    /// </summary>
    public async Task ConfirmReadyAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Phase == SessionPhase.Finished)
            {
                throw new InvalidOperationException("Session already finished.");
            }

            IsLocalReady = true;
            _awaitingShotResult = false;
            SetPhaseNoLock(SessionPhase.AwaitingReady);
        }
        finally
        {
            _stateLock.Release();
        }

        await _networkManager.SendMessageAsync(_protocol.BuildReady(), cancellationToken).ConfigureAwait(false);
        await TryEnterCombatAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gui lenh FIRE den doi thu o che do Combat.
    /// Co co che chan double-shot bang _awaitingShotResult de moi luot chi co 1 phat hop le.
    /// </summary>
    public async Task FireAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Phase != SessionPhase.Combat)
            {
                throw new InvalidOperationException("Cannot fire before combat phase.");
            }

            if (!IsLocalTurn)
            {
                throw new InvalidOperationException("Not your turn.");
            }

            if (_awaitingShotResult)
            {
                throw new InvalidOperationException("Waiting for previous shot result.");
            }

            _awaitingShotResult = true;
        }
        finally
        {
            _stateLock.Release();
        }

        var fireMessage = _protocol.BuildFire(x, y);
        await _networkManager.SendMessageAsync(fireMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ket thuc tran dau, gui END den peer va dong state ve Finished.
    /// </summary>
    public async Task EndGameAsync(bool isWinner, CancellationToken cancellationToken = default)
    {
        await _networkManager.SendMessageAsync(_protocol.BuildEnd(isWinner), cancellationToken).ConfigureAwait(false);

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IsLocalTurn = false;
            _awaitingShotResult = false;
            SetPhaseNoLock(SessionPhase.Finished);
            RaiseTurnChanged(false);
            RaiseGameEnded(isWinner, "Local game end triggered.");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Event bridge tu NetworkManager.
    /// Dung async-void hop ly cho event handler va day logic that su sang ham async rieng.
    /// </summary>
    private async void OnNetworkMessageReceived(object? sender, Networking.Events.MessageReceivedEventArgs e)
    {
        try
        {
            await HandleIncomingMessageAsync(e.Message).ConfigureAwait(false);
        }
        catch
        {
            // Loi parse hoac state da duoc bo qua de session khong bi sap vi 1 packet loi.
        }
    }

    private async void OnNetworkConnectionStateChanged(object? sender, Networking.Events.ConnectionStateChangedEventArgs e)
    {
        if (e.State != ConnectionState.Disconnected)
        {
            return;
        }

        try
        {
            await ResetSessionAsync().ConfigureAwait(false);
        }
        catch
        {
            // Bo qua loi reset de khong anh huong qua trinh dong ket noi.
        }
    }

    /// <summary>
    /// Xu ly message den theo protocol va cap nhat state phien.
    /// Moi case duoc tach ro de de mo rong command sau nay.
    /// </summary>
    private async Task HandleIncomingMessageAsync(string raw)
    {
        if (!_protocol.TryParse(raw, out var message) || message is null)
        {
            return;
        }

        switch (message.Type)
        {
            case GameMessageType.Ready:
                await HandleReadyAsync(message).ConfigureAwait(false);
                break;

            case GameMessageType.Fire:
                await HandleIncomingFireAsync(message).ConfigureAwait(false);
                break;

            case GameMessageType.Result:
                await HandleResultAsync(message).ConfigureAwait(false);
                break;

            case GameMessageType.End:
                await HandleEndAsync(message).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Khi nhan READY tu peer, danh dau remote da san sang va thu vao combat neu du dieu kien.
    /// </summary>
    private async Task HandleReadyAsync(GameMessage message)
    {
        if (message.Ready != true)
        {
            return;
        }

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Phase == SessionPhase.Finished)
            {
                return;
            }

            IsRemoteReady = true;
            if (Phase == SessionPhase.Placement)
            {
                SetPhaseNoLock(SessionPhase.AwaitingReady);
            }
        }
        finally
        {
            _stateLock.Release();
        }

        await TryEnterCombatAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Xu ly lenh FIRE tu peer:
    /// 1) Goi combat adapter de tinh ket qua tren board local.
    /// 2) Gui RESULT tra ve.
    /// 3) Chuyen luot ve local player.
    /// </summary>
    private async Task HandleIncomingFireAsync(GameMessage message)
    {
        if (message.X is null || message.Y is null)
        {
            return;
        }

        ShotOutcome outcome;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Phase != SessionPhase.Combat)
            {
                return;
            }
        }
        finally
        {
            _stateLock.Release();
        }

        outcome = await _combatAdapter.ResolveIncomingShotAsync(message.X.Value, message.Y.Value).ConfigureAwait(false);
        RaiseIncomingShotResolved(message.X.Value, message.Y.Value, outcome);

        var resultMessage = _protocol.BuildResult(message.X.Value, message.Y.Value, outcome);
        await _networkManager.SendMessageAsync(resultMessage).ConfigureAwait(false);

        if (_combatAdapter.AreAllLocalShipsDestroyed())
        {
            await EndGameAsync(isWinner: false).ConfigureAwait(false);
            return;
        }

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            IsLocalTurn = true;
            _awaitingShotResult = false;
            RaiseTurnChanged(true);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Xu ly RESULT tra ve cho phat ban local:
    /// 1) Cap nhat board doi thu ban sao local qua adapter.
    /// 2) Ket thuc luot local, chuyen luot doi thu.
    /// </summary>
    private async Task HandleResultAsync(GameMessage message)
    {
        if (message.X is null || message.Y is null || message.Outcome is null)
        {
            return;
        }

        await _combatAdapter
            .ApplyOutgoingShotResultAsync(message.X.Value, message.Y.Value, message.Outcome.Value)
            .ConfigureAwait(false);

        RaiseOutgoingShotResolved(message.X.Value, message.Y.Value, message.Outcome.Value);

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _awaitingShotResult = false;
            IsLocalTurn = false;
            RaiseTurnChanged(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Xu ly thong diep END den tu peer, dong phien ve Finished.
    /// </summary>
    private async Task HandleEndAsync(GameMessage message)
    {
        var remoteWinner = message.Winner ?? false;
        var localWinner = !remoteWinner;

        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _awaitingShotResult = false;
            IsLocalTurn = false;
            SetPhaseNoLock(SessionPhase.Finished);
            RaiseTurnChanged(false);
            RaiseGameEnded(localWinner, "Remote peer ended the game.");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Kiem tra dieu kien vao Combat: can local + remote deu READY.
    /// Host di truoc, Client di sau de tranh conflict luot dau.
    /// </summary>
    private async Task TryEnterCombatAsync(CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsLocalReady || !IsRemoteReady || Phase == SessionPhase.Finished)
            {
                return;
            }

            SetPhaseNoLock(SessionPhase.Combat);
            IsLocalTurn = _networkManager.Role == NetworkRole.Host;
            _awaitingShotResult = false;
            RaiseTurnChanged(IsLocalTurn);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Cap nhat phase ma khong lock ben trong.
    /// Goi tu cac ham da nam trong critical section de tranh lock long nhau.
    /// </summary>
    private void SetPhaseNoLock(SessionPhase newPhase)
    {
        if (Phase == newPhase)
        {
            return;
        }

        var old = Phase;
        Phase = newPhase;
        RaisePhaseChanged(old, newPhase);
    }

    /// <summary>
    /// Phat su kien phase change tren UI thread.
    /// </summary>
    private void RaisePhaseChanged(SessionPhase oldPhase, SessionPhase newPhase)
    {
        var args = new SessionPhaseChangedEventArgs(oldPhase, newPhase);
        PostToUiThread(() => PhaseChanged?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien doi luot choi tren UI thread.
    /// </summary>
    private void RaiseTurnChanged(bool isLocalTurn)
    {
        var args = new TurnChangedEventArgs(isLocalTurn);
        PostToUiThread(() => TurnChanged?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien ket qua phat ban cua doi thu tren board local.
    /// </summary>
    private void RaiseIncomingShotResolved(int x, int y, ShotOutcome outcome)
    {
        var args = new ShotResolvedEventArgs(x, y, outcome);
        PostToUiThread(() => IncomingShotResolved?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien ket qua phat ban cua local len board doi thu.
    /// </summary>
    private void RaiseOutgoingShotResolved(int x, int y, ShotOutcome outcome)
    {
        var args = new ShotResolvedEventArgs(x, y, outcome);
        PostToUiThread(() => OutgoingShotResolved?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien ket thuc tran dau tren UI thread.
    /// </summary>
    private void RaiseGameEnded(bool isLocalWinner, string reason)
    {
        var args = new GameEndedEventArgs(isLocalWinner, reason);
        PostToUiThread(() => GameEnded?.Invoke(this, args));
    }

    /// <summary>
    /// Dua callback ve UI thread, bao toan tinh on dinh khi update WPF binding.
    /// </summary>
    private void PostToUiThread(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = _dispatcher.InvokeAsync(action);
    }

    /// <summary>
    /// Huy dang ky event va giai phong lock.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _networkManager.MessageReceived -= OnNetworkMessageReceived;
        _networkManager.ConnectionStateChanged -= OnNetworkConnectionStateChanged;
        _stateLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
