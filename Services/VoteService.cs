using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2Admin.Models;
using CS2Admin.Utils;
using PanoramaVote;

namespace CS2Admin.Services;

public class VoteService
{
    private readonly int _thresholdPercent;
    private readonly int _voteDurationSeconds;
    private readonly int _cooldownSeconds;
    private readonly int _minimumVoters;
    private readonly Action<string> _broadcastMessage;
    private readonly Action<Vote> _onVotePassed;
    private readonly CPanoramaVote _panoramaVote;

    private Vote? _currentVote;
    private readonly Dictionary<VoteType, DateTime> _cooldowns = new();

    public Vote? CurrentVote => _currentVote;
    public bool HasActiveVote => _currentVote?.IsActive == true;

    public VoteService(
        BasePlugin plugin,
        int thresholdPercent,
        int voteDurationSeconds,
        int cooldownSeconds,
        int minimumVoters,
        Action<string> broadcastMessage,
        Action<Vote> onVotePassed)
    {
        _thresholdPercent = thresholdPercent;
        _voteDurationSeconds = voteDurationSeconds;
        _cooldownSeconds = cooldownSeconds;
        _minimumVoters = minimumVoters;
        _broadcastMessage = broadcastMessage;
        _onVotePassed = onVotePassed;
        _panoramaVote = new CPanoramaVote(plugin);
    }

    public (bool Success, string Message) StartVote(VoteType type, CCSPlayerController initiator, CCSPlayerController? targetPlayer = null, string? targetMap = null)
    {
        if (HasActiveVote || _panoramaVote.IsVoteInProgress())
        {
            return (false, "A vote is already in progress.");
        }

        var playerCount = PlayerFinder.GetPlayerCount();
        if (playerCount < _minimumVoters)
        {
            return (false, $"Not enough players to start a vote. Need at least {_minimumVoters} players.");
        }

        if (_cooldowns.TryGetValue(type, out var lastVote))
        {
            var elapsed = DateTime.UtcNow - lastVote;
            if (elapsed.TotalSeconds < _cooldownSeconds)
            {
                var remaining = _cooldownSeconds - (int)elapsed.TotalSeconds;
                return (false, $"Vote on cooldown. Please wait {remaining} seconds.");
            }
        }

        _currentVote = new Vote
        {
            Type = type,
            Initiator = initiator,
            TargetPlayer = targetPlayer,
            TargetMap = targetMap,
            StartTime = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(_voteDurationSeconds),
            IsActive = true
        };

        var voteDescription = GetVoteDescription(_currentVote);
        var voteTitle = GetVoteTitleKey(type);
        var callerSlot = initiator.Slot;

        // Start native CS2 Panorama vote
        var success = _panoramaVote.SendYesNoVoteToAll(
            _voteDurationSeconds,
            callerSlot,
            voteTitle,
            voteDescription,
            OnVoteResult,
            OnVoteAction
        );

        if (!success)
        {
            _currentVote = null;
            return (false, "Failed to start vote.");
        }

        return (true, $"Vote started: {voteDescription}");
    }

    private void OnVoteAction(YesNoVoteAction action, int param1, int param2)
    {
        if (_currentVote == null) return;

        switch (action)
        {
            case YesNoVoteAction.VoteAction_Start:
                // Vote started
                break;

            case YesNoVoteAction.VoteAction_Vote:
                // param1 = client slot, param2 = vote choice
                var player = Utilities.GetPlayerFromSlot(param1);
                if (player != null)
                {
                    var steamId = player.SteamID;
                    if (param2 == (int)PanoramaVote.CastVote.VOTE_OPTION1) // Yes
                    {
                        _currentVote.YesVotes.Add(steamId);
                    }
                    else if (param2 == (int)PanoramaVote.CastVote.VOTE_OPTION2) // No
                    {
                        _currentVote.NoVotes.Add(steamId);
                    }
                }
                break;

            case YesNoVoteAction.VoteAction_End:
                // Vote ended - handled in OnVoteResult
                break;
        }
    }

    private bool OnVoteResult(YesNoVoteInfo info)
    {
        if (_currentVote == null) return false;

        var playerCount = info.num_clients > 0 ? info.num_clients : PlayerFinder.GetPlayerCount();
        var yesPercent = playerCount > 0 ? (double)info.yes_votes / playerCount * 100 : 0;
        var passed = yesPercent >= _thresholdPercent;

        _currentVote.IsActive = false;
        _cooldowns[_currentVote.Type] = DateTime.UtcNow;

        var voteDescription = GetVoteDescription(_currentVote);

        if (passed)
        {
            _broadcastMessage($"Vote passed: {voteDescription} ({info.yes_votes}/{playerCount}, {yesPercent:F0}%)");
            _onVotePassed(_currentVote);
        }
        else
        {
            _broadcastMessage($"Vote failed: {voteDescription} ({info.yes_votes}/{playerCount}, {yesPercent:F0}%)");
        }

        _currentVote = null;
        return passed;
    }

    public (bool Success, string Message) CastVote(CCSPlayerController player, bool voteYes)
    {
        // With PanoramaVote, players vote using F1/F2 keys directly
        // This method is kept for backwards compatibility with !yes/!no commands
        if (_currentVote == null || !_currentVote.IsActive)
        {
            return (false, "No active vote. Use F1 (Yes) or F2 (No) to vote.");
        }

        return (false, "Please use F1 (Yes) or F2 (No) to vote.");
    }

    public void CancelVote()
    {
        if (_panoramaVote.IsVoteInProgress())
        {
            _panoramaVote.CancelVote();
        }

        if (_currentVote != null)
        {
            _currentVote.IsActive = false;
            _broadcastMessage("Vote cancelled.");
            _currentVote = null;
        }
    }

    private static string GetVoteDescription(Vote vote)
    {
        return vote.Type switch
        {
            VoteType.Kick => $"Kick {vote.TargetPlayer?.PlayerName ?? "player"}",
            VoteType.Pause => "Pause match",
            VoteType.Restart => "Restart match",
            VoteType.ChangeMap => $"Change map to {vote.TargetMap ?? "unknown"}",
            _ => "Unknown vote"
        };
    }

    private static string GetVoteTitleKey(VoteType type)
    {
        // These are the default CS2 vote title keys
        // You can customize these in platform_english.txt
        return type switch
        {
            VoteType.Kick => "#SFUI_vote_kick_player_other",
            VoteType.Pause => "#SFUI_vote_pause_match",
            VoteType.Restart => "#SFUI_vote_restart_game",
            VoteType.ChangeMap => "#SFUI_vote_changelevel",
            _ => "#SFUI_vote_panorama_vote_default"
        };
    }
}
