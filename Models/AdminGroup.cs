namespace CS2Admin.Models;

public class AdminGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Flags { get; set; } = string.Empty;
    public int Immunity { get; set; }
    public DateTime CreatedAt { get; set; }
    public ulong CreatedBySteamId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public string[] GetFlags() => Flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
