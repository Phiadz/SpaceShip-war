using System;
using System.Threading;
using System.Threading.Tasks;
using Battleship2D.Networking.Events;

namespace Battleship2D.Networking.Abstractions;

/// <summary>
/// Hop dong networking TCP peer-to-peer cho game.
/// Interface nay giup tach implementation khoi tang Application/ViewModel,
/// de test mock va thay the implementation de dang hon.
/// </summary>
public interface INetworkManager : IAsyncDisposable
{
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<NetworkErrorEventArgs>? NetworkError;
    event EventHandler<ReconnectAttemptedEventArgs>? ReconnectAttempted;

    ConnectionState State { get; }
    NetworkRole Role { get; }
    bool IsConnected { get; }

    /// <summary>
    /// Khoi tao va cho ket noi den o vai tro Host.
    /// </summary>
    Task StartHostAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ket noi den Host o vai tro Client.
    /// </summary>
    Task ConnectToHostAsync(string hostIp, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gui mot message protocol den peer qua TCP stream.
    /// </summary>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dong session ket noi va giai phong tai nguyen lien quan.
    /// </summary>
    Task StopAsync();
}
