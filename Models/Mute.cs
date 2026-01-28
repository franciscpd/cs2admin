namespace CS2Admin.Models;

public class Mute
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public ulong AdminSteamId { get; set; }
    public string AdminName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsPermanent => ExpiresAt == null;
    public bool IsExpired => ExpiresAt != null && DateTime.UtcNow > ExpiresAt;
    public bool IsActive => !IsExpired;
}
