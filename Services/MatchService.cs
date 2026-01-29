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
    private bool _isKnifeRound;
    private bool _isKnifeOnly;
    private int _knifeRoundWinnerTeam; // 2 = T, 3 = CT
    private bool _waitingForSideChoice;
    private int _warmupMoney;

    public bool IsPaused => _isPaused;
    public bool IsWarmup => _isWarmup;
    public bool IsKnifeRound => _isKnifeRound;
    public bool IsKnifeOnly => _isKnifeOnly;
    public bool WaitingForSideChoice => _waitingForSideChoice;
    public int KnifeRoundWinnerTeam => _knifeRoundWinnerTeam;

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

        // Force end warmup completely
        Server.ExecuteCommand("mp_warmup_pausetimer 0");
        Server.ExecuteCommand("mp_warmuptime 0");
        Server.ExecuteCommand("mp_warmup_end");

        // Knife round settings - disable buying completely
        Server.ExecuteCommand("mp_respawn_on_death_ct 0");
        Server.ExecuteCommand("mp_respawn_on_death_t 0");
        Server.ExecuteCommand("mp_free_armor 1");
        Server.ExecuteCommand("mp_give_player_c4 0");
        Server.ExecuteCommand("mp_ct_default_secondary \"\"");
        Server.ExecuteCommand("mp_t_default_secondary \"\"");
        Server.ExecuteCommand("mp_buytime 0");
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_maxmoney 0");
        Server.ExecuteCommand("mp_restartgame 3");

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
        Server.ExecuteCommand("mp_startmoney 0");
        Server.ExecuteCommand("mp_maxmoney 0");

        // Restart round to apply settings cleanly
        Server.ExecuteCommand("mp_restartgame 1");

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
