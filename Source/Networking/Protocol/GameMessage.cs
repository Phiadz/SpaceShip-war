using System.Diagnostics.CodeAnalysis;

namespace Battleship2D.Networking.Protocol;

/// <summary>
/// Dai dien mot message da parse tu chuoi TCP.
/// Cac truong khong lien quan voi tung loai message co the null.
/// </summary>
[SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Protocol DTO needs multiple optional parsed fields.")]
public sealed class GameMessage(
    GameMessageType type,
    int? x = null,
    int? y = null,
    ShotOutcome? outcome = null,
    bool? ready = null,
    bool? winner = null,
    string? raw = null,
    int? creditsAmount = null,
    string? loadoutData = null,
    string? economyData = null)
{
    public GameMessageType Type { get; } = type;
    public int? X { get; } = x;
    public int? Y { get; } = y;
    public ShotOutcome? Outcome { get; } = outcome;
    public bool? Ready { get; } = ready;
    public bool? Winner { get; } = winner;
    public string? Raw { get; } = raw;
    public int? CreditsAmount { get; } = creditsAmount;
    public string? LoadoutData { get; } = loadoutData;
    public string? EconomyData { get; } = economyData;
}
