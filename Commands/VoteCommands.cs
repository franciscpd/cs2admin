using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using CS2Admin.Models;
using CS2Admin.Services;
using CS2Admin.Utils;

namespace CS2Admin.Commands;

public class VoteCommands
{
    private readonly PluginConfig _config;
    private readonly VoteService _voteService;

    public VoteCommands(PluginConfig config, VoteService voteService)
    {
        _config = config;
        _voteService = voteService;
    }

    public void RegisterCommands(BasePlugin plugin)
    {
        plugin.AddCommand("css_votekick", "Start a vote to kick a player", OnVoteKickCommand);
        plugin.AddCommand("css_votepause", "Start a vote to pause the match", OnVotePauseCommand);
        plugin.AddCommand("css_voterestart", "Start a vote to restart the match", OnVoteRestartCommand);
        plugin.AddCommand("css_votechangemap", "Start a vote to change the map", OnVoteChangeMapCommand);
        plugin.AddCommand("css_votemap", "Start a vote to change the map", OnVoteChangeMapCommand);
        plugin.AddCommand("css_yes", "Vote yes", OnVoteYesCommand);
        plugin.AddCommand("css_no", "Vote no", OnVoteNoCommand);
    }

    private void OnVoteKickCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_votekick <player>");
            return;
        }

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

        var result = _voteService.StartVote(VoteType.Kick, caller, targetPlayer: target);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
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
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        if (command.ArgCount < 2)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} Usage: css_votechangemap <map>");
            return;
        }

        var mapName = command.GetArg(1);
        var result = _voteService.StartVote(VoteType.ChangeMap, caller, targetMap: mapName);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
        }
    }

    private void OnVoteYesCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        var result = _voteService.CastVote(caller, true);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
        }
    }

    private void OnVoteNoCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (caller == null || !caller.IsValid)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} This command can only be used by players.");
            return;
        }

        var result = _voteService.CastVote(caller, false);
        if (!result.Success)
        {
            command.ReplyToCommand($"{_config.ChatPrefix} {result.Message}");
        }
    }
}
