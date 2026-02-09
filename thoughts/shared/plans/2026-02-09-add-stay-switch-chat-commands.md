# Add !stay/!switch Chat Commands for Knife Round Side Choice

## Overview

Add `!stay` and `!switch` chat commands as alternatives to the F1/F2 Panorama vote for side selection after a knife round. Both mechanisms will work in parallel - the Panorama vote runs as before, but any winning team player can also type `!stay` or `!switch` in chat to immediately resolve the choice. This provides a reliable fallback when the Panorama vote UI doesn't render.

## Current State Analysis

- The Panorama F1/F2 vote is implemented and triggered 3s after knife round ends (`CS2Admin.cs:183`)
- `!stay` and `!switch` are documented in the README (`README.md:127-128, 224, 291`) but **not implemented** in `ChatCommandHandler.cs:84-123`
- The README config example says `"Type !stay or !switch"` which confuses players since those commands do nothing
- The code default says `"Vote: F1 = Stay, F2 = Switch"` (`PluginConfig.cs:88`) which is accurate but doesn't mention the chat alternative

### Key Discoveries:
- `ChatCommandHandler.cs:84-123` - Command switch has no "stay"/"switch" entries
- `VoteService.cs:249-297` - `StartSideChoiceVote()` creates Panorama vote for winning team only
- `MatchService.cs:388-393` - `EndKnifeRound()` sets `_waitingForSideChoice = true`
- `MatchService.cs:395-428` - `ChooseSide(bool stayOnSide)` executes the side change and starts match
- `VoteService.cs:201-214` - `CancelVote()` can cancel a running Panorama vote

## Desired End State

After this plan is complete:
- Players on the winning team can type `!stay` or `!switch` in chat to choose side
- The F1/F2 Panorama vote still works in parallel
- If a player uses `!stay`/`!switch`, the Panorama vote is cancelled and the choice is applied immediately
- If the Panorama vote completes first, `!stay`/`!switch` have no effect (since `WaitingForSideChoice` becomes `false`)
- The chat message after knife round mentions both options
- The README accurately reflects both mechanisms

### Verification:
1. Start a match with `!start` (knife round enabled)
2. Win the knife round
3. See message: `"{team} won the knife round! Type !stay or !switch (or vote F1/F2)"`
4. Either type `!stay`/`!switch` OR press F1/F2 - both should work
5. Match starts with the chosen side

## What We're NOT Doing

- NOT changing the Panorama vote mechanism - it stays as-is
- NOT adding permission requirements to `!stay`/`!switch` - any player on the winning team can use them (matches the vote behavior where all winning team members vote)
- NOT adding a separate vote model - the first `!stay` or `!switch` from a winning team player decides immediately (same as how many CS2 plugins work - "captain calls it")

## Implementation Approach

Add two command handlers ("stay" and "switch") to the ChatCommandHandler that:
1. Check if the server is waiting for a side choice (`WaitingForSideChoice`)
2. Verify the player is on the winning team
3. Cancel any running Panorama vote
4. Call `MatchService.ChooseSide()` with the appropriate value
5. Broadcast the choice to all players

## Phase 1: Add !stay and !switch Command Handlers

### Overview
Add the command handlers and wire them into the command switch.

### Changes Required:

#### 1. ChatCommandHandler - Add command routing
**File**: `Commands/ChatCommandHandler.cs`
**Changes**: Add "stay" and "switch" to the command switch at line 84

Add after line 112 (`"teams" => HandleTeams(player),`):

```csharp
// Knife round side choice
"stay" => HandleStay(player),
"switch" => HandleSwitch(player),
```

#### 2. ChatCommandHandler - Add handler methods
**File**: `Commands/ChatCommandHandler.cs`
**Changes**: Add `HandleStay()` and `HandleSwitch()` methods

```csharp
private bool HandleStay(CCSPlayerController player)
{
    return HandleSideChoice(player, stayOnSide: true);
}

private bool HandleSwitch(CCSPlayerController player)
{
    return HandleSideChoice(player, stayOnSide: false);
}

private bool HandleSideChoice(CCSPlayerController player, bool stayOnSide)
{
    if (!_matchService.WaitingForSideChoice)
    {
        player.PrintToChat($"{_config.ChatPrefix} No side choice is pending.");
        return true;
    }

    if ((int)player.Team != _matchService.KnifeRoundWinnerTeam)
    {
        player.PrintToChat($"{_config.ChatPrefix} Only the winning team can choose sides.");
        return true;
    }

    // Cancel the Panorama vote if it's running
    if (_voteService.PanoramaVote.IsVoteInProgress())
    {
        _voteService.PanoramaVote.CancelVote();
    }

    var choice = stayOnSide ? "STAY" : "SWITCH";
    var teamName = _matchService.KnifeRoundWinnerTeam == 2 ? "Terrorists" : "Counter-Terrorists";
    Server.PrintToChatAll($"{_config.ChatPrefix} {teamName} chose to {choice}! Match starting!");

    _matchService.ChooseSide(stayOnSide);
    return true;
}
```

### Success Criteria:

#### Automated Verification:
- [ ] Project builds: `dotnet build -c Release`

#### Manual Verification:
- [ ] `!start` → knife round → win → type `!stay` → match starts on same side
- [ ] `!start` → knife round → win → type `!switch` → teams swap and match starts
- [ ] Losing team player types `!stay` → gets "Only the winning team" message
- [ ] Player types `!stay` outside of side choice → gets "No side choice is pending" message
- [ ] F1/F2 Panorama vote still works if nobody types `!stay`/`!switch`
- [ ] If player types `!stay` while Panorama vote is showing, vote is cancelled and choice applies

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Update Config Default and README

### Overview
Update the default config message and README to accurately describe both mechanisms.

### Changes Required:

#### 1. PluginConfig - Update default message
**File**: `Config/PluginConfig.cs`
**Changes**: Update `KnifeRoundWinnerMessage` default at line 88

```csharp
public string KnifeRoundWinnerMessage { get; set; } = "{team} won the knife round! Type !stay or !switch (or vote F1/F2)";
```

#### 2. README - Update knife round flow description
**File**: `README.md`
**Changes**: Update line 291

From:
```
4. Winning team captain types `!stay` or `!switch` to choose side
```

To:
```
4. Winning team votes F1 (Stay) / F2 (Switch), or any winning team player types `!stay` or `!switch`
```

#### 3. README - Update config example
**File**: `README.md`
**Changes**: Update line 224

From:
```json
"KnifeRoundWinnerMessage": "{team} won the knife round! Type !stay or !switch to choose side."
```

To:
```json
"KnifeRoundWinnerMessage": "{team} won the knife round! Type !stay or !switch (or vote F1/F2)"
```

### Success Criteria:

#### Automated Verification:
- [ ] Project builds: `dotnet build -c Release`

#### Manual Verification:
- [ ] Chat message after knife round win shows both options

---

## Testing Strategy

### Manual Testing Steps:
1. Enable knife round (`EnableKnifeRound: true`)
2. Start match with `!start`
3. Win knife round by killing opponents
4. Verify chat message mentions both `!stay/!switch` and F1/F2
5. Test `!stay` - verify match starts, teams don't swap
6. Test `!switch` - verify teams swap, match starts
7. Test F1/F2 still works when nobody types chat commands
8. Test that losing team can't use `!stay`/`!switch`
9. Test typing `!stay` when not in side choice phase

## References

- Research: `thoughts/shared/research/2026-02-09-knife-round-f1-f2-vote-not-showing.md`
- Previous research: `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md`
