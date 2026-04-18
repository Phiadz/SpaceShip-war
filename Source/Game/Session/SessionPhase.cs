namespace Battleship2D.Game.Session;

/// <summary>
/// Cac pha cua tran dau.
/// </summary>
public enum SessionPhase
{
    Placement = 0,
    AwaitingReady = 1,
    Combat = 2,
    Finished = 3
}
