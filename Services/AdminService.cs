using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CS2Admin.Database;
using CS2Admin.Models;
using Microsoft.Data.Sqlite;

namespace CS2Admin.Services;

public class AdminService
{
    private readonly DatabaseService _database;
    private readonly bool _enableLogging;

    public AdminService(DatabaseService database, bool enableLogging)
    {
        _database = database;
        _enableLogging = enableLogging;
    }

    public void LoadAdminsToGame()
    {
        var admins = GetAllAdmins();
        var groups = GetAllGroups();

        foreach (var admin in admins)
        {
            var flags = new List<string>();

            // Get flags from group if assigned
            if (!string.IsNullOrEmpty(admin.GroupName))
            {
                var group = groups.FirstOrDefault(g => g.Name == admin.GroupName);
                if (group != null)
                {
                    flags.AddRange(group.GetFlags());
                }
            }

            // Add individual flags
            if (!string.IsNullOrEmpty(admin.Flags))
            {
                flags.AddRange(admin.GetFlags());
            }

            if (flags.Count > 0)
            {
                var distinctFlags = flags.Distinct().ToArray();
                AdminManager.AddPlayerPermissions(new SteamID(admin.SteamId), distinctFlags);
            }
        }
    }

    #region Admin Management

    public List<Admin> GetAllAdmins()
    {
        var connection = _database.GetConnection();
        const string sql = "SELECT id, steam_id, player_name, flags, group_name, created_at, created_by_steam_id, created_by_name FROM admins";

        using var command = new SqliteCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var admins = new List<Admin>();
        while (reader.Read())
        {
            admins.Add(new Admin
            {
                Id = reader.GetInt32(0),
                SteamId = (ulong)reader.GetInt64(1),
                PlayerName = reader.GetString(2),
                Flags = reader.IsDBNull(3) ? "" : reader.GetString(3),
                GroupName = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                CreatedBySteamId = (ulong)reader.GetInt64(6),
                CreatedByName = reader.GetString(7)
            });
        }

        return admins;
    }

    public Admin? GetAdmin(ulong steamId)
    {
        var connection = _database.GetConnection();
        const string sql = "SELECT id, steam_id, player_name, flags, group_name, created_at, created_by_steam_id, created_by_name FROM admins WHERE steam_id = @steamId";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new Admin
        {
            Id = reader.GetInt32(0),
            SteamId = (ulong)reader.GetInt64(1),
            PlayerName = reader.GetString(2),
            Flags = reader.IsDBNull(3) ? "" : reader.GetString(3),
            GroupName = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            CreatedBySteamId = (ulong)reader.GetInt64(6),
            CreatedByName = reader.GetString(7)
        };
    }

    public bool AddAdmin(ulong steamId, string playerName, string flags, ulong createdBySteamId, string createdByName)
    {
        if (GetAdmin(steamId) != null) return false;

        var connection = _database.GetConnection();
        const string sql = """
            INSERT INTO admins (steam_id, player_name, flags, created_at, created_by_steam_id, created_by_name)
            VALUES (@steamId, @playerName, @flags, @createdAt, @createdBySteamId, @createdByName)
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.Parameters.AddWithValue("@playerName", playerName);
        command.Parameters.AddWithValue("@flags", flags);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@createdBySteamId", (long)createdBySteamId);
        command.Parameters.AddWithValue("@createdByName", createdByName);
        command.ExecuteNonQuery();

        // Add to runtime
        var flagArray = flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        AdminManager.AddPlayerPermissions(new SteamID(steamId), flagArray);

        if (_enableLogging)
        {
            _database.LogAction("ADD_ADMIN", createdBySteamId, createdByName, steamId, playerName, $"Flags: {flags}");
        }

        return true;
    }

    public bool RemoveAdmin(ulong steamId, ulong removedBySteamId, string removedByName)
    {
        var admin = GetAdmin(steamId);
        if (admin == null) return false;

        var connection = _database.GetConnection();
        const string sql = "DELETE FROM admins WHERE steam_id = @steamId";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.ExecuteNonQuery();

        // Remove from runtime - find player if online
        var player = Utilities.GetPlayerFromSteamId(steamId);
        if (player != null)
        {
            AdminManager.RemovePlayerPermissions(player);
        }

        if (_enableLogging)
        {
            _database.LogAction("REMOVE_ADMIN", removedBySteamId, removedByName, steamId, admin.PlayerName, null);
        }

        return true;
    }

    public bool SetAdminGroup(ulong steamId, string groupName, ulong setBySteamId, string setByName)
    {
        var admin = GetAdmin(steamId);
        if (admin == null) return false;

        var group = GetGroup(groupName);
        if (group == null) return false;

        var connection = _database.GetConnection();
        const string sql = "UPDATE admins SET group_name = @groupName WHERE steam_id = @steamId";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@groupName", groupName);
        command.Parameters.AddWithValue("@steamId", (long)steamId);
        command.ExecuteNonQuery();

        // Update runtime permissions
        var player = Utilities.GetPlayerFromSteamId(steamId);
        if (player != null)
        {
            AdminManager.RemovePlayerPermissions(player);
        }

        var flags = new List<string>();
        flags.AddRange(group.GetFlags());
        if (!string.IsNullOrEmpty(admin.Flags))
        {
            flags.AddRange(admin.GetFlags());
        }

        AdminManager.AddPlayerPermissions(new SteamID(steamId), flags.Distinct().ToArray());

        if (_enableLogging)
        {
            _database.LogAction("SET_ADMIN_GROUP", setBySteamId, setByName, steamId, admin.PlayerName, $"Group: {groupName}");
        }

        return true;
    }

    #endregion

    #region Group Management

    public List<AdminGroup> GetAllGroups()
    {
        var connection = _database.GetConnection();
        const string sql = "SELECT id, name, flags, immunity, created_at, created_by_steam_id, created_by_name FROM admin_groups";

        using var command = new SqliteCommand(sql, connection);
        using var reader = command.ExecuteReader();

        var groups = new List<AdminGroup>();
        while (reader.Read())
        {
            groups.Add(new AdminGroup
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Flags = reader.GetString(2),
                Immunity = reader.GetInt32(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                CreatedBySteamId = (ulong)reader.GetInt64(5),
                CreatedByName = reader.GetString(6)
            });
        }

        return groups;
    }

    public AdminGroup? GetGroup(string name)
    {
        var connection = _database.GetConnection();
        const string sql = "SELECT id, name, flags, immunity, created_at, created_by_steam_id, created_by_name FROM admin_groups WHERE name = @name";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new AdminGroup
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Flags = reader.GetString(2),
            Immunity = reader.GetInt32(3),
            CreatedAt = DateTime.Parse(reader.GetString(4)),
            CreatedBySteamId = (ulong)reader.GetInt64(5),
            CreatedByName = reader.GetString(6)
        };
    }

    public bool AddGroup(string name, string flags, int immunity, ulong createdBySteamId, string createdByName)
    {
        if (GetGroup(name) != null) return false;

        var connection = _database.GetConnection();
        const string sql = """
            INSERT INTO admin_groups (name, flags, immunity, created_at, created_by_steam_id, created_by_name)
            VALUES (@name, @flags, @immunity, @createdAt, @createdBySteamId, @createdByName)
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@flags", flags);
        command.Parameters.AddWithValue("@immunity", immunity);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@createdBySteamId", (long)createdBySteamId);
        command.Parameters.AddWithValue("@createdByName", createdByName);
        command.ExecuteNonQuery();

        if (_enableLogging)
        {
            _database.LogAction("ADD_GROUP", createdBySteamId, createdByName, null, null, $"Group: {name}, Flags: {flags}");
        }

        return true;
    }

    public bool RemoveGroup(string name, ulong removedBySteamId, string removedByName)
    {
        var group = GetGroup(name);
        if (group == null) return false;

        var connection = _database.GetConnection();

        // Remove group assignment from admins
        const string updateAdminsSql = "UPDATE admins SET group_name = NULL WHERE group_name = @name";
        using (var updateCommand = new SqliteCommand(updateAdminsSql, connection))
        {
            updateCommand.Parameters.AddWithValue("@name", name);
            updateCommand.ExecuteNonQuery();
        }

        // Delete group
        const string sql = "DELETE FROM admin_groups WHERE name = @name";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@name", name);
        command.ExecuteNonQuery();

        if (_enableLogging)
        {
            _database.LogAction("REMOVE_GROUP", removedBySteamId, removedByName, null, null, $"Group: {name}");
        }

        return true;
    }

    #endregion
}
