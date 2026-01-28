using CounterStrikeSharp.API.Core;
using CS2Admin.Database;
using CS2Admin.Models;
using CS2Admin.Utils;
using Microsoft.Data.Sqlite;

namespace CS2Admin.Services;

public class MuteService
{
    private readonly DatabaseService _database;
    private readonly bool _enableLogging;
    private readonly HashSet<ulong> _sessionMutes = new();

    public MuteService(DatabaseService database, bool enableLogging)
    {
        _database = database;
        _enableLogging = enableLogging;
    }

    public Mute? GetActiveMute(ulong steamId)
    {
        var connection = _database.GetConnection();
        const string sql = """
            SELECT id, steam_id, player_name, reason, admin_steam_id, admin_name, created_at, expires_at
            FROM mutes
            WHERE steam_id = @steamId
              AND unmuted_at IS NULL
              AND (expires_at IS NULL OR expires_at > @now)
            ORDER BY created_at DESC
            LIMIT 1
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new Mute
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

    public bool IsMuted(ulong steamId)
    {
        return _sessionMutes.Contains(steamId) || GetActiveMute(steamId) != null;
    }

    public void AddSessionMute(ulong steamId)
    {
        _sessionMutes.Add(steamId);
    }

    public void RemoveSessionMute(ulong steamId)
    {
        _sessionMutes.Remove(steamId);
    }

    public void MutePlayer(CCSPlayerController target, CCSPlayerController admin, TimeSpan? duration, string reason)
    {
        var steamId = target.SteamID;
        var playerName = target.PlayerName;
        var adminSteamId = admin.SteamID;
        var adminName = admin.PlayerName;

        MutePlayer(steamId, playerName, adminSteamId, adminName, duration, reason);
    }

    public void MutePlayer(ulong steamId, string playerName, ulong adminSteamId, string adminName, TimeSpan? duration, string reason)
    {
        _sessionMutes.Add(steamId);

        var connection = _database.GetConnection();
        const string sql = """
            INSERT INTO mutes (steam_id, player_name, reason, admin_steam_id, admin_name, created_at, expires_at)
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
            _database.LogAction("MUTE", adminSteamId, adminName, steamId, playerName, $"Duration: {durationText}, Reason: {reason}");
        }
    }

    public bool UnmutePlayer(ulong steamId, ulong adminSteamId, string adminName)
    {
        _sessionMutes.Remove(steamId);

        var mute = GetActiveMute(steamId);
        if (mute == null) return false;

        var connection = _database.GetConnection();
        const string sql = """
            UPDATE mutes
            SET unmuted_at = @unmutedAt, unmuted_by_steam_id = @adminSteamId, unmuted_by_name = @adminName
            WHERE id = @id
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@unmutedAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@adminSteamId", (long)adminSteamId);
        command.Parameters.AddWithValue("@adminName", adminName);
        command.Parameters.AddWithValue("@id", mute.Id);
        command.ExecuteNonQuery();

        if (_enableLogging)
        {
            _database.LogAction("UNMUTE", adminSteamId, adminName, steamId, mute.PlayerName, null);
        }

        return true;
    }

    public bool UnmutePlayer(CCSPlayerController target, CCSPlayerController admin)
    {
        return UnmutePlayer(target.SteamID, admin.SteamID, admin.PlayerName);
    }

    public void LoadMutesForPlayer(ulong steamId)
    {
        if (GetActiveMute(steamId) != null)
        {
            _sessionMutes.Add(steamId);
        }
    }
}
