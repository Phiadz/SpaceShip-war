using System;

namespace Battleship2D.Networking;

/// <summary>
/// Cac thong so resilience cho NetworkManager.
/// Tach thanh object rieng de de tinh chinh theo moi moi truong mang.
/// </summary>
public sealed class NetworkManagerOptions
{
    /// <summary>
    /// Thoi gian toi da cho moi lan ket noi TCP.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// So lan thu lai ket noi (khong tinh lan dau tien).
    /// Vi du = 2 thi tong cong co 3 lan thu ket noi.
    /// </summary>
    public int ConnectRetryCount { get; set; } = 2;

    /// <summary>
    /// Khoang tre co so giua cac lan retry.
    /// Delay thuc te se tang dan theo so lan thu.
    /// </summary>
    public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// Timeout cho tung lan gui message.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Bat/tat auto reconnect khi client bi mat ket noi ngoai y muon.
    /// </summary>
    public bool AutoReconnectEnabled { get; set; } = true;

    /// <summary>
    /// So lan auto reconnect toi da sau khi dang ket noi ma bi dut.
    /// </summary>
    public int AutoReconnectMaxAttempts { get; set; } = 5;

    /// <summary>
    /// Khoang cho truoc khi bat dau lan auto reconnect dau tien.
    /// </summary>
    public TimeSpan AutoReconnectInitialDelay { get; set; } = TimeSpan.FromSeconds(1);
}
