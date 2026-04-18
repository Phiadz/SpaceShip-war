using System;

namespace Battleship2D.Networking.Events;

/// <summary>
/// Su kien thong bao tien trinh auto reconnect.
/// </summary>
public sealed class ReconnectAttemptedEventArgs : EventArgs
{
    /// <summary>
    /// Tao doi tuong su kien reconnect.
    /// </summary>
    public ReconnectAttemptedEventArgs(int attemptNumber, int maxAttempts, bool success)
    {
        AttemptNumber = attemptNumber;
        MaxAttempts = maxAttempts;
        Success = success;
    }

    public int AttemptNumber { get; }
    public int MaxAttempts { get; }
    public bool Success { get; }
}
