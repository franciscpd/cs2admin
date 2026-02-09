# Pause Match During Knife Round Side Choice Vote - Implementation Plan

## Overview

Add `mp_pause_match` to `EndKnifeRound()` so the match is paused while the winning team votes on side choice. The `mp_unpause_match` already exists in `ChooseSide()` at line 423.

## Current State Analysis

When the knife round ends, `EndKnifeRound()` only updates internal flags — it does not pause the CS2 engine. The match continues normally while the 30-second Panorama vote runs. `ChooseSide()` already executes `mp_unpause_match` (line 423) before `mp_restartgame 3` (line 424), but since the match was never paused, the unpause is a no-op.

### Key Discoveries:
- `EndKnifeRound()` (`MatchService.cs:389-394`) — only sets `_knifeRoundWinnerTeam`, `_waitingForSideChoice`, `_isKnifeRound`. No server commands.
- `ChooseSide()` (`MatchService.cs:423`) — already has `mp_unpause_match` ready to handle the unpause
- `HandleSideChoice()` (`ChatCommandHandler.cs:856`) — `!stay`/`!switch` also calls `ChooseSide()`, so unpause works via both paths

## Desired End State

After the knife round ends, the CS2 engine match is paused (`mp_pause_match`) while the winning team votes. When the vote completes or `!stay`/`!switch` is used, `ChooseSide()` unpauses and restarts the game.

### How to verify:
- Start a match with knife round, win the knife round
- The match should be paused (freeze time, no new round starts) while the vote is shown
- After voting or using `!stay`/`!switch`, the match unpauses and starts with `mp_restartgame 3`

## What We're NOT Doing

- NOT modifying the `_isPaused` plugin state — this pause is managed by the CS2 engine only, not the plugin's pause state machine
- NOT modifying `ChooseSide()` — the `mp_unpause_match` there already handles this
- NOT changing the vote timing or duration

## Implementation Approach

Add a single `Server.ExecuteCommand("mp_pause_match")` to `EndKnifeRound()`.

## Phase 1: Add mp_pause_match to EndKnifeRound

### Overview
Pause the CS2 engine match when the knife round ends, so players wait while the side choice vote happens.

### Changes Required:

#### 1. EndKnifeRound method
**File**: `Services/MatchService.cs`
**Changes**: Add `mp_pause_match` after updating internal state

```csharp
public void EndKnifeRound(int winnerTeam)
{
    _knifeRoundWinnerTeam = winnerTeam;
    _waitingForSideChoice = true;
    _isKnifeRound = false;
    Server.ExecuteCommand("mp_pause_match");
}
```

### Success Criteria:

#### Automated Verification:
- [ ] Build succeeds: `dotnet build`

#### Manual Verification:
- [ ] Start match with `!start` (knife round enabled)
- [ ] Win the knife round — match should pause immediately
- [ ] Panorama vote appears (F1=Stay, F2=Switch) while match is paused
- [ ] After voting, match unpauses and restarts with 3-second countdown
- [ ] Using `!stay` or `!switch` also unpauses and starts the match
- [ ] Regular match pauses (`!pause`) still work normally after the match goes live

## References

- Research: `thoughts/shared/research/2026-02-09-pause-during-knife-round-vote.md`
