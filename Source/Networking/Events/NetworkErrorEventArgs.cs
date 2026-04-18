using System;

namespace Battleship2D.Networking.Events;

public sealed class NetworkErrorEventArgs : EventArgs
{
    public NetworkErrorEventArgs(Exception exception, string operation)
    {
        Exception = exception;
        Operation = operation;
    }

    public Exception Exception { get; }
    public string Operation { get; }
}
