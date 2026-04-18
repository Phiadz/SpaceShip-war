namespace Battleship2D.Networking.Protocol;

/// <summary>
/// Tap loai message trong protocol cho tran dau turn-based.
/// </summary>
public enum GameMessageType
{
    Unknown = 0,
    Ready = 1,
    Fire = 2,
    Result = 3,
    End = 4
}
