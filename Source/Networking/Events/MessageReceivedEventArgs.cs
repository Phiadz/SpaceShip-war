using System;

namespace Battleship2D.Networking.Events;

public sealed class MessageReceivedEventArgs : EventArgs
{
    public MessageReceivedEventArgs(string message, DateTimeOffset receivedAtUtc)
    {
        Message = message;
        ReceivedAtUtc = receivedAtUtc;
    }

    public string Message { get; }
    public DateTimeOffset ReceivedAtUtc { get; }
}
