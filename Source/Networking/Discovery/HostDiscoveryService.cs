using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Battleship2D.Networking.Abstractions;
using Battleship2D.Networking.Events;

namespace Battleship2D.Networking.Discovery;

/// <summary>
/// Cung cap co che tu dong tim Host trong LAN bang UDP broadcast.
/// Thiet ke tach rieng khoi NetworkManager de tranh tron logic TCP gameplay voi discovery.
/// </summary>
public sealed class HostDiscoveryService : IHostDiscoveryService
{
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeen = new();

    private UdpClient? _announceClient;
    private UdpClient? _listenClient;
    private CancellationTokenSource? _cts;
    private Task? _announceTask;
    private Task? _listenTask;

    public HostDiscoveryService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher
            ?? Application.Current?.Dispatcher
            ?? Dispatcher.CurrentDispatcher;
    }

    public event EventHandler<HostDiscoveredEventArgs>? HostDiscovered;
    public event EventHandler<NetworkErrorEventArgs>? DiscoveryError;

    /// <summary>
    /// Bat dau phat thong bao host theo chu ky.
    /// Message format: HOST|hostName|tcpPort|ticksUtc.
    /// Dung UDP broadcast de client trong cung subnet nhan duoc ma khong can nhap IP tay.
    /// </summary>
    public async Task StartHostAnnouncementAsync(
        string hostName,
        int hostTcpPort,
        int discoveryPort,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("Host name cannot be empty.", nameof(hostName));
        }

        if (hostTcpPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(hostTcpPort));
        }

        if (discoveryPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(discoveryPort));
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureCts(cancellationToken);

            _announceClient ??= new UdpClient { EnableBroadcast = true };
            var linkedToken = _cts!.Token;

            _announceTask ??= Task.Run(
                () => AnnouncementLoopAsync(hostName.Trim(), hostTcpPort, discoveryPort, interval, linkedToken),
                CancellationToken.None);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Bat dau lang nghe goi UDP broadcast tren cong discovery.
    /// Moi goi hop le se duoc parse va phat su kien HostDiscovered.
    /// </summary>
    public async Task StartListeningAsync(int discoveryPort, CancellationToken cancellationToken = default)
    {
        if (discoveryPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(discoveryPort));
        }

        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureCts(cancellationToken);

            if (_listenClient is null)
            {
                _listenClient = new UdpClient(new IPEndPoint(IPAddress.Any, discoveryPort));
                _listenClient.EnableBroadcast = true;
            }

            var linkedToken = _cts!.Token;
            _listenTask ??= Task.Run(() => ListeningLoopAsync(linkedToken), CancellationToken.None);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Dung toan bo task discovery va giai phong UdpClient.
    /// </summary>
    public Task StopAsync()
    {
        return StopInternalAsync(waitForLoops: true);
    }

    /// <summary>
    /// Vong lap phat ban tin host announcement theo interval.
    /// Delay bat dong bo de khong ton CPU va de de dieu chinh nhiet do broadcast.
    /// </summary>
    private async Task AnnouncementLoopAsync(string hostName, int hostTcpPort, int discoveryPort, TimeSpan interval, CancellationToken token)
    {
        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

        try
        {
            while (!token.IsCancellationRequested)
            {
                var payload = $"HOST|{hostName}|{hostTcpPort}|{DateTimeOffset.UtcNow.UtcTicks}";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await _announceClient!.SendAsync(bytes, bytes.Length, broadcastEndpoint).WaitAsync(token).ConfigureAwait(false);
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Dung binh thuong khi StopAsync duoc goi.
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            RaiseDiscoveryError(ex, "AnnouncementLoopAsync");
        }
    }

    /// <summary>
    /// Vong lap nhan ban tin discovery.
    /// Co bo loc duplicate ngan de UI khong bi spam khi host broadcast lien tuc.
    /// </summary>
    private async Task ListeningLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var result = await _listenClient!.ReceiveAsync().WaitAsync(token).ConfigureAwait(false);
                if (!TryParseAnnouncement(result.Buffer, out var hostName, out var hostPort))
                {
                    continue;
                }

                var hostIp = result.RemoteEndPoint.Address.ToString();
                var key = $"{hostIp}:{hostPort}";
                var now = DateTimeOffset.UtcNow;

                if (_lastSeen.TryGetValue(key, out var seenAt) && (now - seenAt) < TimeSpan.FromSeconds(1))
                {
                    continue;
                }

                _lastSeen[key] = now;
                RaiseHostDiscovered(hostName, hostIp, hostPort, now);
            }
        }
        catch (OperationCanceledException)
        {
            // Dung binh thuong khi StopAsync duoc goi.
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
        {
            RaiseDiscoveryError(ex, "ListeningLoopAsync");
        }
    }

    /// <summary>
    /// Parse payload discovery ve hostName va tcpPort.
    /// Ham tra false thay vi nem loi de listener loop tiep tuc an toan.
    /// </summary>
    private static bool TryParseAnnouncement(byte[] buffer, out string hostName, out int hostPort)
    {
        hostName = string.Empty;
        hostPort = -1;

        if (buffer is null || buffer.Length == 0)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(buffer).Trim();
        var parts = text.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
        {
            return false;
        }

        if (!parts[0].Equals("HOST", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[2], out hostPort) || hostPort is < 1 or > 65535)
        {
            return false;
        }

        hostName = parts[1];
        return !string.IsNullOrWhiteSpace(hostName);
    }

    /// <summary>
    /// Tao cts dung chung cho cac loop discovery.
    /// </summary>
    private void EnsureCts(CancellationToken externalToken)
    {
        if (_cts is null)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        }
    }

    /// <summary>
    /// Dung noi bo cho discovery, cho phep lua chon co doi task loop ket thuc hay khong.
    /// </summary>
    private async Task StopInternalAsync(bool waitForLoops)
    {
        Task? announceTask;
        Task? listenTask;

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cts?.Cancel();
            announceTask = _announceTask;
            listenTask = _listenTask;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (waitForLoops)
        {
            try
            {
                if (announceTask is not null)
                {
                    await announceTask.ConfigureAwait(false);
                }

                if (listenTask is not null)
                {
                    await listenTask.ConfigureAwait(false);
                }
            }
            catch
            {
                // Loi da duoc dua qua DiscoveryError.
            }
        }

        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _announceClient?.Dispose();
            _listenClient?.Dispose();
            _announceClient = null;
            _listenClient = null;
            _announceTask = null;
            _listenTask = null;
            _cts?.Dispose();
            _cts = null;
            _lastSeen.Clear();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <summary>
    /// Phat su kien host discovered tren UI thread.
    /// </summary>
    private void RaiseHostDiscovered(string hostName, string hostIp, int hostPort, DateTimeOffset discoveredAtUtc)
    {
        var args = new HostDiscoveredEventArgs(hostName, hostIp, hostPort, discoveredAtUtc);
        PostToUiThread(() => HostDiscovered?.Invoke(this, args));
    }

    /// <summary>
    /// Phat su kien loi discovery tren UI thread.
    /// </summary>
    private void RaiseDiscoveryError(Exception ex, string operation)
    {
        var args = new NetworkErrorEventArgs(ex, operation);
        PostToUiThread(() => DiscoveryError?.Invoke(this, args));
    }

    /// <summary>
    /// Marshal callback ve luong UI de tranh loi truy cap control sai thread.
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
    /// Dispose service theo thu tu an toan: stop truoc, roi moi dispose lock.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }
}
