using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CS2Admin.Database;
using CS2Admin.Models;

namespace CS2Admin.Services;

public class MatchService
{
    private readonly DatabaseService? _database;
    private readonly bool _enableLogging;
    private readonly int _warmupMoney;
    private readonly string _chatPrefix;
    private readonly int _teamPauseLimit;
    private readonly int _votePauseDuration;
    private readonly int _disconnectPauseDuration;
    private BasePlugin? _plugin;
    private bool _isPaused;
    private bool _isWarmup;
    private bool _isKnifeRound;
    private bool _isKnifeOnly;
    private int _knifeRoundWinnerTeam; // 2 = T, 3 = CT
    private bool _waitingForSideChoice;

    // Pause state machine
    private PauseType _activePauseType = PauseType.None;
    private int _pauseTeam; // 2=T, 3=CT, 0=none
    private int _pauseRemainingSeconds;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _pauseTimer;
    private Dictionary<int, int> _teamPausesUsed = new() { { 2, 0 }, { 3, 0 } };
    private HashSet<int> _teamDisconnectPauseUsed = new();
    private ulong _disconnectedPlayerSteamId;

    public bool IsPaused => _isPaused;
    public bool IsWarmup => _isWarmup;
    public bool IsKnifeRound => _isKnifeRound;
    public bool IsKnifeOnly => _isKnifeOnly;
    public bool WaitingForSideChoice => _waitingForSideChoice;
    public int KnifeRoundWinnerTeam => _knifeRoundWinnerTeam;
    public PauseType ActivePauseType => _activePauseType;
    public int PauseRemainingSeconds => _pauseRemainingSeconds;

    public int GetTeamPausesRemaining(int team) => _teamPauseLimit - _teamPausesUsed.GetValueOrDefault(team, 0);

    public MatchService(DatabaseService? database = null, bool enableLogging = false, int warmupMoney = 60000,
        int teamPauseLimit = 3, int votePauseDuration = 60, int disconnectPauseDuration = 120,
        string chatPrefix = "[CS2Admin]")
    {
        _database = database;
        _enableLogging = enableLogging;
        _warmupMoney = warmupMoney;
        _teamPauseLimit = teamPauseLimit;
        _votePauseDuration = votePauseDuration;
        _disconnectPauseDuration = disconnectPauseDuration;
        _chatPrefix = chatPrefix;
    }

    public void SetPlugin(BasePlugin plugin)
    {
        _plugin = plugin;
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
        Server.ExecuteCommand("mp_buy_anywhere 0");
        Server.ExecuteCommand("mp_buytime 9999");
        Server.ExecuteCommand("mp_free_armor 2");
        Server.ExecuteCommand("mp_weapons_allow_zeus 1");
        Server.ExecuteCommand("sv_infinite_ammo 0");
        Server.ExecuteCommand("mp_death_drop_gun 0");
        Server.ExecuteCommand("mp_respawn_on_death_ct 1");
        Server.ExecuteCommand("mp_respawn_on_death_t 1");

        // Give money immediately to all players already on the server
        GiveMoneyToAllPlayers(_warmupMoney);

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
        _activePauseType = PauseType.Admin;
        _pauseTeam = 0;

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("PAUSE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void UnpauseMatch(CCSPlayerController? admin = null)
    {
        if (!_isPaused) return;

        KillPauseTimer();
        Server.ExecuteCommand("mp_unpause_match");
        _isPaused = false;
        _activePauseType = PauseType.None;
        _pauseTeam = 0;
        _pauseRemainingSeconds = 0;
        _disconnectedPlayerSteamId = 0;

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("UNPAUSE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public (bool Success, string Message) PauseMatchTimed(PauseType type, int team, int durationSeconds)
    {
        if (_isPaused) return (false, "Match is already paused.");

        if (_isWarmup || _isKnifeRound || _waitingForSideChoice)
            return (false, "Can only pause during a live match.");

        if (type == PauseType.Vote)
        {
            var used = _teamPausesUsed.GetValueOrDefault(team, 0);
            if (used >= _teamPauseLimit)
                return (false, $"Your team has used all {_teamPauseLimit} pauses.");
            _teamPausesUsed[team] = used + 1;
        }

        Server.ExecuteCommand("mp_pause_match");
        _isPaused = true;
        _activePauseType = type;
        _pauseTeam = team;
        _pauseRemainingSeconds = durationSeconds;

        _pauseTimer = _plugin?.AddTimer(1.0f, OnPauseTimerTick, TimerFlags.REPEAT);

        BroadcastPauseCenter();

        return (true, "");
    }

    public (bool Success, string Message) PauseForDisconnect(ulong steamId, int team)
    {
        if (_teamDisconnectPauseUsed.Contains(team))
            return (false, "Your team has already used its disconnect pause.");

        _disconnectedPlayerSteamId = steamId;
        _teamDisconnectPauseUsed.Add(team);
        return PauseMatchTimed(PauseType.Disconnect, team, _disconnectPauseDuration);
    }

    public void OnPlayerReconnect(ulong steamId)
    {
        if (_isPaused && _activePauseType == PauseType.Disconnect && _disconnectedPlayerSteamId == steamId)
        {
            ForceUnpause();
            Server.PrintToChatAll($"{_chatPrefix} Disconnected player reconnected. Match resumed.");
        }
    }

    public void ResetPauseState()
    {
        KillPauseTimer();
        _isPaused = false;
        _activePauseType = PauseType.None;
        _pauseTeam = 0;
        _pauseRemainingSeconds = 0;
        _disconnectedPlayerSteamId = 0;
        _teamPausesUsed = new Dictionary<int, int> { { 2, 0 }, { 3, 0 } };
        _teamDisconnectPauseUsed = new HashSet<int>();
    }

    private void OnPauseTimerTick()
    {
        if (!_isPaused || _activePauseType == PauseType.None)
        {
            KillPauseTimer();
            return;
        }

        _pauseRemainingSeconds--;
        BroadcastPauseCenter();

        if (_pauseRemainingSeconds <= 0)
        {
            ForceUnpause();
        }
    }

    private void BroadcastPauseCenter()
    {
        var typeLabel = _activePauseType switch
        {
            PauseType.Vote => "TACTICAL PAUSE",
            PauseType.Disconnect => "DISCONNECT PAUSE",
            _ => "PAUSED"
        };

        var timeText = _pauseRemainingSeconds > 0 ? $" ({_pauseRemainingSeconds}s)" : "";
        var message = $"{typeLabel}{timeText}";

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid && !player.IsBot && !player.IsHLTV)
            {
                player.PrintToCenter(message);
            }
        }
    }

    private void ForceUnpause()
    {
        KillPauseTimer();
        Server.ExecuteCommand("mp_unpause_match");
        _isPaused = false;
        _activePauseType = PauseType.None;
        _pauseTeam = 0;
        _pauseRemainingSeconds = 0;
        _disconnectedPlayerSteamId = 0;

        Server.PrintToChatAll($"{_chatPrefix} Match resumed.");
    }

    private void KillPauseTimer()
    {
        _pauseTimer?.Kill();
        _pauseTimer = null;
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

    private void GiveMoneyToAllPlayers(int amount)
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player.IsValid && !player.IsBot && !player.IsHLTV && player.PlayerPawn?.Value != null)
            {
                GiveMoneyToPlayer(player);
            }
        }
    }

    public void GiveMoneyToPlayer(CCSPlayerController player)
    {
        if (player.PlayerPawn?.Value == null) return;

        var moneyServices = player.InGameMoneyServices;
        if (moneyServices != null)
        {
            moneyServices.Account = _warmupMoney;
            Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInGameMoneyServices");
        }
    }

    public void StartKnifeRound(CCSPlayerController? admin = null)
    {
        _isWarmup = false;
        _isKnifeRound = true;
        _isKnifeOnly = true;
        _waitingForSideChoice = false;
        _knifeRoundWinnerTeam = 0;

        // Knife round settings - disable buying completely
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");
        Server.ExecuteCommand("mp_free_armor 1");
        Server.ExecuteCommand("mp_give_player_c4 0");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_buy_during_immunity_time 0");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_maxmoney 0");

        // First restart to apply settings
        Server.ExecuteCommand("mp_restartgame 1");

        // Second restart after delay to ensure settings are applied
        _plugin?.AddTimer(3.0f, () =>
        {
            if (_isKnifeRound)
            {
                Server.ExecuteCommand("mp_restartgame 1");
            }
        });

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("KNIFE_ROUND_START", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void EndKnifeRound(int winnerTeam)
    {
        _knifeRoundWinnerTeam = winnerTeam;
        _waitingForSideChoice = true;
        _isKnifeRound = false;

        // Pause match while waiting for side choice
        Server.ExecuteCommand("mp_pause_match");
    }

    public void ChooseSide(bool stayOnSide, CCSPlayerController? admin = null)
    {
        if (!_waitingForSideChoice) return;

        _waitingForSideChoice = false;
        _isKnifeOnly = false;

        // Reset to normal settings
        Server.ExecuteCommand("mp_free_armor 0");
        Server.ExecuteCommand("mp_give_player_c4 1");
        Server.ExecuteCommand("mp_ct_default_secondary \"weapon_hkp2000\"");
        Server.ExecuteCommand("mp_t_default_secondary \"weapon_glock\"");
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_buy_during_immunity_time 1");
        Server.ExecuteCommand("mp_startmoney 800");
        Server.ExecuteCommand("mp_maxmoney 16000");

        if (!stayOnSide)
        {
            // Switch sides
            Server.ExecuteCommand("mp_swapteams");
        }

        // Unpause and restart for actual match
        Server.ExecuteCommand("mp_unpause_match");
        Server.ExecuteCommand("mp_restartgame 3");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("SIDE_CHOICE", admin.SteamID, admin.PlayerName, null, null, stayOnSide ? "Stay" : "Switch");
        }
    }

    public void EnableKnifeOnly(CCSPlayerController? admin = null)
    {
        _isKnifeOnly = true;

        // Knife only settings - disable buying completely
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_give_player_c4 0");
        Server.ExecuteCommand("mp_free_armor 1");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_buy_during_immunity_time 0");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_maxmoney 0");

        // First restart to apply settings
        Server.ExecuteCommand("mp_restartgame 1");

        // Second restart after delay to ensure settings are applied
        _plugin?.AddTimer(3.0f, () =>
        {
            if (_isKnifeOnly)
            {
                Server.ExecuteCommand("mp_restartgame 1");
            }
        });

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("KNIFE_ONLY_ENABLE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void DisableKnifeOnly(CCSPlayerController? admin = null)
    {
        _isKnifeOnly = false;

        // Restore normal settings
        Server.ExecuteCommand("mp_ct_default_secondary \"weapon_hkp2000\"");
        Server.ExecuteCommand("mp_t_default_secondary \"weapon_glock\"");
        Server.ExecuteCommand("mp_give_player_c4 1");
        Server.ExecuteCommand("mp_free_armor 0");
        Server.ExecuteCommand("mp_buytime 20");
        Server.ExecuteCommand("mp_buy_during_immunity_time 1");
        Server.ExecuteCommand("mp_startmoney 800");
        Server.ExecuteCommand("mp_maxmoney 16000");

        // Restart round to apply settings
        Server.ExecuteCommand("mp_restartgame 1");

        if (_enableLogging && _database != null && admin != null)
        {
            _database.LogAction("KNIFE_ONLY_DISABLE", admin.SteamID, admin.PlayerName, null, null, null);
        }
    }

    public void StripAllPlayersWeapons()
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player.IsValid && !player.IsBot && !player.IsHLTV && player.PawnIsAlive)
            {
                StripPlayerWeapons(player);
            }
        }
    }

    public void StripPlayerWeapons(CCSPlayerController player)
    {
        if (player.PlayerPawn?.Value == null) return;

        var pawn = player.PlayerPawn.Value;
        if (pawn.WeaponServices?.MyWeapons == null) return;

        // Collect weapon names to remove first to avoid modifying collection while iterating
        var weaponsToRemove = new List<string>();

        foreach (var weapon in pawn.WeaponServices.MyWeapons)
        {
            if (weapon.Value == null) continue;

            var weaponName = weapon.Value.DesignerName;
            if (!string.IsNullOrEmpty(weaponName) &&
                !weaponName.Contains("knife") &&
                !weaponName.Contains("bayonet"))
            {
                weaponsToRemove.Add(weaponName);
            }
        }

        // Remove collected weapons using safe method
        foreach (var weaponName in weaponsToRemove)
        {
            player.RemoveItemByDesignerName(weaponName);
        }
    }

    public void ResetKnifeRoundState()
    {
        _isKnifeRound = false;
        _isKnifeOnly = false;
        _waitingForSideChoice = false;
        _knifeRoundWinnerTeam = 0;
    }
}
