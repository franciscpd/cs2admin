using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace CS2Admin.Utils;

public static class PlayerFinder
{
    /// <summary>
    /// Finds a player by partial name, exact name, #userid, or SteamID64
    /// </summary>
    public static CCSPlayerController? Find(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        target = target.Trim();

        // Try to find by #userid
        if (target.StartsWith('#') && int.TryParse(target[1..], out var userId))
        {
            return Utilities.GetPlayerFromUserid(userId);
        }

        // Try to find by SteamID64
        if (ulong.TryParse(target, out var steamId) && steamId > 76561197960265728)
        {
            return Utilities.GetPlayerFromSteamId(steamId);
        }

        // Find by name (case-insensitive, partial match)
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToList();

        // Exact match first
        var exactMatch = players.FirstOrDefault(p =>
            p.PlayerName.Equals(target, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
            return exactMatch;

        // Partial match
        var partialMatches = players
            .Where(p => p.PlayerName.Contains(target, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Only return if there's exactly one match to avoid ambiguity
        return partialMatches.Count == 1 ? partialMatches[0] : null;
    }

    /// <summary>
    /// Gets all valid human players on the server
    /// </summary>
    public static List<CCSPlayerController> GetAllPlayers()
    {
        return Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV)
            .ToList();
    }

    /// <summary>
    /// Gets the count of valid human players on the server
    /// </summary>
    public static int GetPlayerCount()
    {
        return GetAllPlayers().Count;
    }
}
