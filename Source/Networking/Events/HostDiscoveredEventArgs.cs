using System;

namespace Battleship2D.Networking.Events;

/// <summary>
/// Chua thong tin Host duoc tim thay qua UDP broadcast.
/// </summary>
public sealed class HostDiscoveredEventArgs : EventArgs
{
    /// <summary>
    /// Tao doi tuong su kien host discovery.
    /// </summary>
    public HostDiscoveredEventArgs(string hostName, string hostIp, int hostPort, DateTimeOffset discoveredAtUtc)
    {
        HostName = hostName;
        HostIp = hostIp;
        HostPort = hostPort;
        DiscoveredAtUtc = discoveredAtUtc;
    }

    public string HostName { get; }
    public string HostIp { get; }
    public int HostPort { get; }
    public DateTimeOffset DiscoveredAtUtc { get; }
}
