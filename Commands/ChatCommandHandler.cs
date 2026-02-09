using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Models;
using CS2Admin.Services;
using CS2Admin.Utils;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;

namespace CS2Admin.Commands;

public class ChatCommandHandler
{
    private readonly BasePlugin _plugin;
    private readonly PluginConfig _config;
    private readonly PlayerService _playerService;
    private readonly BanService _banService;
    private readonly MuteService _muteService;
    private readonly MatchService _matchService;
    private readonly VoteService _voteService;
    private readonly AdminManagementCommands _adminManagementCommands;
    private readonly AdminService _adminService;
    private HashSet<ulong> _selectedForTeam1 = new();

    private static readonly string[] AvailableFlags =
    [
        "@css/kick",
        "@css/ban",
        "@css/slay",
        "@css/chat",
        "@css/changemap",
        "@css/generic",
        "@css/root",
        "@css/vip",
        "@css/reservation"
    ];

    public ChatCommandHandler(
        BasePlugin plugin,
        PluginConfig config,
        PlayerService playerService,
        BanService banService,
        MuteService muteService,
        MatchService matchService,
        VoteService voteService,
        AdminManagementCommands adminManagementCommands,
        AdminService adminService)
    {
        _plugin = plugin;
        _config = config;
        _playerService = playerService;
        _banService = banService;
        _muteService = muteService;
        _matchService = matchService;
        _voteService = voteService;
        _adminManagementCommands = adminManagementCommands;
        _adminService = adminService;
    }

    public HookResult OnPlayerChat(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid) return HookResult.Continue;

        var message = info.GetArg(1)?.Trim('"', ' ');
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith('!'))
            return HookResult.Continue;

        // Check if player is muted
        if (_muteService.IsMuted(player.SteamID))
        {
            player.PrintToChat($"{_config.ChatPrefix} You are muted.");
            return HookResult.Handled;
        }

        var parts = message[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return HookResult.Continue;

        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        var handled = command switch
        {
            // Help command
            "help" => HandleHelp(player),

            // Public vote commands
            "votekick" => HandleVoteKick(player, args),
            "votepause" => HandleVotePause(player),
            "voterestart" => HandleVoteRestart(player),
            "votechangemap" or "votemap" => HandleVoteChangeMap(player, args),

            // Admin commands
            "kick" => HandleKick(player, args),
            "ban" => HandleBan(player, args),
            "unban" => HandleUnban(player, args),
            "mute" => HandleMute(player, args),
            "unmute" => HandleUnmute(player, args),
            "slay" => HandleSlay(player, args),
            "slap" => HandleSlap(player, args),
            "respawn" => HandleRespawn(player, args),
            "changemap" or "map" => HandleChangeMap(player, args),
            "pause" => HandlePause(player),
            "unpause" => HandleUnpause(player),
            "restart" => HandleRestart(player),
            "start" => HandleStart(player),
            "warmup" => HandleWarmup(player),
            "endwarmup" => HandleEndWarmup(player),
            "knife" => HandleKnife(player),
            "teams" => HandleTeams(player),

            // Admin management commands
            "add_admin" => _adminManagementCommands.HandleAddAdmin(player, args),
            "remove_admin" => _adminManagementCommands.HandleRemoveAdmin(player, args),
            "list_admins" => _adminManagementCommands.HandleListAdmins(player),
            "list_groups" => _adminManagementCommands.HandleListGroups(player),
            "set_group" => _adminManagementCommands.HandleSetGroup(player, args),
            "reload_admins" => _adminManagementCommands.HandleReloadAdmins(player),

            _ => false
        };

        return handled ? HookResult.Handled : HookResult.Continue;
    }

    #region Player Selection Menu

    private List<CCSPlayerController> GetSelectablePlayers(CCSPlayerController? excludePlayer = null)
    {
        return Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && !p.IsHLTV && (excludePlayer == null || p.SteamID != excludePlayer.SteamID))
            .ToList();
    }

    private void ShowPlayerSelectionMenu(CCSPlayerController admin, string title, Action<CCSPlayerController, CCSPlayerController> onSelect, bool excludeSelf = false)
    {
        var players = GetSelectablePlayers(excludeSelf ? admin : null);

        if (players.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No players available.");
            return;
        }

        var menu = new WasdMenu(title, _plugin);

        foreach (var target in players)
        {
            menu.AddItem(target.PlayerName, (p, opt) => onSelect(p, target));
        }

        menu.Display(admin, 30);
    }

    #endregion

    #region Vote Commands

    private bool HandleVoteKick(CCSPlayerController player, string[] args)
    {
        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Vote Kick - Select Player", (admin, target) =>
            {
                var result = _voteService.StartVote(VoteType.Kick, admin, targetPlayer: target);
                if (!result.Success)
                {
                    admin.PrintToChat($"{_config.ChatPrefix} {result.Message}");
                }
            }, excludeSelf: true);
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        if (target.SteamID == player.SteamID)
        {
            player.PrintToChat($"{_config.ChatPrefix} You cannot vote to kick yourself.");
            return true;
        }

        var result = _voteService.StartVote(VoteType.Kick, player, targetPlayer: target);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }

        return true;
    }

    private bool HandleVotePause(CCSPlayerController player)
    {
        var result = _voteService.StartVote(VoteType.Pause, player);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
        return true;
    }

    private bool HandleVoteRestart(CCSPlayerController player)
    {
        var result = _voteService.StartVote(VoteType.Restart, player);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
        return true;
    }

    private bool HandleVoteChangeMap(CCSPlayerController player, string[] args)
    {
        if (args.Length < 1)
        {
            ShowMapSelectionMenu(player);
            return true;
        }

        var result = _voteService.StartVote(VoteType.ChangeMap, player, targetMap: args[0]);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }

        return true;
    }

    private void ShowMapSelectionMenu(CCSPlayerController player)
    {
        if (_config.VoteMaps.Count == 0)
        {
            player.PrintToChat($"{_config.ChatPrefix} No maps configured for voting.");
            return;
        }

        var menu = new WasdMenu("Vote Map - Select Map", _plugin);

        foreach (var map in _config.VoteMaps)
        {
            var mapName = map; // Capture for closure
            menu.AddItem(map, (p, opt) =>
            {
                var result = _voteService.StartVote(VoteType.ChangeMap, p, targetMap: mapName);
                if (!result.Success)
                {
                    p.PrintToChat($"{_config.ChatPrefix} {result.Message}");
                }
            });
        }

        menu.Display(player, 30);
    }

    #endregion

    #region Admin Commands

    private bool HandleKick(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/kick"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Kick - Select Player", (admin, target) =>
            {
                var reason = _config.DefaultKickReason;
                _playerService.KickPlayer(target, admin, reason);
                Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was kicked by {admin.PlayerName}. Reason: {reason}");
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        var reason = args.Length > 1 ? string.Join(" ", args[1..]) : _config.DefaultKickReason;
        _playerService.KickPlayer(target, player, reason);

        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was kicked by {player.PlayerName}. Reason: {reason}");
        return true;
    }

    private bool HandleBan(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/ban"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Ban - Select Player", (admin, target) =>
            {
                ShowBanDurationMenu(admin, target);
            });
            return true;
        }

        if (args.Length < 2)
        {
            var target = PlayerFinder.Find(args[0]);
            if (target == null)
            {
                player.PrintToChat($"{_config.ChatPrefix} Player not found.");
                return true;
            }
            ShowBanDurationMenu(player, target);
            return true;
        }

        var targetPlayer = PlayerFinder.Find(args[0]);
        if (targetPlayer == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        var duration = TimeParser.Parse(args[1]);
        var reason = args.Length > 2 ? string.Join(" ", args[2..]) : _config.DefaultBanReason;

        _banService.BanPlayer(targetPlayer.SteamID, targetPlayer.PlayerName, player.SteamID, player.PlayerName, duration, reason);
        _playerService.KickPlayer(targetPlayer, player, $"Banned: {reason}");

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {targetPlayer.PlayerName} was banned by {player.PlayerName} for {durationText}. Reason: {reason}");
        return true;
    }

    private void ShowBanDurationMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = new WasdMenu($"Ban {target.PlayerName} - Duration", _plugin);

        menu.AddItem("30 minutes", (p, o) => ExecuteBan(p, target, TimeSpan.FromMinutes(30)));
        menu.AddItem("1 hour", (p, o) => ExecuteBan(p, target, TimeSpan.FromHours(1)));
        menu.AddItem("1 day", (p, o) => ExecuteBan(p, target, TimeSpan.FromDays(1)));
        menu.AddItem("1 week", (p, o) => ExecuteBan(p, target, TimeSpan.FromDays(7)));
        menu.AddItem("Permanent", (p, o) => ExecuteBan(p, target, null));

        menu.Display(admin, 30);
    }

    private void ExecuteBan(CCSPlayerController admin, CCSPlayerController target, TimeSpan? duration)
    {
        var reason = _config.DefaultBanReason;
        _banService.BanPlayer(target.SteamID, target.PlayerName, admin.SteamID, admin.PlayerName, duration, reason);
        _playerService.KickPlayer(target, admin, $"Banned: {reason}");

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was banned by {admin.PlayerName} for {durationText}. Reason: {reason}");
    }

    private bool HandleUnban(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/ban"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: !unban <steamid>");
            return true;
        }

        if (!ulong.TryParse(args[0], out var steamId))
        {
            player.PrintToChat($"{_config.ChatPrefix} Invalid SteamID.");
            return true;
        }

        if (_banService.UnbanPlayer(steamId, player.SteamID, player.PlayerName))
        {
            player.PrintToChat($"{_config.ChatPrefix} Player unbanned successfully.");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} No active ban found for this SteamID.");
        }

        return true;
    }

    private bool HandleMute(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/chat"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Mute - Select Player", (admin, target) =>
            {
                ShowMuteDurationMenu(admin, target);
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        TimeSpan? duration = null;
        var reason = _config.DefaultMuteReason;

        if (args.Length > 1)
        {
            duration = TimeParser.Parse(args[1]);
        }

        if (args.Length > 2)
        {
            reason = string.Join(" ", args[2..]);
        }

        _muteService.MutePlayer(target.SteamID, target.PlayerName, player.SteamID, player.PlayerName, duration, reason);

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was muted by {player.PlayerName} for {durationText}.");
        return true;
    }

    private void ShowMuteDurationMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = new WasdMenu($"Mute {target.PlayerName} - Duration", _plugin);

        menu.AddItem("5 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(5)));
        menu.AddItem("15 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(15)));
        menu.AddItem("30 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(30)));
        menu.AddItem("1 hour", (p, o) => ExecuteMute(p, target, TimeSpan.FromHours(1)));
        menu.AddItem("Permanent", (p, o) => ExecuteMute(p, target, null));

        menu.Display(admin, 30);
    }

    private void ExecuteMute(CCSPlayerController admin, CCSPlayerController target, TimeSpan? duration)
    {
        var reason = _config.DefaultMuteReason;
        _muteService.MutePlayer(target.SteamID, target.PlayerName, admin.SteamID, admin.PlayerName, duration, reason);

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was muted by {admin.PlayerName} for {durationText}.");
    }

    private bool HandleUnmute(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/chat"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Unmute - Select Player", (admin, target) =>
            {
                if (_muteService.UnmutePlayer(target.SteamID, admin.SteamID, admin.PlayerName))
                {
                    Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was unmuted by {admin.PlayerName}.");
                }
                else
                {
                    admin.PrintToChat($"{_config.ChatPrefix} Player is not muted.");
                }
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        if (_muteService.UnmutePlayer(target.SteamID, player.SteamID, player.PlayerName))
        {
            Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was unmuted by {player.PlayerName}.");
        }
        else
        {
            player.PrintToChat($"{_config.ChatPrefix} Player is not muted.");
        }

        return true;
    }

    private bool HandleSlay(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/slay"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Slay - Select Player", (admin, target) =>
            {
                _playerService.SlayPlayer(target, admin);
                Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was slayed by {admin.PlayerName}.");
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        _playerService.SlayPlayer(target, player);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was slayed by {player.PlayerName}.");
        return true;
    }

    private bool HandleSlap(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/slay"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Slap - Select Player", (admin, target) =>
            {
                ShowSlapDamageMenu(admin, target);
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        var damage = 0;
        if (args.Length > 1 && int.TryParse(args[1], out var d))
        {
            damage = d;
        }

        _playerService.SlapPlayer(target, player, damage);
        player.PrintToChat($"{_config.ChatPrefix} {target.PlayerName} was slapped{(damage > 0 ? $" for {damage} damage" : "")}.");
        return true;
    }

    private void ShowSlapDamageMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = new WasdMenu($"Slap {target.PlayerName} - Damage", _plugin);

        menu.AddItem("No damage", (p, o) => ExecuteSlap(p, target, 0));
        menu.AddItem("5 damage", (p, o) => ExecuteSlap(p, target, 5));
        menu.AddItem("10 damage", (p, o) => ExecuteSlap(p, target, 10));
        menu.AddItem("25 damage", (p, o) => ExecuteSlap(p, target, 25));
        menu.AddItem("50 damage", (p, o) => ExecuteSlap(p, target, 50));

        menu.Display(admin, 30);
    }

    private void ExecuteSlap(CCSPlayerController admin, CCSPlayerController target, int damage)
    {
        _playerService.SlapPlayer(target, admin, damage);
        admin.PrintToChat($"{_config.ChatPrefix} {target.PlayerName} was slapped{(damage > 0 ? $" for {damage} damage" : "")}.");
    }

    private bool HandleRespawn(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/slay"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            ShowPlayerSelectionMenu(player, "Respawn - Select Player", (admin, target) =>
            {
                _playerService.RespawnPlayer(target, admin);
                admin.PrintToChat($"{_config.ChatPrefix} {target.PlayerName} was respawned.");
            });
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        _playerService.RespawnPlayer(target, player);
        player.PrintToChat($"{_config.ChatPrefix} {target.PlayerName} was respawned.");
        return true;
    }

    private bool HandleChangeMap(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/changemap"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: !changemap <map>");
            return true;
        }

        var mapName = args[0];
        Server.PrintToChatAll($"{_config.ChatPrefix} Changing map to {mapName}...");
        _matchService.ChangeMap(mapName, player);
        return true;
    }

    private bool HandlePause(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (_matchService.IsPaused)
        {
            player.PrintToChat($"{_config.ChatPrefix} Match is already paused.");
            return true;
        }

        _matchService.PauseMatch(player);
        Server.PrintToChatAll($"{_config.ChatPrefix} Match paused by {player.PlayerName}.");
        return true;
    }

    private bool HandleUnpause(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (!_matchService.IsPaused)
        {
            player.PrintToChat($"{_config.ChatPrefix} Match is not paused.");
            return true;
        }

        _matchService.UnpauseMatch(player);
        Server.PrintToChatAll($"{_config.ChatPrefix} Match unpaused by {player.PlayerName}.");
        return true;
    }

    private bool HandleRestart(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        Server.PrintToChatAll($"{_config.ChatPrefix} Match restarting by {player.PlayerName}...");
        _matchService.RestartMatch(player);
        return true;
    }

    private bool HandleStart(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (!_matchService.IsWarmup)
        {
            player.PrintToChat($"{_config.ChatPrefix} Server is not in warmup mode.");
            return true;
        }

        var playerCount = PlayerFinder.GetPlayerCount();
        if (playerCount < _config.MinPlayersToStart)
        {
            player.PrintToChat($"{_config.ChatPrefix} Not enough players. Need at least {_config.MinPlayersToStart} players.");
            return true;
        }

        if (_config.EnableKnifeRound)
        {
            Server.PrintToChatAll($"{_config.ChatPrefix} {_config.KnifeRoundMessage}");
            Server.PrintToChatAll($"{_config.ChatPrefix} Match started by {player.PlayerName}.");
            _matchService.StartKnifeRound(player);
        }
        else
        {
            Server.PrintToChatAll($"{_config.ChatPrefix} {_config.MatchStartMessage}");
            Server.PrintToChatAll($"{_config.ChatPrefix} Match started by {player.PlayerName}.");
            _matchService.StartMatch(player);
        }

        return true;
    }

    private bool HandleWarmup(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (_matchService.IsWarmup)
        {
            player.PrintToChat($"{_config.ChatPrefix} Server is already in warmup mode.");
            return true;
        }

        Server.PrintToChatAll($"{_config.ChatPrefix} Warmup started by {player.PlayerName}.");
        Server.PrintToChatAll($"{_config.ChatPrefix} {_config.WarmupMessage}");
        _matchService.StartWarmup(player);
        return true;
    }

    private bool HandleEndWarmup(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (!_matchService.IsWarmup)
        {
            player.PrintToChat($"{_config.ChatPrefix} Server is not in warmup mode.");
            return true;
        }

        var playerCount = PlayerFinder.GetPlayerCount();
        if (playerCount < _config.MinPlayersToStart)
        {
            player.PrintToChat($"{_config.ChatPrefix} Not enough players. Need at least {_config.MinPlayersToStart}.");
            return true;
        }

        Server.PrintToChatAll($"{_config.ChatPrefix} Warmup ended by {player.PlayerName}.");
        _matchService.EndWarmup(player);
        return true;
    }

    private bool HandleKnife(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (_matchService.IsKnifeOnly)
        {
            _matchService.DisableKnifeOnly(player);
            Server.PrintToChatAll($"{_config.ChatPrefix} Knife only mode disabled by {player.PlayerName}.");
        }
        else
        {
            _matchService.EnableKnifeOnly(player);
            Server.PrintToChatAll($"{_config.ChatPrefix} Knife only mode enabled by {player.PlayerName}.");
        }

        return true;
    }

    private bool HandleTeams(CCSPlayerController player)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (!_matchService.IsWarmup)
        {
            player.PrintToChat($"{_config.ChatPrefix} Teams can only be set during warmup.");
            return true;
        }

        _selectedForTeam1.Clear();
        ShowTeamSelectionMenu(player);
        return true;
    }

    private void ShowTeamSelectionMenu(CCSPlayerController admin)
    {
        var players = GetSelectablePlayers();

        if (players.Count < 2)
        {
            admin.PrintToChat($"{_config.ChatPrefix} Need at least 2 players to set teams.");
            return;
        }

        var menu = new WasdMenu("Select players for Team 1 (T)", _plugin);

        foreach (var target in players)
        {
            var steamId = target.SteamID;
            var isSelected = _selectedForTeam1.Contains(steamId);
            var label = isSelected ? $"[X] {target.PlayerName}" : $"[ ] {target.PlayerName}";

            menu.AddItem(label, (p, o) =>
            {
                if (_selectedForTeam1.Contains(steamId))
                    _selectedForTeam1.Remove(steamId);
                else
                    _selectedForTeam1.Add(steamId);

                ShowTeamSelectionMenu(p);
            });
        }

        menu.AddItem("✓ Confirm Teams", (p, o) => ExecuteTeamSelection(p));
        menu.AddItem("Cancel", (p, o) => { _selectedForTeam1.Clear(); });

        menu.Display(admin, 60);
    }

    private void ExecuteTeamSelection(CCSPlayerController admin)
    {
        var allPlayers = GetSelectablePlayers();
        var team1 = allPlayers.Where(p => _selectedForTeam1.Contains(p.SteamID)).ToList();
        var team2 = allPlayers.Where(p => !_selectedForTeam1.Contains(p.SteamID)).ToList();

        if (team1.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} You must select at least one player for Team 1.");
            ShowTeamSelectionMenu(admin);
            return;
        }

        if (team2.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} You must leave at least one player for Team 2.");
            ShowTeamSelectionMenu(admin);
            return;
        }

        _matchService.ApplyTeams(team1, team2);
        _selectedForTeam1.Clear();

        var team1Name = _matchService.Team1Name;
        var team2Name = _matchService.Team2Name;

        Server.PrintToChatAll($"{_config.ChatPrefix} Teams: {team1Name} (T) vs {team2Name} (CT)");
        Server.PrintToChatAll($"{_config.ChatPrefix} T: {string.Join(", ", team1.Select(p => p.PlayerName))}");
        Server.PrintToChatAll($"{_config.ChatPrefix} CT: {string.Join(", ", team2.Select(p => p.PlayerName))}");
    }

    #endregion

    #region Help Command

    private bool HandleHelp(CCSPlayerController player)
    {
        var menu = new WasdMenu("CS2Admin Help", _plugin);

        menu.AddItem("Vote Commands", (p, opt) => ShowVoteCommands(p));

        var hasKick = AdminManager.PlayerHasPermissions(player, "@css/kick");
        var hasBan = AdminManager.PlayerHasPermissions(player, "@css/ban");
        var hasChat = AdminManager.PlayerHasPermissions(player, "@css/chat");
        var hasSlay = AdminManager.PlayerHasPermissions(player, "@css/slay");
        var hasMap = AdminManager.PlayerHasPermissions(player, "@css/changemap");
        var hasGeneric = AdminManager.PlayerHasPermissions(player, "@css/generic");
        var hasRoot = AdminManager.PlayerHasPermissions(player, "@css/root");

        if (hasKick || hasBan || hasChat || hasSlay || hasMap || hasGeneric)
        {
            menu.AddItem("Admin Commands", (p, opt) => ShowAdminCommands(p));
        }

        if (hasRoot)
        {
            menu.AddItem("Root Admin Commands", (p, opt) => ShowRootAdminCommands(p));
        }

        menu.Display(player, 30);
        return true;
    }

    private void ShowVoteCommands(CCSPlayerController player)
    {
        var menu = new WasdMenu("Vote Commands", _plugin);

        menu.AddItem("Vote Kick Player", (p, o) => HandleVoteKick(p, Array.Empty<string>()));
        menu.AddItem("Vote Pause Match", (p, o) => HandleVotePause(p));
        menu.AddItem("Vote Restart Match", (p, o) => HandleVoteRestart(p));
        menu.AddItem("Vote Change Map", (p, o) => HandleVoteChangeMap(p, Array.Empty<string>()));
        menu.AddItem("« Back", (p, o) => HandleHelp(p));

        menu.Display(player, 30);
    }

    private void ShowAdminCommands(CCSPlayerController player)
    {
        var menu = new WasdMenu("Admin Commands", _plugin);

        if (AdminManager.PlayerHasPermissions(player, "@css/kick"))
            menu.AddItem("!kick <player> [reason]", DisableOption.DisableShowNumber);

        if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
            menu.AddItem("!ban/!unban <player> <dur> [reason]", DisableOption.DisableShowNumber);

        if (AdminManager.PlayerHasPermissions(player, "@css/chat"))
            menu.AddItem("!mute/!unmute <player>", DisableOption.DisableShowNumber);

        if (AdminManager.PlayerHasPermissions(player, "@css/slay"))
            menu.AddItem("!slay/!slap/!respawn <player>", DisableOption.DisableShowNumber);

        if (AdminManager.PlayerHasPermissions(player, "@css/changemap"))
            menu.AddItem("!map <mapname> - Change map", DisableOption.DisableShowNumber);

        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            menu.AddItem("!pause/!unpause - Pause match", DisableOption.DisableShowNumber);
            menu.AddItem("!warmup/!endwarmup - Warmup control", DisableOption.DisableShowNumber);
            menu.AddItem("!start - Start match", DisableOption.DisableShowNumber);
            menu.AddItem("!knife - Toggle knife only", DisableOption.DisableShowNumber);
            menu.AddItem("!teams - Set teams (warmup)", DisableOption.DisableShowNumber);
        }

        menu.AddItem("« Back", (p, o) => HandleHelp(p));
        menu.Display(player, 30);
    }

    private void ShowRootAdminCommands(CCSPlayerController player)
    {
        var menu = new WasdMenu("Root Admin Commands", _plugin);

        menu.AddItem("Add Admin", (p, o) => ShowAddAdminMenu(p));
        menu.AddItem("Remove Admin", (p, o) => ShowRemoveAdminMenu(p));
        menu.AddItem("Edit Admin Permissions", (p, o) => ShowEditAdminMenu(p));
        menu.AddItem("List Admins", (p, o) => { _adminManagementCommands.HandleListAdmins(p); });
        menu.AddItem("Set Admin Group", (p, o) => ShowSetGroupMenu(p));
        menu.AddItem("List Groups", (p, o) => { _adminManagementCommands.HandleListGroups(p); });
        menu.AddItem("Reload Admins", (p, o) => { _adminManagementCommands.HandleReloadAdmins(p); });
        menu.AddItem("« Back", (p, o) => HandleHelp(p));

        menu.Display(player, 30);
    }

    private void ShowAddAdminMenu(CCSPlayerController admin)
    {
        var players = GetSelectablePlayers();

        if (players.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No players available.");
            return;
        }

        var menu = new WasdMenu("Add Admin - Select Player", _plugin);

        foreach (var target in players)
        {
            menu.AddItem(target.PlayerName, (p, o) => ShowFlagSelectionMenu(p, target));
        }

        menu.AddItem("« Back", (p, o) => ShowRootAdminCommands(p));
        menu.Display(admin, 30);
    }

    private void ShowFlagSelectionMenu(CCSPlayerController admin, CCSPlayerController target)
    {
        var menu = new WasdMenu($"Flags for {target.PlayerName}", _plugin);

        menu.AddItem("Full Admin (@css/root)", (p, o) => ExecuteAddAdmin(p, target, "@css/root"));
        menu.AddItem("Moderator (kick,ban,chat)", (p, o) => ExecuteAddAdmin(p, target, "@css/kick,@css/ban,@css/chat"));
        menu.AddItem("Basic Admin (kick,slay)", (p, o) => ExecuteAddAdmin(p, target, "@css/kick,@css/slay"));
        menu.AddItem("Match Admin (generic,map)", (p, o) => ExecuteAddAdmin(p, target, "@css/generic,@css/changemap"));
        menu.AddItem("VIP", (p, o) => ExecuteAddAdmin(p, target, "@css/vip,@css/reservation"));
        menu.AddItem("« Back", (p, o) => ShowAddAdminMenu(p));

        menu.Display(admin, 30);
    }

    private void ExecuteAddAdmin(CCSPlayerController admin, CCSPlayerController target, string flags)
    {
        if (_adminService.AddAdmin(target.SteamID, target.PlayerName, flags, admin.SteamID, admin.PlayerName))
        {
            admin.PrintToChat($"{_config.ChatPrefix} Admin added: {target.PlayerName}");
            Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} is now an admin.");
        }
        else
        {
            admin.PrintToChat($"{_config.ChatPrefix} Admin already exists.");
        }
    }

    private void ShowRemoveAdminMenu(CCSPlayerController admin)
    {
        var admins = _adminService.GetAllAdmins();

        if (admins.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No admins found.");
            return;
        }

        var menu = new WasdMenu("Remove Admin - Select", _plugin);

        foreach (var targetAdmin in admins.Take(7))
        {
            menu.AddItem($"{targetAdmin.PlayerName}", (p, o) =>
            {
                if (_adminService.RemoveAdmin(targetAdmin.SteamId, p.SteamID, p.PlayerName))
                {
                    p.PrintToChat($"{_config.ChatPrefix} Admin removed: {targetAdmin.PlayerName}");
                }
                else
                {
                    p.PrintToChat($"{_config.ChatPrefix} Failed to remove admin.");
                }
            });
        }

        if (admins.Count > 7)
        {
            admin.PrintToChat($"{_config.ChatPrefix} Showing first 7 admins. Use !remove_admin <steamid> for others.");
        }

        menu.AddItem("« Back", (p, o) => ShowRootAdminCommands(p));
        menu.Display(admin, 30);
    }

    private void ShowEditAdminMenu(CCSPlayerController admin)
    {
        var admins = _adminService.GetAllAdmins();

        if (admins.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No admins found.");
            return;
        }

        var menu = new WasdMenu("Edit Admin - Select", _plugin);

        foreach (var targetAdmin in admins.Take(7))
        {
            menu.AddItem($"{targetAdmin.PlayerName}", (p, o) =>
                ShowAdminPermissionsMenu(p, targetAdmin));
        }

        if (admins.Count > 7)
        {
            admin.PrintToChat($"{_config.ChatPrefix} Showing first 7 admins.");
        }

        menu.AddItem("« Back", (p, o) => ShowRootAdminCommands(p));
        menu.Display(admin, 30);
    }

    private void ShowAdminPermissionsMenu(CCSPlayerController admin, Admin targetAdmin)
    {
        var menu = new WasdMenu($"Permissions: {targetAdmin.PlayerName}", _plugin);

        var currentFlags = targetAdmin.Flags ?? "None";
        menu.AddItem($"Current: {currentFlags}", DisableOption.DisableShowNumber);

        menu.AddItem("Add/Remove Permissions", (p, o) => ShowAddFlagsMenu(p, targetAdmin));
        menu.AddItem("Set Role Preset", (p, o) => ShowFlagSelectionMenuForEdit(p, targetAdmin));
        menu.AddItem("« Back", (p, o) => ShowEditAdminMenu(p));

        menu.Display(admin, 30);
    }

    private void ShowAddFlagsMenu(CCSPlayerController admin, Admin targetAdmin)
    {
        var updatedAdmin = _adminService.GetAdmin(targetAdmin.SteamId) ?? targetAdmin;

        var menu = new WasdMenu($"Toggle Flags: {updatedAdmin.PlayerName}", _plugin);

        foreach (var flag in AvailableFlags)
        {
            var hasFlag = updatedAdmin.Flags?.Contains(flag) ?? false;
            var label = hasFlag ? $"[X] {flag}" : $"[ ] {flag}";

            menu.AddItem(label, (p, o) => ExecuteToggleFlag(p, updatedAdmin, flag));
        }

        menu.AddItem("« Back", (p, o) => ShowAdminPermissionsMenu(p, updatedAdmin));
        menu.Display(admin, 30);
    }

    private void ExecuteToggleFlag(CCSPlayerController admin, Admin targetAdmin, string flag)
    {
        var currentFlags = targetAdmin.Flags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
            ?? new List<string>();

        if (currentFlags.Contains(flag))
        {
            currentFlags.Remove(flag);
            admin.PrintToChat($"{_config.ChatPrefix} Removed {flag} from {targetAdmin.PlayerName}");
        }
        else
        {
            currentFlags.Add(flag);
            admin.PrintToChat($"{_config.ChatPrefix} Added {flag} to {targetAdmin.PlayerName}");
        }

        var newFlags = string.Join(",", currentFlags);
        _adminService.UpdateAdminFlags(targetAdmin.SteamId, newFlags, admin.SteamID, admin.PlayerName);

        var updatedAdmin = _adminService.GetAdmin(targetAdmin.SteamId);
        if (updatedAdmin != null)
        {
            ShowAddFlagsMenu(admin, updatedAdmin);
        }
    }

    private void ShowFlagSelectionMenuForEdit(CCSPlayerController admin, Admin targetAdmin)
    {
        var menu = new WasdMenu($"Set Role: {targetAdmin.PlayerName}", _plugin);

        menu.AddItem("Full Admin (@css/root)", (p, o) => ExecuteSetFlags(p, targetAdmin, "@css/root"));
        menu.AddItem("Moderator", (p, o) => ExecuteSetFlags(p, targetAdmin, "@css/kick,@css/ban,@css/chat"));
        menu.AddItem("Basic Admin", (p, o) => ExecuteSetFlags(p, targetAdmin, "@css/kick,@css/slay"));
        menu.AddItem("Match Admin", (p, o) => ExecuteSetFlags(p, targetAdmin, "@css/generic,@css/changemap"));
        menu.AddItem("VIP", (p, o) => ExecuteSetFlags(p, targetAdmin, "@css/vip,@css/reservation"));
        menu.AddItem("« Back", (p, o) => ShowAdminPermissionsMenu(p, targetAdmin));

        menu.Display(admin, 30);
    }

    private void ExecuteSetFlags(CCSPlayerController admin, Admin targetAdmin, string flags)
    {
        _adminService.UpdateAdminFlags(targetAdmin.SteamId, flags, admin.SteamID, admin.PlayerName);
        admin.PrintToChat($"{_config.ChatPrefix} Updated {targetAdmin.PlayerName}'s permissions to: {flags}");

        var updatedAdmin = _adminService.GetAdmin(targetAdmin.SteamId);
        if (updatedAdmin != null)
        {
            ShowAdminPermissionsMenu(admin, updatedAdmin);
        }
    }

    private void ShowSetGroupMenu(CCSPlayerController admin)
    {
        var admins = _adminService.GetAllAdmins();

        if (admins.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No admins found.");
            return;
        }

        var menu = new WasdMenu("Set Group - Select Admin", _plugin);

        foreach (var targetAdmin in admins.Take(7))
        {
            menu.AddItem($"{targetAdmin.PlayerName}", (p, o) => ShowGroupSelectionForAdmin(p, targetAdmin.SteamId, targetAdmin.PlayerName));
        }

        if (admins.Count > 7)
        {
            admin.PrintToChat($"{_config.ChatPrefix} Showing first 7 admins. Use !set_group <steamid> <group> for others.");
        }

        menu.AddItem("« Back", (p, o) => ShowRootAdminCommands(p));
        menu.Display(admin, 30);
    }

    private void ShowGroupSelectionForAdmin(CCSPlayerController admin, ulong targetSteamId, string targetName)
    {
        var groups = _adminService.GetAllGroups();

        if (groups.Count == 0)
        {
            admin.PrintToChat($"{_config.ChatPrefix} No groups found. Create a group first.");
            return;
        }

        var menu = new WasdMenu($"Group for {targetName}", _plugin);

        foreach (var group in groups)
        {
            menu.AddItem(group.Name, (p, o) =>
            {
                if (_adminService.SetAdminGroup(targetSteamId, group.Name, p.SteamID, p.PlayerName))
                {
                    p.PrintToChat($"{_config.ChatPrefix} {targetName} assigned to group: {group.Name}");
                }
                else
                {
                    p.PrintToChat($"{_config.ChatPrefix} Failed to set group.");
                }
            });
        }

        menu.AddItem("« Back", (p, o) => ShowSetGroupMenu(p));
        menu.Display(admin, 30);
    }

    #endregion
}
