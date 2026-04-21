using System;
using Battleship2D.Networking.Protocol;

namespace Battleship2D.Game.Session;

/// <summary>
/// Su kien thay doi phase cua tran dau.
/// </summary>
public sealed class SessionPhaseChangedEventArgs : EventArgs
{
    public SessionPhaseChangedEventArgs(SessionPhase oldPhase, SessionPhase newPhase)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }

    public SessionPhase OldPhase { get; }
    public SessionPhase NewPhase { get; }
}

/// <summary>
/// Su kien thay doi luot choi.
/// </summary>
public sealed class TurnChangedEventArgs : EventArgs
{
    public TurnChangedEventArgs(bool isLocalTurn)
    {
        IsLocalTurn = isLocalTurn;
    }

    public bool IsLocalTurn { get; }
}

/// <summary>
/// Su kien bao ket qua phat ban de UI cap nhat luoi hien thi.
/// </summary>
public sealed class ShotResolvedEventArgs : EventArgs
{
    public ShotResolvedEventArgs(int x, int y, ShotOutcome outcome)
    {
        X = x;
        Y = y;
        Outcome = outcome;
    }

    public int X { get; }
    public int Y { get; }
    public ShotOutcome Outcome { get; }
}

/// <summary>
/// Su kien ket thuc tran dau de UI biet ben nao thang.
/// </summary>
public sealed class GameEndedEventArgs : EventArgs
{
    public GameEndedEventArgs(bool isLocalWinner, string reason)
    {
        IsLocalWinner = isLocalWinner;
        Reason = reason;
    }

    public bool IsLocalWinner { get; }
    public string Reason { get; }
}
