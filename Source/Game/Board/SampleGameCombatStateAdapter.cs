using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Battleship2D.Game.Session;
using Battleship2D.Networking.Protocol;

namespace Battleship2D.Game.Board;

/// <summary>
/// Adapter mau de noi session networking voi board 10x10.
/// Lop nay cung cap mot fleet mac dinh de ban co the test end-to-end ngay.
/// </summary>
public sealed class SampleGameCombatStateAdapter : IGameCombatStateAdapter
{
    private readonly List<HashSet<(int X, int Y)>> _localShips = new();
    private readonly HashSet<(int X, int Y)> _incomingShots = new();
    private readonly Dictionary<(int X, int Y), ShotOutcome> _enemyBoardResults = new();

    /// <summary>
    /// Khoi tao board va dat fleet mac dinh cho test nhanh.
    /// </summary>
    public SampleGameCombatStateAdapter()
    {
        BuildDefaultFleet();
    }

    /// <summary>
    /// Xu ly vien dan doi thu ban vao board local.
    /// Tra ve Miss/Hit/Sunk de coordinator gui RESULT nguoc lai.
    /// </summary>
    public Task<ShotOutcome> ResolveIncomingShotAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCoordinate(x, y);

        var target = (x, y);
        if (!_incomingShots.Add(target))
        {
            // Da bi ban toa do nay roi, tra ve Miss de protocol don gian va idempotent.
            return Task.FromResult(ShotOutcome.Miss);
        }

        var ship = _localShips.FirstOrDefault(s => s.Contains(target));
        if (ship is null)
        {
            return Task.FromResult(ShotOutcome.Miss);
        }

        var isSunk = ship.All(c => _incomingShots.Contains(c));
        return Task.FromResult(isSunk ? ShotOutcome.Sunk : ShotOutcome.Hit);
    }

    /// <summary>
    /// Luu ket qua phat ban cua local player len board doi thu ban sao local.
    /// ViewModel co the doc dictionary nay de to mau luoi enemy.
    /// </summary>
    public Task ApplyOutgoingShotResultAsync(int x, int y, ShotOutcome outcome, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateCoordinate(x, y);
        _enemyBoardResults[(x, y)] = outcome;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tra ket qua da biet cua mot o tren board doi thu (neu da ban).
    /// </summary>
    public ShotOutcome? GetKnownEnemyCellResult(int x, int y)
    {
        ValidateCoordinate(x, y);
        return _enemyBoardResults.TryGetValue((x, y), out var outcome) ? outcome : null;
    }

    /// <summary>
    /// Kiem tra mot o co phai vi tri tau local khong.
    /// UI dung ham nay de ve doi hinh khoi tao tren local board.
    /// </summary>
    public bool IsLocalShipCell(int x, int y)
    {
        ValidateCoordinate(x, y);
        var cell = (x, y);
        return _localShips.Any(ship => ship.Contains(cell));
    }

    /// <summary>
    /// Ap dung doi hinh dat tau tu placement phase thay cho layout mac dinh.
    /// </summary>
    public void ApplyCustomFleet(IEnumerable<IReadOnlyCollection<(int X, int Y)>> ships)
    {
        if (ships is null)
        {
            throw new ArgumentNullException(nameof(ships));
        }

        var normalizedShips = ships
            .Select(s => new HashSet<(int X, int Y)>(s))
            .Where(s => s.Count > 0)
            .ToList();

        if (normalizedShips.Count == 0)
        {
            throw new InvalidOperationException("Custom fleet cannot be empty.");
        }

        var occupied = new HashSet<(int X, int Y)>();
        foreach (var ship in normalizedShips)
        {
            foreach (var cell in ship)
            {
                ValidateCoordinate(cell.X, cell.Y);
                if (!occupied.Add(cell))
                {
                    throw new InvalidOperationException("Custom fleet has overlapping cells.");
                }
            }
        }

        _localShips.Clear();
        _localShips.AddRange(normalizedShips);
        _incomingShots.Clear();
        _enemyBoardResults.Clear();
    }

    /// <summary>
    /// Tra ve true neu moi o cua tat ca tau local deu da trung dan.
    /// </summary>
    public bool AreAllLocalShipsDestroyed()
    {
        if (_localShips.Count == 0)
        {
            return false;
        }

        return _localShips.All(ship => ship.All(cell => _incomingShots.Contains(cell)));
    }

    /// <summary>
    /// Dat fleet mac dinh theo bo 5-4-3-3-2 de de test nhanh.
    /// Khong random de hai may test ra ket qua giong nhau, de debug.
    /// </summary>
    private void BuildDefaultFleet()
    {
        _localShips.Clear();

        _localShips.Add(BuildHorizontalShip(0, 0, 5));
        _localShips.Add(BuildHorizontalShip(2, 1, 4));
        _localShips.Add(BuildHorizontalShip(4, 2, 3));
        _localShips.Add(BuildHorizontalShip(6, 3, 3));
        _localShips.Add(BuildHorizontalShip(8, 4, 2));
    }

    /// <summary>
    /// Tao ship nam ngang tu cot bat dau voi do dai cho truoc.
    /// </summary>
    private static HashSet<(int X, int Y)> BuildHorizontalShip(int xStart, int y, int length)
    {
        var ship = new HashSet<(int X, int Y)>();
        for (var i = 0; i < length; i++)
        {
            var x = xStart + i;
            ValidateCoordinate(x, y);
            ship.Add((x, y));
        }

        return ship;
    }

    /// <summary>
    /// Dam bao toa do trong mien 10x10.
    /// </summary>
    private static void ValidateCoordinate(int x, int y)
    {
        if (x < 0 || x > 9 || y < 0 || y > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinate must be in range 0..9.");
        }
    }
}
