using System;

namespace Battleship2D.Networking.Events;

public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionStateChangedEventArgs(ConnectionState state, NetworkRole role, string? peerEndPoint)
    {
        State = state;
        Role = role;
        PeerEndPoint = peerEndPoint;
    }

    public ConnectionState State { get; }
    public NetworkRole Role { get; }
    public string? PeerEndPoint { get; }
}
