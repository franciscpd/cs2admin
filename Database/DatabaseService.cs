using Microsoft.Data.Sqlite;

namespace CS2Admin.Database;

public class DatabaseService : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _disposed;

    public DatabaseService(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={databasePath}";
    }

    public SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
            DatabaseMigrations.RunMigrations(_connection);
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }

        return _connection;
    }

    public void LogAction(string action, ulong adminSteamId, string adminName,
        ulong? targetSteamId = null, string? targetName = null, string? details = null)
    {
        var connection = GetConnection();
        const string sql = """
            INSERT INTO logs (action, admin_steam_id, admin_name, target_steam_id, target_name, details, created_at)
            VALUES (@action, @adminSteamId, @adminName, @targetSteamId, @targetName, @details, @createdAt)
            """;

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue("@adminSteamId", (long)adminSteamId);
        command.Parameters.AddWithValue("@adminName", adminName);
        command.Parameters.AddWithValue("@targetSteamId", targetSteamId.HasValue ? (object)(long)targetSteamId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@targetName", targetName ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }

        _disposed = true;
    }
}
