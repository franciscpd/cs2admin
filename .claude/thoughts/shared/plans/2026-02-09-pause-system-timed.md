# Timed Pause System Implementation Plan

## Overview

Implement a comprehensive pause system for CS2Admin with per-team tactical pauses (3 per side, 1 minute each), admin pause (unlimited), and disconnect auto-pause (2 minutes). Replaces the current simple `mp_pause_match`/`mp_unpause_match` with a state-aware system that tracks pause type, team ownership, countdown timers, and on-screen display.

## Current State Analysis

The current pause system is minimal:
- `MatchService.cs:108-119` — `PauseMatch()` just runs `mp_pause_match` and sets `_isPaused = true`
- `MatchService.cs:121-132` — `UnpauseMatch()` just runs `mp_unpause_match` and sets `_isPaused = false`
- No concept of pause type, team ownership, timers, or limits
- `PlayerConnectionHandler.cs:99-109` — disconnect handler only clears session mutes
- Vote-pause (`VoteService` → `CS2AdminServiceCollection.OnVotePassed`) calls `MatchService.PauseMatch()` with no team context

### Key Discoveries:
- Timer pattern is well-established: `_plugin.AddTimer(float, Action)` — used in `CS2Admin.cs:69,121,133`, `MatchService.cs:227,302`
- Team identification: `(int)player.Team` where 2=T, 3=CT — used in `VoteService.cs:96,101`
- `PrintToCenter` is not used anywhere yet but is available via CounterStrikeSharp player extension methods
- The `Vote` model (`Models/Vote.cs:8`) stores `Initiator` which gives us the team for vote-pause
- `MatchService` already has `_plugin` reference via `SetPlugin()` for timer creation

## Desired End State

After implementation:
1. **Vote-pause** (`!votepause`): consumes 1 of 3 team pauses, lasts 60 seconds with center-screen countdown, auto-unpauses
2. **Admin pause** (`!pause`): unlimited, no timer, manual `!unpause` required (admin-only, behavior unchanged)
3. **Disconnect pause**: auto-triggers when a non-bot player disconnects during a live match, lasts 120 seconds, auto-unpauses on reconnect or timeout
4. **State**: pause counts persist per map (reset on map change), center display shows remaining time
5. **Scope**: only activates during live match (not warmup, not knife round)

### Verification:
- Plugin compiles without errors: `dotnet build`
- All three pause paths work: admin pause (unlimited, no timer), vote-pause (team limit + timer), disconnect pause (auto + timer)
- Countdown displays via `PrintToCenter` every second during timed pauses
- Team pause counts are tracked and enforced (max 3 per team per map)
- Disconnect reconnect cancels the pause early

## What We're NOT Doing

- No database persistence of pause counts across maps
- No half-time swap of pause counts (counts reset on map change only)
- No custom HUD/HTML panel — using `PrintToCenter` only
- No `!unpause` by players — only admins can manually unpause (timed pauses auto-unpause)
- No ConVar-based approach (not using `mp_team_timeout_time` etc.)
- No changes to the knife round pause in `EndKnifeRound()` (line 248)

## Implementation Approach

Create a new `PauseType` enum and expand `MatchService` with pause state tracking, a repeating 1-second timer for countdown + display, and team pause counters. Modify `PlayerConnectionHandler` to trigger disconnect pauses. Modify `CS2AdminServiceCollection.OnVotePassed` to pass team context. Add config options for pause limits and durations.

## Phase 1: Configuration and Model

### Overview
Add configuration options and a pause type enum so the rest of the system can reference them.

### Changes Required:

#### 1. New enum: PauseType
**File**: `Models/PauseType.cs` (new)
**Changes**: Define the three pause types

```csharp
namespace CS2Admin.Models;

public enum PauseType
{
    None,
    Admin,
    Vote,
    Disconnect
}
```

#### 2. Configuration additions
**File**: `Config/PluginConfig.cs`
**Changes**: Add pause-related config properties after the existing vote config block (after line 21)

```csharp
[JsonPropertyName("TeamPauseLimit")]
public int TeamPauseLimit { get; set; } = 3;

[JsonPropertyName("VotePauseDurationSeconds")]
public int VotePauseDurationSeconds { get; set; } = 60;

[JsonPropertyName("DisconnectPauseDurationSeconds")]
public int DisconnectPauseDurationSeconds { get; set; } = 120;

[JsonPropertyName("EnableDisconnectPause")]
public bool EnableDisconnectPause { get; set; } = true;
```

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles: `dotnet build`
- [x] New enum file exists at `Models/PauseType.cs`
- [x] Config properties are accessible

---

## Phase 2: MatchService Pause State Machine

### Overview
Replace the simple boolean pause tracking with a full state machine that knows the pause type, owning team, remaining time, and handles the countdown timer.

### Changes Required:

#### 1. New state fields in MatchService
**File**: `Services/MatchService.cs`
**Changes**: Add fields after `_isPaused` (line 13), replace/extend pause methods

Add new fields:
```csharp
private PauseType _activePauseType = PauseType.None;
private int _pauseTeam; // 2=T, 3=CT, 0=none
private int _pauseRemainingSeconds;
private CounterStrikeSharp.API.Modules.Timers.Timer? _pauseTimer;
private Dictionary<int, int> _teamPausesUsed = new() { { 2, 0 }, { 3, 0 } }; // team -> count
private int _teamPauseLimit;
private int _votePauseDuration;
private int _disconnectPauseDuration;
private ulong _disconnectedPlayerSteamId; // for reconnect detection
```

Update constructor to accept new config values:
```csharp
public MatchService(DatabaseService? database = null, bool enableLogging = false, int warmupMoney = 60000,
    int teamPauseLimit = 3, int votePauseDuration = 60, int disconnectPauseDuration = 120)
```

Add new public properties:
```csharp
public PauseType ActivePauseType => _activePauseType;
public int PauseRemainingSeconds => _pauseRemainingSeconds;
public int GetTeamPausesRemaining(int team) => _teamPauseLimit - (_teamPausesUsed.GetValueOrDefault(team, 0));
```

#### 2. New pause methods
**File**: `Services/MatchService.cs`

Add `PauseMatchTimed(PauseType type, int team, int durationSeconds)`:
```csharp
public (bool Success, string Message) PauseMatchTimed(PauseType type, int team, int durationSeconds)
{
    if (_isPaused) return (false, "Match is already paused.");

    // Check if live match (not warmup, not knife round)
    if (_isWarmup || _isKnifeRound || _waitingForSideChoice)
        return (false, "Can only pause during a live match.");

    // For vote pauses, check team limit
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

    // Start 1-second repeating timer for countdown
    _pauseTimer = _plugin?.AddTimer(1.0f, OnPauseTimerTick, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

    // Show initial message
    BroadcastPauseCenter();

    return (true, "");
}
```

Add timer tick handler:
```csharp
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

    Server.PrintToChatAll($"Match resumed.");
}

private void KillPauseTimer()
{
    _pauseTimer?.Kill();
    _pauseTimer = null;
}
```

Add disconnect-specific methods:
```csharp
public (bool Success, string Message) PauseForDisconnect(ulong steamId, int team)
{
    _disconnectedPlayerSteamId = steamId;
    return PauseMatchTimed(PauseType.Disconnect, team, _disconnectPauseDuration);
}

public void OnPlayerReconnect(ulong steamId)
{
    if (_isPaused && _activePauseType == PauseType.Disconnect && _disconnectedPlayerSteamId == steamId)
    {
        ForceUnpause();
        Server.PrintToChatAll($"Disconnected player reconnected. Match resumed.");
    }
}
```

#### 3. Modify existing PauseMatch for admin
**File**: `Services/MatchService.cs`
**Changes**: The existing `PauseMatch()` method stays for admin use but now sets `_activePauseType = PauseType.Admin`. No timer for admin pause.

```csharp
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
```

#### 4. Modify existing UnpauseMatch for admin
**File**: `Services/MatchService.cs`
**Changes**: Clear all pause state, kill any active timer

```csharp
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
```

#### 5. Reset pause state on map change
**File**: `Services/MatchService.cs`
**Changes**: Add method to reset pause counts, call it from `ResetKnifeRoundState` or add a new `ResetPauseState` method

```csharp
public void ResetPauseState()
{
    KillPauseTimer();
    _isPaused = false;
    _activePauseType = PauseType.None;
    _pauseTeam = 0;
    _pauseRemainingSeconds = 0;
    _disconnectedPlayerSteamId = 0;
    _teamPausesUsed = new Dictionary<int, int> { { 2, 0 }, { 3, 0 } };
}
```

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles: `dotnet build`
- [x] `MatchService` has `PauseMatchTimed`, `PauseForDisconnect`, `OnPlayerReconnect`, `ResetPauseState` methods
- [x] `ForceUnpause` cleans up timer and state

---

## Phase 3: Wire Up Vote-Pause with Team Context

### Overview
When a vote-pause passes, pass the initiator's team to `PauseMatchTimed` so it consumes a team pause and starts the timer.

### Changes Required:

#### 1. Update OnVotePassed
**File**: `CS2AdminServiceCollection.cs`
**Changes**: Modify the `VoteType.Pause` case (lines 80-82) to call `PauseMatchTimed` with team context from the vote initiator

```csharp
case VoteType.Pause:
    var team = (int)vote.Initiator.Team;
    var result = MatchService.PauseMatchTimed(PauseType.Vote, team, Config.VotePauseDurationSeconds);
    if (!result.Success)
    {
        BroadcastMessage(result.Message);
    }
    else
    {
        var teamName = team == 2 ? "T" : "CT";
        var remaining = MatchService.GetTeamPausesRemaining(team);
        BroadcastMessage($"{teamName} tactical pause ({remaining} remaining).");
    }
    break;
```

#### 2. Update MatchService constructor call
**File**: `CS2AdminServiceCollection.cs`
**Changes**: Pass new config values to `MatchService` constructor (line 42)

```csharp
MatchService = new MatchService(Database, config.EnableLogging, config.WarmupMoney,
    config.TeamPauseLimit, config.VotePauseDuration, config.DisconnectPauseDurationSeconds);
```

#### 3. Add using for PauseType
**File**: `CS2AdminServiceCollection.cs`
**Changes**: Add `using CS2Admin.Models;` at the top (it's already there for `Vote` and `VoteType`)

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles: `dotnet build`
- [x] Vote-pause passes team context to `PauseMatchTimed`

#### Manual Verification:
- [ ] `!votepause` triggers a 60-second timed pause with center-screen countdown
- [ ] After 3 vote-pauses by the same team, a 4th is rejected
- [ ] Timer counts down and auto-unpauses at 0

---

## Phase 4: Disconnect Auto-Pause

### Overview
When a player disconnects during a live match, automatically pause for 2 minutes. If the player reconnects, unpause immediately.

### Changes Required:

#### 1. Update PlayerConnectionHandler for disconnect pause
**File**: `Handlers/PlayerConnectionHandler.cs`
**Changes**: Add disconnect pause logic in `OnPlayerDisconnect` (after line 106), add reconnect check in `OnPlayerConnect`

In `OnPlayerDisconnect` after the mute cleanup:
```csharp
// Auto-pause on disconnect during live match
if (_config.EnableDisconnectPause &&
    !_matchService.IsPaused &&
    !_matchService.IsWarmup &&
    !_matchService.IsKnifeRound &&
    !_matchService.WaitingForSideChoice)
{
    var team = (int)player.Team;
    if (team == 2 || team == 3) // Only T or CT
    {
        var playerName = player.PlayerName;
        var result = _matchService.PauseForDisconnect(player.SteamID, team);
        if (result.Success)
        {
            Server.PrintToChatAll($"{_config.ChatPrefix} {playerName} disconnected. Match paused for 2 minutes.");
        }
    }
}
```

In `OnPlayerConnect`, before the ban check (add early in the method):
```csharp
// Check if this reconnect should unpause a disconnect pause
_matchService.OnPlayerReconnect(player.SteamID);
```

#### 2. Expose config in PlayerConnectionHandler
**File**: `Handlers/PlayerConnectionHandler.cs`
**Changes**: The handler already receives `PluginConfig` in its constructor, so `_config.EnableDisconnectPause` will work once we add it to the config (Phase 1).

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles: `dotnet build`

#### Manual Verification:
- [ ] When a player disconnects during a live match, a 2-minute pause starts
- [ ] If the player reconnects within 2 minutes, the match resumes immediately
- [ ] If the player doesn't reconnect, the match auto-resumes after 2 minutes
- [ ] Disconnect during warmup/knife does NOT trigger a pause

---

## Phase 5: Map Reset and Chat Feedback

### Overview
Reset pause counts on map change, add chat prefix to ForceUnpause messages, and add `!pause` info feedback about remaining pauses.

### Changes Required:

#### 1. Reset on map change
**File**: `CS2Admin.cs`
**Changes**: Call `ResetPauseState()` in `OnMapStart` (after line 130 where `ResetKnifeRoundState` is called)

```csharp
_services.MatchService.ResetPauseState();
```

#### 2. Add chat prefix to MatchService messages
**File**: `Services/MatchService.cs`
**Changes**: `MatchService` doesn't have access to `_config.ChatPrefix`. The simplest approach: accept a `string chatPrefix` in the constructor and store it. Use it in `ForceUnpause` and `BroadcastPauseCenter` for chat messages.

Add to constructor: `string chatPrefix = "[CS2Admin]"`
Add field: `private readonly string _chatPrefix;`

Update `ForceUnpause` message:
```csharp
Server.PrintToChatAll($"{_chatPrefix} Match resumed.");
```

Update reconnect message similarly.

#### 3. Update CS2AdminServiceCollection to pass chatPrefix
**File**: `CS2AdminServiceCollection.cs`
**Changes**: Pass `config.ChatPrefix` to `MatchService` constructor

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles: `dotnet build`

#### Manual Verification:
- [ ] Pause counts reset when map changes
- [ ] All chat messages have the configured prefix
- [ ] Center-screen display shows during timed pauses

---

## Testing Strategy

### Manual Testing Steps:
1. Start a match, use `!votepause` — confirm 60s countdown appears center-screen
2. Use `!votepause` 3 times for one team — confirm 4th is rejected
3. Use `!pause` as admin — confirm no timer, requires `!unpause`
4. Disconnect a player during live match — confirm 2-minute pause starts
5. Reconnect the player — confirm match resumes immediately
6. Let disconnect timer expire — confirm auto-unpause at 0
7. Try `!votepause` during warmup — confirm rejection
8. Try disconnect pause during knife round — confirm no pause triggers
9. Change map — confirm pause counts reset

### Edge Cases:
- Vote-pause while already paused (should reject)
- Admin `!unpause` during a timed pause (should work, kills timer)
- Two disconnects in quick succession (second should be rejected since already paused)
- Disconnect of spectator (should NOT trigger pause — only team 2/3)

## Performance Considerations

- The 1-second repeating timer is lightweight — only iterates connected players for `PrintToCenter`
- Timer is properly killed on unpause/map change to prevent leaks
- `_teamPausesUsed` dictionary is tiny (2 entries)

## References

- Research document: `.claude/thoughts/shared/research/2026-02-09-pause-system-structure.md`
- Current pause implementation: `Services/MatchService.cs:108-132`
- Timer pattern: `CS2Admin.cs:69`, `MatchService.cs:227`
- Team identification: `VoteService.cs:96`
- Vote passed handler: `CS2AdminServiceCollection.cs:69-95`
- Disconnect handler: `Handlers/PlayerConnectionHandler.cs:99-109`
