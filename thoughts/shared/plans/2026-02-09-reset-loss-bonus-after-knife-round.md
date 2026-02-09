# Reset Loss Bonus After Knife Round - Implementation Plan

## Overview

Add `mp_starting_losses 1` to the knife→live and warmup→live transitions to ensure the loss bonus counter starts at the competitive default value (1) after the knife round. This matches the behavior of MatchZy and CS2's built-in competitive mode, where losing the pistol round gives $1900 instead of $1400.

## Current State Analysis

The plugin transitions from knife round to live match in `ChooseSide()` (`MatchService.cs:395-428`), setting economy ConVars and executing `mp_restartgame 3`. However, `mp_starting_losses` is never set by the plugin anywhere, leaving it at the server's default (which may be 0 instead of the competitive standard of 1).

### Key Discoveries:
- MatchZy explicitly sets `mp_starting_losses 1` in its default live configuration ([source](https://github.com/shobhit-pathak/MatchZy/blob/dev/Utility.cs))
- CS2 competitive matchmaking uses `mp_starting_losses 1` so that pistol round losers get $1900 instead of $1400
- The plugin currently does not set `mp_starting_losses` in any transition: warmup→knife, knife→live, or warmup→live
- `mp_restartgame` resets game statistics but explicit documentation on whether it resets the loss streak counter is ambiguous

## Desired End State

After implementing this change:
1. When the knife round ends and the live match starts via `ChooseSide()`, the loss bonus counter should be explicitly set to the competitive value (1)
2. When a match starts directly (without knife round) via `StartMatch()` → `EndWarmup()`, the loss bonus counter should also be set to 1
3. Both teams should receive $1900 (not $1400) after losing the first round of each half, matching the CS2 competitive standard

### How to verify:
- Start a match with knife round enabled
- After knife round, the winning team picks a side
- Play the pistol round — the losing team should receive $1900

## What We're NOT Doing

- NOT changing any other economy ConVars — the existing values are already correct
- NOT adding `mp_consecutive_loss_aversion` or `mp_consecutive_loss_max` — defaults are correct
- NOT modifying warmup or knife round economy — those are already properly configured

## Implementation Approach

Add a single ConVar (`mp_starting_losses 1`) to two methods in `MatchService.cs`:
1. `ChooseSide()` — the knife→live transition
2. `EndWarmup()` — the warmup→live transition (used by `StartMatch()` when knife is disabled)

## Phase 1: Add mp_starting_losses to Match Transitions

### Overview
Add `mp_starting_losses 1` before `mp_restartgame` in both match start paths.

### Changes Required:

#### 1. ChooseSide() — Knife → Live Transition
**File**: `Services/MatchService.cs`
**Changes**: Add `mp_starting_losses 1` after the economy ConVars and before `mp_restartgame 3`

```csharp
// In ChooseSide(), after line 412 (mp_death_drop_gun 1) and before line 414 (if !stayOnSide):
Server.ExecuteCommand("mp_starting_losses 1");
```

#### 2. EndWarmup() — Warmup → Live Transition
**File**: `Services/MatchService.cs`
**Changes**: Add `mp_starting_losses 1` after the economy ConVars and before `mp_warmup_end`

```csharp
// In EndWarmup(), after line 116 (mp_respawn_on_death_t 0) and before line 117 (mp_warmup_end):
Server.ExecuteCommand("mp_starting_losses 1");
```

### Success Criteria:

#### Automated Verification:
- [ ] Build succeeds: `dotnet build`

#### Manual Verification:
- [ ] Start a match with knife round enabled (`!start`)
- [ ] Complete the knife round and choose a side
- [ ] Play the pistol round — losing team should receive $1900 (not $1400)
- [ ] Start a match without knife round (`EnableKnifeRound = false` in config, then `!start`)
- [ ] Play the pistol round — losing team should receive $1900

## References

- Research document: `thoughts/shared/research/2026-02-09-loss-bonus-reset-after-knife-round.md`
- MatchZy live defaults: [Utility.cs](https://github.com/shobhit-pathak/MatchZy/blob/dev/Utility.cs) — sets `mp_starting_losses 1`
- CS2 mp_starting_losses docs: [totalcsgo.com](https://totalcsgo.com/commands/mpstartinglosses)
- Get5 issue confirming importance: [Issue #309](https://github.com/splewis/get5/issues/309)
