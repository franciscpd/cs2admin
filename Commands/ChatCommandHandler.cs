using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CS2Admin.Config;
using CS2Admin.Models;
using CS2Admin.Services;
using CS2Admin.Utils;

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

    public ChatCommandHandler(
        BasePlugin plugin,
        PluginConfig config,
        PlayerService playerService,
        BanService banService,
        MuteService muteService,
        MatchService matchService,
        VoteService voteService,
        AdminManagementCommands adminManagementCommands)
    {
        _plugin = plugin;
        _config = config;
        _playerService = playerService;
        _banService = banService;
        _muteService = muteService;
        _matchService = matchService;
        _voteService = voteService;
        _adminManagementCommands = adminManagementCommands;
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
            "yes" => HandleVoteYes(player),
            "no" => HandleVoteNo(player),

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

            // Knife round side choice (available to knife round winners)
            "stay" => HandleStay(player),
            "switch" => HandleSwitch(player),

            // Admin management commands
            "add_admin" => _adminManagementCommands.HandleAddAdmin(player, args),
            "remove_admin" => _adminManagementCommands.HandleRemoveAdmin(player, args),
            "list_admins" => _adminManagementCommands.HandleListAdmins(player),
            "add_group" => _adminManagementCommands.HandleAddGroup(player, args),
            "remove_group" => _adminManagementCommands.HandleRemoveGroup(player, args),
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

        var menu = new CenterHtmlMenu(title, _plugin);

        foreach (var target in players)
        {
            menu.AddMenuOption(target.PlayerName, (p, opt) => onSelect(p, target));
        }

        MenuManager.OpenCenterHtmlMenu(_plugin, admin, menu);
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: !votechangemap <map>");
            return true;
        }

        var result = _voteService.StartVote(VoteType.ChangeMap, player, targetMap: args[0]);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }

        return true;
    }

    private bool HandleVoteYes(CCSPlayerController player)
    {
        var result = _voteService.CastVote(player, true);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
        return true;
    }

    private bool HandleVoteNo(CCSPlayerController player)
    {
        var result = _voteService.CastVote(player, false);
        if (!result.Success)
        {
            player.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
        return true;
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
        var menu = new CenterHtmlMenu($"Ban {target.PlayerName} - Duration", _plugin);

        menu.AddMenuOption("30 minutes", (p, o) => ExecuteBan(p, target, TimeSpan.FromMinutes(30)));
        menu.AddMenuOption("1 hour", (p, o) => ExecuteBan(p, target, TimeSpan.FromHours(1)));
        menu.AddMenuOption("1 day", (p, o) => ExecuteBan(p, target, TimeSpan.FromDays(1)));
        menu.AddMenuOption("1 week", (p, o) => ExecuteBan(p, target, TimeSpan.FromDays(7)));
        menu.AddMenuOption("Permanent", (p, o) => ExecuteBan(p, target, null));

        MenuManager.OpenCenterHtmlMenu(_plugin, admin, menu);
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
        var menu = new CenterHtmlMenu($"Mute {target.PlayerName} - Duration", _plugin);

        menu.AddMenuOption("5 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(5)));
        menu.AddMenuOption("15 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(15)));
        menu.AddMenuOption("30 minutes", (p, o) => ExecuteMute(p, target, TimeSpan.FromMinutes(30)));
        menu.AddMenuOption("1 hour", (p, o) => ExecuteMute(p, target, TimeSpan.FromHours(1)));
        menu.AddMenuOption("Permanent", (p, o) => ExecuteMute(p, target, null));

        MenuManager.OpenCenterHtmlMenu(_plugin, admin, menu);
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
        var menu = new CenterHtmlMenu($"Slap {target.PlayerName} - Damage", _plugin);

        menu.AddMenuOption("No damage", (p, o) => ExecuteSlap(p, target, 0));
        menu.AddMenuOption("5 damage", (p, o) => ExecuteSlap(p, target, 5));
        menu.AddMenuOption("10 damage", (p, o) => ExecuteSlap(p, target, 10));
        menu.AddMenuOption("25 damage", (p, o) => ExecuteSlap(p, target, 25));
        menu.AddMenuOption("50 damage", (p, o) => ExecuteSlap(p, target, 50));

        MenuManager.OpenCenterHtmlMenu(_plugin, admin, menu);
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
            _matchService.EndWarmup(player);
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

    private bool HandleStay(CCSPlayerController player)
    {
        if (!_matchService.WaitingForSideChoice)
        {
            player.PrintToChat($"{_config.ChatPrefix} No side choice pending.");
            return true;
        }

        // Check if player is on the winning team
        var playerTeam = player.TeamNum;
        if (playerTeam != _matchService.KnifeRoundWinnerTeam)
        {
            player.PrintToChat($"{_config.ChatPrefix} Only the knife round winner can choose side.");
            return true;
        }

        _matchService.ChooseSide(true, player);
        Server.PrintToChatAll($"{_config.ChatPrefix} {player.PlayerName} chose to STAY on current side. Match starting!");
        return true;
    }

    private bool HandleSwitch(CCSPlayerController player)
    {
        if (!_matchService.WaitingForSideChoice)
        {
            player.PrintToChat($"{_config.ChatPrefix} No side choice pending.");
            return true;
        }

        // Check if player is on the winning team
        var playerTeam = player.TeamNum;
        if (playerTeam != _matchService.KnifeRoundWinnerTeam)
        {
            player.PrintToChat($"{_config.ChatPrefix} Only the knife round winner can choose side.");
            return true;
        }

        _matchService.ChooseSide(false, player);
        Server.PrintToChatAll($"{_config.ChatPrefix} {player.PlayerName} chose to SWITCH sides. Match starting!");
        return true;
    }

    #endregion

    #region Help Command

    private bool HandleHelp(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("CS2Admin Help", _plugin);

        menu.AddMenuOption("Vote Commands", (p, opt) => ShowVoteCommands(p));

        var hasKick = AdminManager.PlayerHasPermissions(player, "@css/kick");
        var hasBan = AdminManager.PlayerHasPermissions(player, "@css/ban");
        var hasChat = AdminManager.PlayerHasPermissions(player, "@css/chat");
        var hasSlay = AdminManager.PlayerHasPermissions(player, "@css/slay");
        var hasMap = AdminManager.PlayerHasPermissions(player, "@css/changemap");
        var hasGeneric = AdminManager.PlayerHasPermissions(player, "@css/generic");

        if (hasKick || hasBan || hasChat || hasSlay || hasMap || hasGeneric)
        {
            menu.AddMenuOption("Admin Commands", (p, opt) => ShowAdminCommands(p));
        }

        MenuManager.OpenCenterHtmlMenu(_plugin, player, menu);
        return true;
    }

    private void ShowVoteCommands(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("Vote Commands", _plugin);

        menu.AddMenuOption("!votekick <player> - Vote to kick", (p, o) => {}, true);
        menu.AddMenuOption("!votepause - Vote to pause", (p, o) => {}, true);
        menu.AddMenuOption("!voterestart - Vote to restart", (p, o) => {}, true);
        menu.AddMenuOption("!votemap <map> - Vote to change map", (p, o) => {}, true);
        menu.AddMenuOption("!yes / !no - Cast your vote", (p, o) => {}, true);
        menu.AddMenuOption("!stay / !switch - Knife round choice", (p, o) => {}, true);
        menu.AddMenuOption("« Back", (p, o) => HandleHelp(p));

        MenuManager.OpenCenterHtmlMenu(_plugin, player, menu);
    }

    private void ShowAdminCommands(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("Admin Commands", _plugin);

        if (AdminManager.PlayerHasPermissions(player, "@css/kick"))
            menu.AddMenuOption("!kick <player> [reason]", (p, o) => {}, true);

        if (AdminManager.PlayerHasPermissions(player, "@css/ban"))
            menu.AddMenuOption("!ban/!unban <player> <dur> [reason]", (p, o) => {}, true);

        if (AdminManager.PlayerHasPermissions(player, "@css/chat"))
            menu.AddMenuOption("!mute/!unmute <player>", (p, o) => {}, true);

        if (AdminManager.PlayerHasPermissions(player, "@css/slay"))
            menu.AddMenuOption("!slay/!slap/!respawn <player>", (p, o) => {}, true);

        if (AdminManager.PlayerHasPermissions(player, "@css/changemap"))
            menu.AddMenuOption("!map <mapname> - Change map", (p, o) => {}, true);

        if (AdminManager.PlayerHasPermissions(player, "@css/generic"))
        {
            menu.AddMenuOption("!pause/!unpause - Pause match", (p, o) => {}, true);
            menu.AddMenuOption("!warmup/!endwarmup - Warmup control", (p, o) => {}, true);
            menu.AddMenuOption("!start - Start match", (p, o) => {}, true);
            menu.AddMenuOption("!knife - Toggle knife only", (p, o) => {}, true);
        }

        menu.AddMenuOption("« Back", (p, o) => HandleHelp(p));
        MenuManager.OpenCenterHtmlMenu(_plugin, player, menu);
    }

    #endregion
}
