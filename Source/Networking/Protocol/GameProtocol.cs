using System;
using Battleship2D.Networking.Abstractions;

namespace Battleship2D.Networking.Protocol;

/// <summary>
/// Protocol text don gian theo format: COMMAND|arg1|arg2...
/// Chon format nay vi de debug bang log, Wireshark va de doc trong qua trinh hoc.
/// </summary>
public sealed class GameProtocol : IGameProtocol
{
    /// <summary>
    /// Tao message READY de thong bao da san sang vao tran.
    /// </summary>
    public string BuildReady(bool isReady = true) => $"READY|{(isReady ? 1 : 0)}";

    /// <summary>
    /// Tao message FIRE de ban vao toa do x,y tren luoi 10x10.
    /// </summary>
    public string BuildFire(int x, int y)
    {
        ValidateCoordinate(x, y);
        return $"FIRE|{x}|{y}";
    }

    /// <summary>
    /// Tao message RESULT de tra ket qua sau khi bi ban.
    /// </summary>
    public string BuildResult(int x, int y, ShotOutcome outcome)
    {
        ValidateCoordinate(x, y);
        return $"RESULT|{x}|{y}|{outcome.ToString().ToUpperInvariant()}";
    }

    /// <summary>
    /// Tao message END khi tran ket thuc.
    /// </summary>
    public string BuildEnd(bool isWinner) => $"END|{(isWinner ? "WIN" : "LOSE")}";

    /// <summary>
    /// Parse message tu dang text sang object co kieu.
    /// Ham khong nem exception de receive loop an toan; chi tra ve false neu message sai.
    /// </summary>
    public bool TryParse(string rawMessage, out GameMessage? parsedMessage)
    {
        parsedMessage = null;

        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return false;
        }

        var parts = rawMessage.Trim().Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var command = parts[0].ToUpperInvariant();

        switch (command)
        {
            case "READY":
                if (parts.Length != 2 || !TryParseBool01(parts[1], out var ready))
                {
                    return false;
                }

                parsedMessage = new GameMessage(GameMessageType.Ready, ready: ready, raw: rawMessage);
                return true;

            case "FIRE":
                if (parts.Length != 3 || !TryParseCoordinate(parts[1], parts[2], out var fx, out var fy))
                {
                    return false;
                }

                parsedMessage = new GameMessage(GameMessageType.Fire, x: fx, y: fy, raw: rawMessage);
                return true;

            case "RESULT":
                if (parts.Length != 4 || !TryParseCoordinate(parts[1], parts[2], out var rx, out var ry))
                {
                    return false;
                }

                if (!Enum.TryParse<ShotOutcome>(parts[3], true, out var outcome))
                {
                    return false;
                }

                parsedMessage = new GameMessage(GameMessageType.Result, x: rx, y: ry, outcome: outcome, raw: rawMessage);
                return true;

            case "END":
                if (parts.Length != 2)
                {
                    return false;
                }

                var value = parts[1].ToUpperInvariant();
                var isWinner = value switch
                {
                    "WIN" => true,
                    "LOSE" => false,
                    _ => (bool?)null
                };

                if (!isWinner.HasValue)
                {
                    return false;
                }

                parsedMessage = new GameMessage(GameMessageType.End, winner: isWinner.Value, raw: rawMessage);
                return true;

            default:
                parsedMessage = new GameMessage(GameMessageType.Unknown, raw: rawMessage);
                return true;
        }
    }

    /// <summary>
    /// Kiem tra toa do nam trong luoi 10x10 (0..9).
    /// </summary>
    private static void ValidateCoordinate(int x, int y)
    {
        if (x < 0 || x > 9 || y < 0 || y > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinate must be in range 0..9.");
        }
    }

    /// <summary>
    /// Parse 2 thanh phan toa do va dam bao dung mien 0..9.
    /// </summary>
    private static bool TryParseCoordinate(string xText, string yText, out int x, out int y)
    {
        x = -1;
        y = -1;

        if (!int.TryParse(xText, out x) || !int.TryParse(yText, out y))
        {
            return false;
        }

        return x is >= 0 and <= 9 && y is >= 0 and <= 9;
    }

    /// <summary>
    /// Parse bool theo quy uoc protocol 1=true, 0=false.
    /// </summary>
    private static bool TryParseBool01(string text, out bool value)
    {
        value = false;
        if (text == "1")
        {
            value = true;
            return true;
        }

        if (text == "0")
        {
            value = false;
            return true;
        }

        return false;
    }
}
