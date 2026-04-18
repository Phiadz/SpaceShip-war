namespace Battleship2D.Presentation.ViewModels;

/// <summary>
/// Model hien thi host tim duoc tren UI.
/// </summary>
public sealed class DiscoveredHostViewModel
{
    /// <summary>
    /// Khoi tao host item.
    /// </summary>
    public DiscoveredHostViewModel(string hostName, string hostIp, int hostPort, string lastSeen)
    {
        HostName = hostName;
        HostIp = hostIp;
        HostPort = hostPort;
        LastSeen = lastSeen;
    }

    public string HostName { get; }
    public string HostIp { get; }
    public int HostPort { get; }
    public string LastSeen { get; }

    public string Display => $"{HostName} - {HostIp}:{HostPort} ({LastSeen})";
}
