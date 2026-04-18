namespace Battleship2D.Networking.Protocol;

/// <summary>
/// Dai dien mot message da parse tu chuoi TCP.
/// Cac truong khong lien quan voi tung loai message co the null.
/// </summary>
public sealed class GameMessage
{
    public GameMessage(
        GameMessageType type,
        int? x = null,
        int? y = null,
        ShotOutcome? outcome = null,
        bool? ready = null,
        bool? winner = null,
        string? raw = null)
    {
        Type = type;
        X = x;
        Y = y;
        Outcome = outcome;
        Ready = ready;
        Winner = winner;
        Raw = raw;
    }

    public GameMessageType Type { get; }
    public int? X { get; }
    public int? Y { get; }
    public ShotOutcome? Outcome { get; }
    public bool? Ready { get; }
    public bool? Winner { get; }
    public string? Raw { get; }
}
