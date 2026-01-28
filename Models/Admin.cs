namespace CS2Admin.Models;

public class Admin
{
    public int Id { get; set; }
    public ulong SteamId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong CreatedBySteamId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public string[] GetFlags() => Flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
