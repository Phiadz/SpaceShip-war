using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Battleship2D.Networking.Abstractions;
using Battleship2D.Networking.Events;

namespace Battleship2D.Networking;

/// <summary>
/// Quan ly mot phien TCP peer-to-peer (dong vai Host hoac Client) cho game.
/// Lop nay tap trung vao 3 muc tieu:
/// 1) Tat ca I/O deu bat dong bo de khong khoa UI WPF.
/// 2) Du lieu gui/nhan theo tung dong van ban (UTF-8) de protocol de debug.
/// 3) Moi su kien tra ve UI deu duoc marshal qua Dispatcher.
/// </summary>
public sealed class NetworkManager : INetworkManager
{
    private readonly Dispatcher _dispatcher;
    private readonly NetworkManagerOptions _options;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _sessionCts;
    private Task? _receiveLoopTask;
    private volatile bool _manualStopRequested;
    private string? _lastHostIp;
    private int _lastHostPort;

    /// <summary>
    /// Khoi tao NetworkManager.
    /// Neu caller khong truyen Dispatcher, lop se uu tien lay Dispatcher cua ung dung WPF hien tai.
    /// Cach lam nay dam bao su kien mang co the cap nhat ViewModel/View an toan tren UI thread.
    /// </summary>
    /// <param name="dispatcher">Dispatcher dung de dua callback ve UI thread.</param>
    /// <param name="options">Thong so timeout/retry/reconnect cho mang.</param>
    public NetworkManager(Dispatcher? dispatcher = null, NetworkManagerOptions? options = null)
    {
        _dispatcher = dispatcher
            ?? Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;
        _options = options ?? new NetworkManagerOptions();
    }

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<NetworkErrorEventArgs>? NetworkError;
    public event EventHandler<ReconnectAttemptedEventArgs>? ReconnectAttempted;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public NetworkRole Role { get; private set; } = NetworkRole.None;
    public bool IsConnected => State == ConnectionState.Connected;

    /// <summary>
    /// Bat dau che do Host: mo TcpListener va cho peer ket noi.
    /// Ham goi StopAsync truoc de dam bao khong con session cu treo tai nguyen.
    /// Qua trinh cho Accept la bat dong bo, UI van responsive khi doi nguoi choi ben kia.
    /// </summary>
    /// <param name="port">Cong TCP Host se lang nghe.</param>
    /// <param name="cancellationToken">Token dung de huy thao tac cho ket noi.</param>
    /// <returns>Task hoan tat khi da chap nhan ket noi va khoi tao receive loop.</returns>
    public async Task StartHostAsync(int port, CancellationToken cancellationToken = default)
    {
        await StopInternalAsync(waitForReceiveLoop: true, isManualStop: true).ConfigureAwait(false);
        _manualStopRequested = false;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sessionCts = linkedCts;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();

            State = ConnectionState.Listening;
            Role = NetworkRole.Host;
            RaiseConnectionStateChanged(State, Role, null);
        }
        finally
        {
            _lifecycleLock.Release();
        }

        try
        {
            var acceptTask = _listener!.AcceptTcpClientAsync();
            var acceptedClient = await acceptTask.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            await AttachClientAsync(acceptedClient, NetworkRole.Host, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await StopInternalAsync(waitForReceiveLoop: true, isManualStop: false).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            RaiseNetworkError(ex, "StartHostAsync/Accept");
            await StopInternalAsync(waitForReceiveLoop: true, isManualStop: false).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Bat dau che do Client: ket noi den Host thong qua IP va port.
    /// Ket noi su dung ConnectAsync + cancellation de tranh treo vo thoi han khi mang co van de.
    /// Sau khi ket noi thanh cong, lop se tao stream reader/writer va receive loop nen.
    /// </summary>
    /// <param name="hostIp">Dia chi IP cua may Host.</param>
    /// <param name="port">Cong Host dang mo.</param>
    /// <param name="cancellationToken">Token dung de huy ket noi.</param>
    /// <returns>Task hoan tat khi client attach xong vao session.</returns>
    public async Task ConnectToHostAsync(string hostIp, int port, CancellationToken cancellationToken = default)
    {
        await StopInternalAsync(waitForReceiveLoop: true, isManualStop: true).ConfigureAwait(false);
        await ConnectWithRetryAsync(hostIp, port, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gui mot message van ban qua TCP.
    /// Message duoc dong goi theo dinh dang "moi message 1 dong" (WriteLine + newline) de dau nhan tach frame de dang.
    /// _sendLock duoc dung de ngan nhieu lenh gui dong thoi lam tron/lan du lieu tren stream.
    /// </summary>
    /// <param name="message">Noi dung message can gui.</param>
    /// <param name="cancellationToken">Token huy thao tac gui.</param>
    /// <returns>Task hoan tat khi du lieu da flush xong xuong stream.</returns>
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        StreamWriter? writerSnapshot;
        CancellationToken linkedToken;

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State != ConnectionState.Connected || _writer is null || _sessionCts is null)
            {
                throw new InvalidOperationException("No active TCP connection.");
            }

            writerSnapshot = _writer;
            linkedToken = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, _sessionCts.Token)
                .Token;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        await _sendLock.WaitAsync(linkedToken).ConfigureAwait(false);
        try
        {
            using var sendTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(linkedToken);
            sendTimeoutCts.CancelAfter(_options.SendTimeout);

            await writerSnapshot!.WriteLineAsync(message).WaitAsync(sendTimeoutCts.Token).ConfigureAwait(false);
            await writerSnapshot.FlushAsync().WaitAsync(sendTimeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException("Send message timed out.", ex);
            RaiseNetworkError(timeoutException, "SendMessageAsync/Timeout");
            await StopInternalAsync(waitForReceiveLoop: true, isManualStop: false).ConfigureAwait(false);
            throw timeoutException;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            RaiseNetworkError(ex, "SendMessageAsync");
            await StopInternalAsync(waitForReceiveLoop: true, isManualStop: false).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Dung phien ket noi hien tai va giai phong tai nguyen mang.
    /// Ham nay doi receive loop ket thuc de tranh race-condition khi dong stream.
    /// </summary>
    /// <returns>Task hoan tat khi session da ve trang thai Disconnected.</returns>
    public Task StopAsync()
    {
        return StopInternalAsync(waitForReceiveLoop: true, isManualStop: true);
    }

    /// <summary>
    /// Ket noi theo policy retry + timeout.
    /// Ham nay la trung tam resilience cho che do Client.
    /// </summary>
    private async Task ConnectWithRetryAsync(string hostIp, int port, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostIp))
        {
            throw new ArgumentException("Host IP cannot be empty.", nameof(hostIp));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _manualStopRequested = false;
        _lastHostIp = hostIp;
        _lastHostPort = port;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sessionCts = linkedCts;
            Role = NetworkRole.Client;
            State = ConnectionState.Disconnected;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        Exception? lastError = null;
        var totalAttempts = Math.Max(1, _options.ConnectRetryCount + 1);

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = new TcpClient();
            try
            {
                using var connectTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                connectTimeoutCts.CancelAfter(_options.ConnectTimeout);

                var connectTask = client.ConnectAsync(hostIp, port);
                await connectTask.WaitAsync(connectTimeoutCts.Token).ConfigureAwait(false);
                await AttachClientAsync(client, NetworkRole.Client, linkedCts.Token).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException ex)
                when (!cancellationToken.IsCancellationRequested && !linkedCts.Token.IsCancellationRequested)
            {
                client.Dispose();
                lastError = new TimeoutException($"Connect timeout to {hostIp}:{port}.", ex);
                RaiseNetworkError(lastError, "ConnectToHostAsync/Timeout");
            }
            catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
            {
                client.Dispose();
                lastError = ex;
                RaiseNetworkError(ex, "ConnectToHostAsync/Connect");
            }

            if (attempt < totalAttempts)
            {
                var delayMs = Math.Max(0, _options.RetryBackoffBase.TotalMilliseconds * attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), linkedCts.Token).ConfigureAwait(false);
            }
        }

        await StopInternalAsync(waitForReceiveLoop: true, isManualStop: false).ConfigureAwait(false);
        throw new IOException("Failed to connect after retry attempts.", lastError);
    }

    /// <summary>
    /// Gan TcpClient vao session hien tai (cho ca Host sau Accept va Client sau Connect).
    /// O day lop tao reader/writer UTF-8, cap nhat state Connected va khoi dong receive loop nen.
    /// </summary>
    /// <param name="tcpClient">Socket da ket noi.</param>
    /// <param name="role">Vai tro mang cua instance (Host/Client).</param>
    /// <param name="sessionToken">Token dung de dung toan bo session.</param>
    private async Task AttachClientAsync(TcpClient tcpClient, NetworkRole role, CancellationToken sessionToken)
    {
        await _lifecycleLock.WaitAsync(sessionToken).ConfigureAwait(false);
        try
        {
            _client = tcpClient;
            _client.NoDelay = true;

            _stream = _client.GetStream();
            _reader = new StreamReader(
                _stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true);

            _writer = new StreamWriter(
                _stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = false,
                NewLine = "\n"
            };

            Role = role;
            State = ConnectionState.Connected;
            RaiseConnectionStateChanged(State, Role, _client.Client.RemoteEndPoint?.ToString());

            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(sessionToken), CancellationToken.None);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Vong lap nhan du lieu nen.
    /// Moi lan ReadLineAsync doc duoc 1 message day du, giam do phuc tap tach goi tin.
    /// Khi peer dong ket noi (line == null) hoac bi huy, ham thoat va trigger stop session an toan.
    /// </summary>
    /// <param name="sessionToken">Token huy receive loop.</param>
    private async Task ReceiveLoopAsync(CancellationToken sessionToken)
    {
        var shouldAutoReconnect = false;

        try
        {
            while (!sessionToken.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync().WaitAsync(sessionToken).ConfigureAwait(false);

                if (line is null)
                {
                    // Remote peer closed gracefully.
                    break;
                }

                RaiseMessageReceived(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            RaiseNetworkError(ex, "ReceiveLoopAsync");
        }
        finally
        {
            shouldAutoReconnect =
                !_manualStopRequested &&
                Role == NetworkRole.Client &&
                _options.AutoReconnectEnabled &&
                !string.IsNullOrWhiteSpace(_lastHostIp) &&
                _lastHostPort > 0;

            await StopInternalAsync(waitForReceiveLoop: false, isManualStop: false).ConfigureAwait(false);

            if (shouldAutoReconnect)
            {
                await TryAutoReconnectAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Thu auto reconnect khi client bi dut ket noi ngoai y muon.
    /// Tien trinh moi lan thu duoc day ra event de UI hien thi trang thai phuc hoi.
    /// </summary>
    private async Task TryAutoReconnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastHostIp) || _lastHostPort <= 0)
        {
            return;
        }

        if (_options.AutoReconnectInitialDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.AutoReconnectInitialDelay).ConfigureAwait(false);
        }

        var maxAttempts = Math.Max(1, _options.AutoReconnectMaxAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (_manualStopRequested)
            {
                return;
            }

            try
            {
                await ConnectWithRetryAsync(_lastHostIp, _lastHostPort, CancellationToken.None).ConfigureAwait(false);
                RaiseReconnectAttempted(attempt, maxAttempts, success: true);
                return;
            }
            catch
            {
                RaiseReconnectAttempted(attempt, maxAttempts, success: false);
                if (attempt < maxAttempts)
                {
                    var delayMs = Math.Max(0, _options.RetryBackoffBase.TotalMilliseconds * attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs)).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Ham dung noi bo: huy token session, dung listener, dong stream/socket va reset state.
    /// Tham so waitForReceiveLoop giup tranh deadlock:
    /// - true: caller ben ngoai se doi receive loop ket thuc.
    /// - false: dung trong chinh receive loop de tranh tu cho chinh no.
    /// </summary>
    /// <param name="waitForReceiveLoop">Co doi task nhan du lieu ket thuc hay khong.</param>
    /// <param name="isManualStop">Danh dau day co phai stop chu dong tu user/app hay khong.</param>
    private async Task StopInternalAsync(bool waitForReceiveLoop, bool isManualStop)
    {
        Task? receiveTaskSnapshot;

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _manualStopRequested = isManualStop;
            _sessionCts?.Cancel();
            _listener?.Stop();
            receiveTaskSnapshot = _receiveLoopTask;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (waitForReceiveLoop && receiveTaskSnapshot is not null)
        {
            try
            {
                await receiveTaskSnapshot.ConfigureAwait(false);
            }
            catch
            {
                // Errors are surfaced via NetworkError event.
            }
        }

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
            _listener?.Stop();
            _listener = null;
            _client = null;
            _stream = null;
            _reader = null;
            _writer = null;
            _receiveLoopTask = null;
            _sessionCts?.Dispose();
            _sessionCts = null;

            if (State != ConnectionState.Disconnected || Role != NetworkRole.None)
            {
                State = ConnectionState.Disconnected;
                Role = NetworkRole.None;
                RaiseConnectionStateChanged(State, Role, null);
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Phat su kien thay doi trang thai ket noi.
    /// Su kien luon duoc dua ve UI thread de ViewModel cap nhat binding an toan.
    /// </summary>
    private void RaiseConnectionStateChanged(ConnectionState state, NetworkRole role, string? peer)
    {
        var args = new ConnectionStateChangedEventArgs(state, role, peer);
        PostToUiThread(() => ConnectionStateChanged?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien khi nhan duoc message tu peer.
    /// ReceivedAtUtc giup de log va debug do tre mang giua hai ben.
    /// </summary>
    private void RaiseMessageReceived(string message)
    {
        var args = new MessageReceivedEventArgs(message, DateTimeOffset.UtcNow);
        PostToUiThread(() => MessageReceived?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien loi mang theo ngu canh thao tac de de truy vet (vi du: Send, Receive, Connect).
    /// </summary>
    private void RaiseNetworkError(Exception exception, string operation)
    {
        var args = new NetworkErrorEventArgs(exception, operation);
        PostToUiThread(() => NetworkError?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien ket qua tung lan auto reconnect.
    /// UI co the dung event nay de thong bao trang thai phuc hoi ket noi cho nguoi choi.
    /// </summary>
    private void RaiseReconnectAttempted(int attemptNumber, int maxAttempts, bool success)
    {
        var args = new ReconnectAttemptedEventArgs(attemptNumber, maxAttempts, success);
        PostToUiThread(() => ReconnectAttempted?.Invoke(this, args));
    }

    /// <summary>
    /// Tien ich chuyen callback ve UI thread neu dang o worker thread.
    /// Day la diem then chot de tranh InvalidOperationException khi cap nhat control WPF tu thread nen.
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
    /// Giai phong tai nguyen bat dong bo cua NetworkManager.
    /// Thuc hien StopAsync truoc khi dispose lock de dam bao session dong dung thu tu.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _sendLock.Dispose();
        _lifecycleLock.Dispose();
    }
}
