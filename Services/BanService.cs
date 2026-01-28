using CounterStrikeSharp.API.Core;
using CS2Admin.Database;
using CS2Admin.Models;
using CS2Admin.Utils;
using Microsoft.Data.Sqlite;

namespace CS2Admin.Services;

public class BanService
{
    private readonly DatabaseService _database;
    private readonly bool _enableLogging;

    public BanService(DatabaseService database, bool enableLogging)
    {
        _database = database;
        _enableLogging = enableLogging;
    }

    public Ban? GetActiveBan(ulong steamId)
    {
        var connection = _database.GetConnection();
        const string sql = """
            SELECT id, steam_id, player_name, reason, admin_steam_id, admin_name, created_at, expires_at
            FROM bans
            WHERE steam_id = @steamId
              AND unbanned_at IS NULL
              AND (expires_at IS NULL OR expires_at > @now)
            ORDER BY created_at DESC
            LIMIT 1
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new Ban
        {
            Id = reader.GetInt32(0),
            SteamId = (ulong)reader.GetInt64(1),
            PlayerName = reader.GetString(2),
            Reason = reader.GetString(3),
            AdminSteamId = (ulong)reader.GetInt64(4),
            AdminName = reader.GetString(5),
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            ExpiresAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7))
        };
    }

    public bool IsBanned(ulong steamId)
    {
        return GetActiveBan(steamId) != null;
    }

    public void BanPlayer(CCSPlayerController target, CCSPlayerController admin, TimeSpan? duration, string reason)
    {
        var steamId = target.SteamID;
        var playerName = target.PlayerName;
        var adminSteamId = admin.SteamID;
        var adminName = admin.PlayerName;

        BanPlayer(steamId, playerName, adminSteamId, adminName, duration, reason);
    }

    public void BanPlayer(ulong steamId, string playerName, ulong adminSteamId, string adminName, TimeSpan? duration, string reason)
    {
        var connection = _database.GetConnection();
        const string sql = """
            INSERT INTO bans (steam_id, player_name, reason, admin_steam_id, admin_name, created_at, expires_at)
            VALUES (@steamId, @playerName, @reason, @adminSteamId, @adminName, @createdAt, @expiresAt)
            """;

        var now = DateTime.UtcNow;
        DateTime? expiresAt = duration.HasValue ? now.Add(duration.Value) : null;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.Parameters.AddWithValue("@playerName", playerName);
        command.Parameters.AddWithValue("@reason", reason);
        command.Parameters.AddWithValue("@adminSteamId", (long)adminSteamId);
        command.Parameters.AddWithValue("@adminName", adminName);
        command.Parameters.AddWithValue("@createdAt", now.ToString("O"));
        command.Parameters.AddWithValue("@expiresAt", expiresAt?.ToString("O") ?? (object)DBNull.Value);
        command.ExecuteNonQuery();

        if (_enableLogging)
        {
            var durationText = TimeParser.Format(duration);
            _database.LogAction("BAN", adminSteamId, adminName, steamId, playerName, $"Duration: {durationText}, Reason: {reason}");
        }
    }

    public bool UnbanPlayer(ulong steamId, ulong adminSteamId, string adminName)
    {
        var ban = GetActiveBan(steamId);
        if (ban == null) return false;

        var connection = _database.GetConnection();
        const string sql = """
            UPDATE bans
            SET unbanned_at = @unbannedAt, unbanned_by_steam_id = @adminSteamId, unbanned_by_name = @adminName
            WHERE id = @id
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@unbannedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@adminSteamId", (long)adminSteamId);
        command.Parameters.AddWithValue("@adminName", adminName);
        command.Parameters.AddWithValue("@id", ban.Id);
        command.ExecuteNonQuery();

        if (_enableLogging)
        {
            _database.LogAction("UNBAN", adminSteamId, adminName, steamId, ban.PlayerName, null);
        }

        return true;
    }
}
