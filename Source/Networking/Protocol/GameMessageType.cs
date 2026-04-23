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
    End = 4,
    Credits = 5,     // Dùng để báo cộng tiền khi bắn trúng
    Loadout = 6,     // Dùng để chốt danh sách vũ khí trước khi đánh
    Economy = 7      // (Dự phòng) Đồng bộ toàn bộ ví tiền
}
