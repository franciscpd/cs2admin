using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CS2Admin.Database;

namespace CS2Admin.Services;

public class PlayerService
{
    private readonly DatabaseService? _database;
    private readonly bool _enableLogging;

    public PlayerService(DatabaseService? database = null, bool enableLogging = false)
    {
        _database = database;
        _enableLogging = enableLogging;
    }

    public void KickPlayer(CCSPlayerController target, CCSPlayerController? admin, string reason)
    {
        if (!target.IsValid) return;

        Server.ExecuteCommand($"kickid {target.UserId} \"{reason}\"");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("KICK", admin.SteamID, admin.PlayerName, target.SteamID, target.PlayerName, $"Reason: {reason}");
        }
    }

    public void SlayPlayer(CCSPlayerController target, CCSPlayerController? admin)
    {
        if (!target.IsValid) return;

        var pawn = target.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;

        pawn.CommitSuicide(false, true);

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("SLAY", admin.SteamID, admin.PlayerName, target.SteamID, target.PlayerName, null);
        }
    }

    public void SlapPlayer(CCSPlayerController target, CCSPlayerController? admin, int damage = 0)
    {
        if (!target.IsValid) return;

        var pawn = target.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;

        if (pawn.Health <= 0) return;

        // Apply damage
        if (damage > 0)
        {
            var newHealth = Math.Max(0, pawn.Health - damage);
            pawn.Health = newHealth;

            if (newHealth <= 0)
            {
                pawn.CommitSuicide(false, true);
            }
        }

        // Apply velocity push (slap effect)
        var random = new Random();
        var velocity = new Vector(
            random.Next(-300, 300),
            random.Next(-300, 300),
            random.Next(100, 300)
        );

        pawn.AbsVelocity.Add(velocity);

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("SLAP", admin.SteamID, admin.PlayerName, target.SteamID, target.PlayerName, $"Damage: {damage}");
        }
    }

    public void RespawnPlayer(CCSPlayerController target, CCSPlayerController? admin)
    {
        if (!target.IsValid) return;

        target.Respawn();

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("RESPAWN", admin.SteamID, admin.PlayerName, target.SteamID, target.PlayerName, null);
        }
    }
}
