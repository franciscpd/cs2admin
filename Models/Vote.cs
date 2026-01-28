using CounterStrikeSharp.API.Core;

namespace CS2Admin.Models;

public class Vote
{
    public VoteType Type { get; set; }
    public CCSPlayerController Initiator { get; set; } = null!;
    public CCSPlayerController? TargetPlayer { get; set; }
    public string? TargetMap { get; set; }
    public HashSet<ulong> YesVotes { get; set; } = new();
    public HashSet<ulong> NoVotes { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }

    public int TotalVotes => YesVotes.Count + NoVotes.Count;
    public double YesPercentage => TotalVotes > 0 ? (double)YesVotes.Count / TotalVotes * 100 : 0;
}
