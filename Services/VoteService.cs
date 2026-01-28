using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2Admin.Models;
using CS2Admin.Utils;

namespace CS2Admin.Services;

public class VoteService
{
    private readonly int _thresholdPercent;
    private readonly int _voteDurationSeconds;
    private readonly int _cooldownSeconds;
    private readonly int _minimumVoters;
    private readonly Action<string> _broadcastMessage;
    private readonly Action<Vote> _onVotePassed;

    private Vote? _currentVote;
    private readonly Dictionary<VoteType, DateTime> _cooldowns = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;

    public Vote? CurrentVote => _currentVote;
    public bool HasActiveVote => _currentVote?.IsActive == true;

    public VoteService(
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
    }

    public (bool Success, string Message) StartVote(VoteType type, CCSPlayerController initiator, CCSPlayerController? targetPlayer = null, string? targetMap = null)
    {
        if (HasActiveVote)
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

        // Initiator automatically votes yes
        _currentVote.YesVotes.Add(initiator.SteamID);

        var voteDescription = GetVoteDescription(_currentVote);
        _broadcastMessage($"Vote started: {voteDescription}");
        _broadcastMessage($"Type !yes or !no to vote. ({_voteDurationSeconds} seconds remaining)");

        // Start timer for vote expiration
        _voteTimer = new CounterStrikeSharp.API.Modules.Timers.Timer(_voteDurationSeconds, () =>
        {
            if (_currentVote?.IsActive == true)
            {
                EndVote(false);
            }
        });

        return (true, $"Vote started: {voteDescription}");
    }

    public (bool Success, string Message) CastVote(CCSPlayerController player, bool voteYes)
    {
        if (_currentVote == null || !_currentVote.IsActive)
        {
            return (false, "No active vote.");
        }

        var steamId = player.SteamID;

        if (_currentVote.YesVotes.Contains(steamId) || _currentVote.NoVotes.Contains(steamId))
        {
            return (false, "You have already voted.");
        }

        if (voteYes)
        {
            _currentVote.YesVotes.Add(steamId);
        }
        else
        {
            _currentVote.NoVotes.Add(steamId);
        }

        var playerCount = PlayerFinder.GetPlayerCount();
        var yesPercent = (double)_currentVote.YesVotes.Count / playerCount * 100;

        // Check if vote passed
        if (yesPercent >= _thresholdPercent)
        {
            EndVote(true);
            return (true, "Vote passed!");
        }

        // Check if vote cannot pass anymore
        var maxPossibleYes = _currentVote.YesVotes.Count + (playerCount - _currentVote.TotalVotes);
        var maxPossiblePercent = (double)maxPossibleYes / playerCount * 100;

        if (maxPossiblePercent < _thresholdPercent)
        {
            EndVote(false);
            return (true, "Vote failed.");
        }

        return (true, $"Vote recorded. Current: {_currentVote.YesVotes.Count} yes, {_currentVote.NoVotes.Count} no ({yesPercent:F0}%)");
    }

    private void EndVote(bool passed)
    {
        if (_currentVote == null) return;

        _currentVote.IsActive = false;
        _cooldowns[_currentVote.Type] = DateTime.UtcNow;

        _voteTimer?.Kill();
        _voteTimer = null;

        var voteDescription = GetVoteDescription(_currentVote);
        var playerCount = PlayerFinder.GetPlayerCount();
        var yesPercent = playerCount > 0 ? (double)_currentVote.YesVotes.Count / playerCount * 100 : 0;

        if (passed)
        {
            _broadcastMessage($"Vote passed: {voteDescription} ({_currentVote.YesVotes.Count}/{playerCount}, {yesPercent:F0}%)");
            _onVotePassed(_currentVote);
        }
        else
        {
            _broadcastMessage($"Vote failed: {voteDescription} ({_currentVote.YesVotes.Count}/{playerCount}, {yesPercent:F0}%)");
        }

        _currentVote = null;
    }

    public void CancelVote()
    {
        if (_currentVote == null) return;

        _currentVote.IsActive = false;
        _voteTimer?.Kill();
        _voteTimer = null;

        _broadcastMessage("Vote cancelled.");
        _currentVote = null;
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
}
