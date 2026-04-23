using Battleship2D.Networking.Protocol;

namespace Battleship2D.Networking.Abstractions;

/// <summary>
/// Dinh nghia parser/serializer cho message game qua TCP.
/// </summary>
public interface IGameProtocol
{
    /// <summary>
    /// Dong goi message READY gui den peer.
    /// </summary>
    string BuildReady(bool isReady = true);

    /// <summary>
    /// Dong goi message FIRE tai toa do x,y.
    /// </summary>
    string BuildFire(int x, int y);

    /// <summary>
    /// Dong goi message RESULT tra ket qua phat ban.
    /// </summary>
    string BuildResult(int x, int y, ShotOutcome outcome);

    /// <summary>
    /// Dong goi message END thong bao trang thai thang/thua.
    /// </summary>
    string BuildEnd(bool isWinner);

    /// <summary>
    /// Dong goi message CREDITS de bao cong tien.
    /// </summary>
    string BuildCredits(int amount);

    /// <summary>
    /// Dong goi message LOADOUT de chot danh sach vu khi.
    /// </summary>
    string BuildLoadout(string loadoutDataPayload);

    /// <summary>
    /// Dong goi message ECONOMY de dong bo vi tien.
    /// </summary>
    string BuildEconomy(int credits, int spent);

    /// <summary>
    /// Thu parse chuoi thuan TCP thanh GameMessage co kieu du lieu ro rang.
    /// </summary>
    bool TryParse(string rawMessage, out GameMessage? parsedMessage);
}
