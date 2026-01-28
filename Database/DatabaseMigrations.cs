using Microsoft.Data.Sqlite;

namespace CS2Admin.Database;

public static class DatabaseMigrations
{
    public static void RunMigrations(SqliteConnection connection)
    {
        CreateBansTable(connection);
        CreateMutesTable(connection);
        CreateLogsTable(connection);
        CreateAdminGroupsTable(connection);
        CreateAdminsTable(connection);
    }

    private static void CreateBansTable(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS bans (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                steam_id INTEGER NOT NULL,
                player_name TEXT NOT NULL,
                reason TEXT NOT NULL,
                admin_steam_id INTEGER NOT NULL,
                admin_name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT,
                unbanned_at TEXT,
                unbanned_by_steam_id INTEGER,
                unbanned_by_name TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_bans_steam_id ON bans(steam_id);
            CREATE INDEX IF NOT EXISTS idx_bans_expires_at ON bans(expires_at);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void CreateMutesTable(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS mutes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                steam_id INTEGER NOT NULL,
                player_name TEXT NOT NULL,
                reason TEXT NOT NULL,
                admin_steam_id INTEGER NOT NULL,
                admin_name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT,
                unmuted_at TEXT,
                unmuted_by_steam_id INTEGER,
                unmuted_by_name TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_mutes_steam_id ON mutes(steam_id);
            CREATE INDEX IF NOT EXISTS idx_mutes_expires_at ON mutes(expires_at);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void CreateLogsTable(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                action TEXT NOT NULL,
                admin_steam_id INTEGER NOT NULL,
                admin_name TEXT NOT NULL,
                target_steam_id INTEGER,
                target_name TEXT,
                details TEXT,
                created_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_logs_created_at ON logs(created_at);
            CREATE INDEX IF NOT EXISTS idx_logs_admin_steam_id ON logs(admin_steam_id);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void CreateAdminGroupsTable(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS admin_groups (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                flags TEXT NOT NULL,
                immunity INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                created_by_steam_id INTEGER NOT NULL,
                created_by_name TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_admin_groups_name ON admin_groups(name);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void CreateAdminsTable(SqliteConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS admins (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                steam_id INTEGER NOT NULL UNIQUE,
                player_name TEXT NOT NULL,
                flags TEXT,
                group_name TEXT,
                created_at TEXT NOT NULL,
                created_by_steam_id INTEGER NOT NULL,
                created_by_name TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_admins_steam_id ON admins(steam_id);
            """;

        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
