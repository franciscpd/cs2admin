using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Services;
using CS2Admin.Utils;

namespace CS2Admin.Commands;

public class AdminCommands
{
    private readonly PluginConfig _config;
    private readonly PlayerService _playerService;
    private readonly BanService _banService;
    private readonly MuteService _muteService;
    private readonly MatchService _matchService;

    public AdminCommands(
        PluginConfig config,
        PlayerService playerService,
        BanService banService,
        MuteService muteService,
        MatchService matchService)
    {
        _config = config;
        _playerService = playerService;
        _banService = banService;
        _muteService = muteService;
        _matchService = matchService;
    }

    public void RegisterCommands(BasePlugin plugin)
    {
        plugin.AddCommand("css_kick", "Kick a player", OnKickCommand);
        plugin.AddCommand("css_ban", "Ban a player", OnBanCommand);
        plugin.AddCommand("css_unban", "Unban a player", OnUnbanCommand);
        plugin.AddCommand("css_mute", "Mute a player", OnMuteCommand);
        plugin.AddCommand("css_unmute", "Unmute a player", OnUnmuteCommand);
        plugin.AddCommand("css_slay", "Slay a player", OnSlayCommand);
        plugin.AddCommand("css_slap", "Slap a player", OnSlapCommand);
        plugin.AddCommand("css_respawn", "Respawn a player", OnRespawnCommand);
        plugin.AddCommand("css_changemap", "Change the map", OnChangeMapCommand);
        plugin.AddCommand("css_map", "Change the map", OnChangeMapCommand);
        plugin.AddCommand("css_pause", "Pause the match", OnPauseCommand);
        plugin.AddCommand("css_unpause", "Unpause the match", OnUnpauseCommand);
        plugin.AddCommand("css_restart", "Restart the match", OnRestartCommand);
        plugin.AddCommand("css_start", "Start the match (end warmup)", OnStartCommand);
        plugin.AddCommand("css_warmup", "Start warmup mode", OnWarmupCommand);
        plugin.AddCommand("css_endwarmup", "End warmup mode", OnEndWarmupCommand);
    }

    [RequiresPermissions("@css/kick")]
    private void OnKickCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_kick <player> [reason]");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        var reason = command.ArgCount > 2 ? command.GetArg(2) : _config.DefaultKickReason;
        _playerService.KickPlayer(target, caller, reason);

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was kicked by {adminName}. Reason: {reason}");
    }

    [RequiresPermissions("@css/ban")]
    private void OnBanCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 3)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_ban <player> <duration> [reason]");
            command.ReplyToCommand($"{_config.ChatPrefix} Duration: 0=permanent, 30m, 1h, 1d, 1w, etc.");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        var duration = TimeParser.Parse(command.GetArg(2));
        var reason = command.ArgCount > 3 ? command.GetArg(3) : _config.DefaultBanReason;

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        _banService.BanPlayer(target.SteamID, target.PlayerName, adminSteamId, adminName, duration, reason);
        _playerService.KickPlayer(target, caller, $"Banned: {reason}");

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was banned by {adminName} for {durationText}. Reason: {reason}");
    }

    [RequiresPermissions("@css/ban")]
    private void OnUnbanCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_unban <steamid>");
            return;
        }

        if (!ulong.TryParse(command.GetArg(1), out var steamId))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Invalid SteamID.");
            return;
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_banService.UnbanPlayer(steamId, adminSteamId, adminName))
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player unbanned successfully.");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} No active ban found for this SteamID.");
        }
    }

    [RequiresPermissions("@css/chat")]
    private void OnMuteCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_mute <player> [duration] [reason]");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        TimeSpan? duration = null;
        var reason = _config.DefaultMuteReason;

        if (command.ArgCount > 2)
        {
            duration = TimeParser.Parse(command.GetArg(2));
        }

        if (command.ArgCount > 3)
        {
            reason = command.GetArg(3);
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        _muteService.MutePlayer(target.SteamID, target.PlayerName, adminSteamId, adminName, duration, reason);

        var durationText = TimeParser.Format(duration);
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was muted by {adminName} for {durationText}.");
    }

    [RequiresPermissions("@css/chat")]
    private void OnUnmuteCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_unmute <player>");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        var adminSteamId = caller?.SteamID ?? 0;
        var adminName = caller?.PlayerName ?? "Console";

        if (_muteService.UnmutePlayer(target.SteamID, adminSteamId, adminName))
        {
            Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was unmuted by {adminName}.");
        }
        else
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player is not muted.");
        }
    }

    [RequiresPermissions("@css/slay")]
    private void OnSlayCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_slay <player>");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        _playerService.SlayPlayer(target, caller);

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} {target.PlayerName} was slayed by {adminName}.");
    }

    [RequiresPermissions("@css/slay")]
    private void OnSlapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_slap <player> [damage]");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        var damage = 0;
        if (command.ArgCount > 2 && int.TryParse(command.GetArg(2), out var d))
        {
            damage = d;
        }

        _playerService.SlapPlayer(target, caller, damage);

        var adminName = caller?.PlayerName ?? "Console";
        command.ReplyToCommand($"{_config.ChatPrefix} {target.PlayerName} was slapped{(damage > 0 ? $" for {damage} damage" : "")}.");
    }

    [RequiresPermissions("@css/slay")]
    private void OnRespawnCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_respawn <player>");
            return;
        }

        var target = PlayerFinder.Find(command.GetArg(1));
        if (target == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
            return;
        }

        _playerService.RespawnPlayer(target, caller);

        var adminName = caller?.PlayerName ?? "Console";
        command.ReplyToCommand($"{_config.ChatPrefix} {target.PlayerName} was respawned.");
    }

    [RequiresPermissions("@css/changemap")]
    private void OnChangeMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_changemap <map>");
            return;
        }

        var mapName = command.GetArg(1);
        var adminName = caller?.PlayerName ?? "Console";

        Server.PrintToChatAll($"{_config.ChatPrefix} Changing map to {mapName}...");
        _matchService.ChangeMap(mapName, caller);
    }

    [RequiresPermissions("@css/generic")]
    private void OnPauseCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (_matchService.IsPaused)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Match is already paused.");
            return;
        }

        _matchService.PauseMatch(caller);

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} Match paused by {adminName}.");
    }

    [RequiresPermissions("@css/generic")]
    private void OnUnpauseCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!_matchService.IsPaused)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Match is not paused.");
            return;
        }

        _matchService.UnpauseMatch(caller);

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} Match unpaused by {adminName}.");
    }

    [RequiresPermissions("@css/generic")]
    private void OnRestartCommand(CCSPlayerController? caller, CommandInfo command)
    {
        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} Match restarting by {adminName}...");
        _matchService.RestartMatch(caller);
    }

    [RequiresPermissions("@css/generic")]
    private void OnStartCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!_matchService.IsWarmup)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Server is not in warmup mode.");
            return;
        }

        var playerCount = PlayerFinder.GetPlayerCount();
        if (playerCount < _config.MinPlayersToStart)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Not enough players. Need at least {_config.MinPlayersToStart} players to start.");
            return;
        }

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} {_config.MatchStartMessage}");
        Server.PrintToChatAll($"{_config.ChatPrefix} Match started by {adminName}.");
        _matchService.StartMatch(caller);
    }

    [RequiresPermissions("@css/generic")]
    private void OnWarmupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (_matchService.IsWarmup)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Server is already in warmup mode.");
            return;
        }

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} Warmup started by {adminName}.");
        Server.PrintToChatAll($"{_config.ChatPrefix} {_config.WarmupMessage}");
        _matchService.StartWarmup(caller);
    }

    [RequiresPermissions("@css/generic")]
    private void OnEndWarmupCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!_matchService.IsWarmup)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Server is not in warmup mode.");
            return;
        }

        var adminName = caller?.PlayerName ?? "Console";
        Server.PrintToChatAll($"{_config.ChatPrefix} Warmup ended by {adminName}.");
        _matchService.EndWarmup(caller);
    }
}
