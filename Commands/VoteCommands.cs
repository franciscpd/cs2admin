using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Models;
using CS2Admin.Services;
using CS2Admin.Utils;
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;

namespace CS2Admin.Commands;

public class VoteCommands
{
    private readonly PluginConfig _config;
    private readonly VoteService _voteService;
    private BasePlugin? _plugin;

    public VoteCommands(PluginConfig config, VoteService voteService)
    {
        _config = config;
        _voteService = voteService;
    }

    public void RegisterCommands(BasePlugin plugin)
    {
        _plugin = plugin;
        plugin.AddCommand("css_votekick", "Start a vote to kick a player", OnVoteKickCommand);
        plugin.AddCommand("css_votepause", "Start a vote to pause the match", OnVotePauseCommand);
        plugin.AddCommand("css_voterestart", "Start a vote to restart the match", OnVoteRestartCommand);
        plugin.AddCommand("css_votechangemap", "Start a vote to change the map", OnVoteChangeMapCommand);
        plugin.AddCommand("css_votemap", "Start a vote to change the map", OnVoteChangeMapCommand);
    }

    private void OnVoteKickCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid || _plugin == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        // If player name provided, use it directly
        if (command.ArgCount >= 2)
        {
            var target = PlayerFinder.Find(command.GetArg(1));
            if (target == null)
            {
                command.ReplyToCommand($"{_config.ChatPrefix} Player not found.");
                return;
            }

            if (target.SteamID == caller.SteamID)
            {
                command.ReplyToCommand($"{_config.ChatPrefix} You cannot vote to kick yourself.");
                return;
            }

            StartKickVote(caller, target);
            return;
        }

        // Show player selection menu
        ShowPlayerSelectionMenu(caller);
    }

    private void ShowPlayerSelectionMenu(CCSPlayerController caller)
    {
        if (_plugin == null) return;

        var menu = new WasdMenu("Vote Kick - Select Player", _plugin);

        var players = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && !p.IsHLTV
                && p.Connected == PlayerConnectedState.PlayerConnected
                && p.SteamID != caller.SteamID)
            .ToList();

        if (players.Count == 0)
        {
            caller.PrintToChat($"{_config.ChatPrefix} No players available to kick.");
            return;
        }

        foreach (var player in players)
        {
            var targetPlayer = player; // Capture for closure
            menu.AddItem(player.PlayerName, (p, opt) => StartKickVote(p, targetPlayer));
        }

        menu.Display(caller, 30);
    }

    private void StartKickVote(CCSPlayerController caller, CCSPlayerController target)
    {
        var result = _voteService.StartVote(VoteType.Kick, caller, targetPlayer: target);
        if (!result.Success)
        {
            caller.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
    }

    private void OnVotePauseCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        var result = _voteService.StartVote(VoteType.Pause, caller);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
        }
    }

    private void OnVoteRestartCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        var result = _voteService.StartVote(VoteType.Restart, caller);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
        }
    }

    private void OnVoteChangeMapCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid || _plugin == null)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        // If map name provided, use it directly
        if (command.ArgCount >= 2)
        {
            var mapName = command.GetArg(1);
            StartMapVote(caller, mapName);
            return;
        }

        // Show map selection menu
        ShowMapSelectionMenu(caller);
    }

    private void ShowMapSelectionMenu(CCSPlayerController caller)
    {
        if (_plugin == null) return;

        var menu = new WasdMenu("Vote Map - Select Map", _plugin);

        if (_config.VoteMaps.Count == 0)
        {
            caller.PrintToChat($"{_config.ChatPrefix} No maps configured for voting.");
            return;
        }

        foreach (var map in _config.VoteMaps)
        {
            var mapName = map; // Capture for closure
            menu.AddItem(map, (p, opt) => StartMapVote(p, mapName));
        }

        menu.Display(caller, 30);
    }

    private void StartMapVote(CCSPlayerController caller, string mapName)
    {
        var result = _voteService.StartVote(VoteType.ChangeMap, caller, targetMap: mapName);
        if (!result.Success)
        {
            caller.PrintToChat($"{_config.ChatPrefix} {result.Message}");
        }
    }
}
