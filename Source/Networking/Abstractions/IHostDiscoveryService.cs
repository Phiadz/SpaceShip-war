using System;
using System.Threading;
using System.Threading.Tasks;
using Battleship2D.Networking.Events;

namespace Battleship2D.Networking.Abstractions;

/// <summary>
/// Dinh nghia dich vu tim host bang UDP broadcast trong LAN.
/// </summary>
public interface IHostDiscoveryService : IAsyncDisposable
{
    event EventHandler<HostDiscoveredEventArgs>? HostDiscovered;
    event EventHandler<NetworkErrorEventArgs>? DiscoveryError;

    /// <summary>
    /// Bat dau phat goi broadcast de thong bao su ton tai cua Host.
    /// </summary>
    Task StartHostAnnouncementAsync(string hostName, int hostTcpPort, int discoveryPort, TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bat dau lang nghe goi broadcast de tim Host trong LAN.
    /// </summary>
    Task StartListeningAsync(int discoveryPort, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dung toan bo chuc nang discovery va giai phong tai nguyen UDP.
    /// </summary>
    Task StopAsync();
}
