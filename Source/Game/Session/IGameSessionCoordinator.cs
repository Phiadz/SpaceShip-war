using System;
using System.Threading;
using System.Threading.Tasks;
using Battleship2D.Networking.Protocol;

namespace Battleship2D.Game.Session;

/// <summary>
/// Dieu phoi logic READY/FIRE/RESULT/END o muc session.
/// Layer nay khong phu thuoc truc tiep vao UI controls.
/// </summary>
public interface IGameSessionCoordinator : IAsyncDisposable
{
    event EventHandler<SessionPhaseChangedEventArgs>? PhaseChanged;
    event EventHandler<TurnChangedEventArgs>? TurnChanged;
    event EventHandler<ShotResolvedEventArgs>? IncomingShotResolved;
    event EventHandler<ShotResolvedEventArgs>? OutgoingShotResolved;
    event EventHandler<GameEndedEventArgs>? GameEnded;

    SessionPhase Phase { get; }
    bool IsLocalReady { get; }
    bool IsRemoteReady { get; }
    bool IsLocalTurn { get; }

    Task ResetSessionAsync(CancellationToken cancellationToken = default);
    Task ConfirmReadyAsync(CancellationToken cancellationToken = default);
    Task FireAsync(int x, int y, CancellationToken cancellationToken = default);
    Task EndGameAsync(bool isWinner, CancellationToken cancellationToken = default);
}
