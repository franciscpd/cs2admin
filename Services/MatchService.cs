using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2Admin.Database;

namespace CS2Admin.Services;

public class MatchService
{
    private readonly DatabaseService? _database;
    private readonly bool _enableLogging;
    private bool _isPaused;
    private bool _isWarmup;
    private int _warmupMoney;

    public bool IsPaused => _isPaused;
    public bool IsWarmup => _isWarmup;

    public MatchService(DatabaseService? database = null, bool enableLogging = false, int warmupMoney = 60000)
    {
        _database = database;
        _enableLogging = enableLogging;
        _warmupMoney = warmupMoney;
    }

    public void StartWarmup(CCSPlayerController? admin = null)
    {
        if (_isWarmup) return;

        _isWarmup = true;

        // Set warmup cvars
        Server.ExecuteCommand("mp_warmup_start");
        Server.ExecuteCommand("mp_warmup_pausetimer 1");
        Server.ExecuteCommand("mp_warmuptime 9999");
        Server.ExecuteCommand($"mp_startmoney {_warmupMoney}");
        Server.ExecuteCommand($"mp_maxmoney {_warmupMoney}");
        Server.ExecuteCommand($"mp_afterroundmoney {_warmupMoney}");
        Server.ExecuteCommand("mp_buy_anywhere 1");
        Server.ExecuteCommand("mp_buytime 9999");
        Server.ExecuteCommand("mp_free_armor 2");
        Server.ExecuteCommand("mp_weapons_allow_zeus 1");
        Server.ExecuteCommand("sv_infinite_ammo 0");
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("WARMUP_START", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void EndWarmup(CCSPlayerController? admin = null)
    {
        if (!_isWarmup) return;

        _isWarmup = false;

        // Reset cvars to normal competitive values
        Server.ExecuteCommand("mp_warmup_pausetimer 0");
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_free_armor 0");
        Server.ExecuteCommand("mp_startmoney 800");
        Server.ExecuteCommand("mp_maxmoney 16000");
        Server.ExecuteCommand("mp_afterroundmoney 0");
        Server.ExecuteCommand("mp_death_drop_gun 1");
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");
        Server.ExecuteCommand("mp_warmup_end");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("WARMUP_END", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void StartMatch(CCSPlayerController? admin = null)
    {
        EndWarmup(admin);

        // Restart the game to start fresh
        Server.ExecuteCommand("mp_restartgame 3");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("MATCH_START", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void PauseMatch(CCSPlayerController? admin = null)
    {
        if (_isPaused) return;

        Server.ExecuteCommand("mp_pause_match");
        _isPaused = true;

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("PAUSE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void UnpauseMatch(CCSPlayerController? admin = null)
    {
        if (!_isPaused) return;

        Server.ExecuteCommand("mp_unpause_match");
        _isPaused = false;

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("UNPAUSE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void RestartMatch(CCSPlayerController? admin = null)
    {
        Server.ExecuteCommand("mp_restartgame 1");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("RESTART", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void ChangeMap(string mapName, CCSPlayerController? admin = null)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return;

        // Reset warmup state on map change
        _isWarmup = false;

        // Check if it's a workshop map
        if (mapName.StartsWith("workshop/") || ulong.TryParse(mapName, out _))
        {
            Server.ExecuteCommand($"host_workshop_map {mapName}");
        }
        else
        {
            Server.ExecuteCommand($"changelevel {mapName}");
        }

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("CHANGEMAP", admin.SteamID, admin.PlayerName, null, null, $"Map: {mapName}");
        }
    }

    public void ForceRespawnAllPlayers()
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player.IsValid && !player.IsBot && !player.IsHLTV && player.PawnIsAlive == false)
            {
                player.Respawn();
            }
        }
    }
}
