using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Models;
using CS2Admin.Services;
using CS2Admin.Utils;

namespace CS2Admin.Commands;

public class ChatCommandHandler
{
    private readonly PluginConfig _config;
    private readonly PlayerService _playerService;
    private readonly BanService _banService;
    private readonly MuteService _muteService;
    private readonly MatchService _matchService;
    private readonly VoteService _voteService;
    private readonly AdminManagementCommands _adminManagementCommands;

    public ChatCommandHandler(
        PluginConfig config,
        PlayerService playerService,
        BanService banService,
        MuteService muteService,
        MatchService matchService,
        VoteService voteService,
        AdminManagementCommands adminManagementCommands)
    {
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

        var message = info.GetArg(1);
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith('.'))
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

    #region Vote Commands

    private bool HandleVoteKick(CCSPlayerController player, string[] args)
    {
        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .votekick <player>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .votechangemap <map>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .kick <player> [reason]");
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

        if (args.Length < 2)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .ban <player> <duration> [reason]");
            return true;
        }

        var target = PlayerFinder.Find(args[0]);
        if (target == null)
        {
            player.PrintToChat($"{_config.ChatPrefix} Player not found.");
            return true;
        }

        var duration = TimeParser.Parse(args[1]);
        var reason = args.Length > 2 ? string.Join(" ", args[2..]) : _config.DefaultBanReason;

        _banService.BanPlayer(target.SteamID, target.PlayerName, player.SteamID, player.PlayerName, duration, reason);
        _playerService.KickPlayer(target, player, $"Banned: {reason}");

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was banned by {player.PlayerName} for {durationText}. Reason: {reason}");
        return true;
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .unban <steamid>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .mute <player> [duration] [reason]");
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

    private bool HandleUnmute(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/chat"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .unmute <player>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .slay <player>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .slap <player> [damage]");
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

    private bool HandleRespawn(CCSPlayerController player, string[] args)
    {
        if (!AdminManager.PlayerHasPermissions(player, "@css/slay"))
        {
            player.PrintToChat($"{_config.ChatPrefix} You don't have permission to use this command.");
            return true;
        }

        if (args.Length < 1)
        {
            player.PrintToChat($"{_config.ChatPrefix} Usage: .respawn <player>");
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
            player.PrintToChat($"{_config.ChatPrefix} Usage: .changemap <map>");
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

        Server.PrintToChatAll($"{_config.ChatPrefix} {_config.MatchStartMessage}");
        Server.PrintToChatAll($"{_config.ChatPrefix} Match started by {player.PlayerName}.");
        _matchService.StartMatch(player);
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

        Server.PrintToChatAll($"{_config.ChatPrefix} Warmup ended by {player.PlayerName}.");
        _matchService.EndWarmup(player);
        return true;
    }

    #endregion
}
