using System.Threading;
using System.Threading.Tasks;
using Battleship2D.Networking.Protocol;

namespace Battleship2D.Game.Session;

/// <summary>
/// Adapter noi Session Coordinator voi game-board/domain logic.
/// Tach interface nay giup coordinator chi lo networking + turn state.
/// </summary>
public interface IGameCombatStateAdapter
{
    /// <summary>
    /// Xu ly phat ban tu doi thu vao board local va tra ket qua.
    /// </summary>
    Task<ShotOutcome> ResolveIncomingShotAsync(int x, int y, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ap ket qua phat ban cua local player len board doi thu (ban sao local).
    /// </summary>
    Task ApplyOutgoingShotResultAsync(int x, int y, ShotOutcome outcome, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiem tra tat ca tau local da bi pha huy het chua.
    /// </summary>
    bool AreAllLocalShipsDestroyed();
}
