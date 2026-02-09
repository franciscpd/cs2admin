# Fix Knife Round Not Ending Warmup - Implementation Plan

## Overview

When `!start` initiates a knife round, the CS2 engine stays in warmup mode because `StartKnifeRound()` never executes `mp_warmup_end`. This causes the game to appear stuck in warmup after killing the opponent -- the round doesn't finalize properly. The fix is to explicitly end the CS2 engine warmup and remove the redundant double `mp_restartgame` calls, relying on `mp_warmup_end` to handle the transition cleanly.

## Current State Analysis

`StartKnifeRound()` at `MatchService.cs:356-392`:
- Sets `_isWarmup = false` (line 358) -- only updates the **plugin's internal state**
- Sets knife ConVars (lines 365-374)
- Executes `mp_restartgame 1` at line 377 (first restart)
- Executes `mp_restartgame 1` again via a 3-second timer at lines 380-386 (second restart)
- **Does NOT execute `mp_warmup_end`** -- the CS2 engine warmup persists

By comparison, `StartMatch()` at `MatchService.cs:125` calls `EndWarmup()` which properly executes `mp_warmup_end` (line 117). The knife round path skips this entirely.

### Key Discoveries:
- `mp_restartgame` does NOT implicitly end warmup in CS2 ([MatchZy reference](https://github.com/shobhit-pathak/MatchZy))
- MatchZy uses `mp_restartgame 1;mp_warmup_end;` to transition from warmup to knife round
- `EventRoundEnd` fires during warmup but round behavior is different (no proper winner resolution)
- The double restart was a workaround attempt for settings not applying -- the real issue was that warmup was never ended

## Desired End State

After `!start` with knife round enabled:
1. The CS2 engine warmup ends (`mp_warmup_end` is called)
2. Knife round ConVars are applied
3. The game transitions cleanly to knife round without redundant restarts
4. When a player kills the opponent, the round ends normally
5. `EventRoundEnd` fires with the correct `@event.Winner`
6. Side choice vote begins
7. Match transitions to live

**Verification:** After running `!start`, the HUD should NOT show "warmup". When a team is eliminated, the round should end and the knife winner message should appear with the side choice vote. No double restart delays.

## What We're NOT Doing

- Not refactoring the state machine (boolean flags -> enum)
- Not changing the `EndWarmup()` method signature or behavior
- Not modifying the side choice vote flow
- Not changing any other command flows

## Implementation Approach

Modify `StartKnifeRound()` in `MatchService.cs` to:
1. Add `mp_warmup_end` and `mp_warmup_pausetimer 0` to properly end the CS2 engine warmup
2. Remove the first `mp_restartgame 1` (line 377)
3. Remove the 3-second timer with the second `mp_restartgame 1` (lines 380-386)

The `mp_warmup_end` command handles the transition from warmup to live gameplay. The knife ConVars are set before this command so they take effect when the new round starts.

## Phase 1: Fix StartKnifeRound

### Overview
Add `mp_warmup_end` to properly end warmup and remove the two redundant `mp_restartgame` calls.

### Changes Required:

#### 1. MatchService.cs - StartKnifeRound method
**File**: `Services/MatchService.cs`
**Method**: `StartKnifeRound()` (lines 356-392)

Replace the current method with:

```csharp
public void StartKnifeRound(CCSPlayerController? admin = null)
{
    _isWarmup = false;
    _isKnifeRound = true;
    _isKnifeOnly = true;
    _waitingForSideChoice = false;
    _knifeRoundWinnerTeam = 0;

    // Knife round settings - disable buying completely
    Server.ExecuteCommand("mp_respawn_on_death_ct 0");
    Server.ExecuteCommand("mp_respawn_on_death_t 0");
    Server.ExecuteCommand("mp_free_armor 1");
    Server.ExecuteCommand("mp_give_player_c4 0");
    Server.ExecuteCommand("mp_ct_default_secondary \"\"");
    Server.ExecuteCommand("mp_t_default_secondary \"\"");
    Server.ExecuteCommand("mp_buytime 0");
    Server.ExecuteCommand("mp_buy_during_immunity_time 0");
    Server.ExecuteCommand("mp_startmoney 0");
    Server.ExecuteCommand("mp_maxmoney 0");

    // End CS2 engine warmup - without this the engine stays in warmup mode
    // and rounds don't finalize properly after kills
    Server.ExecuteCommand("mp_warmup_pausetimer 0");
    Server.ExecuteCommand("mp_warmup_end");

    if (_enableLogging && _database != null && admin != null)
    {
        _database.LogAction("KNIFE_ROUND_START", admin.SteamID, admin.PlayerName, null, null, null);
    }
}
```

**What changed:**
1. **Added** `mp_warmup_pausetimer 0` -- resets the paused warmup timer (was set to 1 during `StartWarmup()`)
2. **Added** `mp_warmup_end` -- tells the CS2 engine to exit warmup mode and start a new round
3. **Removed** `mp_restartgame 1` (was line 377)
4. **Removed** the 3-second timer with second `mp_restartgame 1` (was lines 380-386)

ConVars are set BEFORE `mp_warmup_end` so that when the engine starts the new round after ending warmup, the knife settings are already active.

### Success Criteria:

#### Automated Verification:
- [x] Plugin compiles without errors: `dotnet build`

#### Manual Verification:
- [ ] Start a server with 2+ players and `EnableKnifeRound: true`
- [ ] Execute `!start` -- verify the HUD does NOT show "warmup"
- [ ] Verify no double restart delays (clean single transition)
- [ ] Play the knife round -- verify players only have knives
- [ ] Kill the opponent -- verify the round ends properly
- [ ] Verify the knife round winner message appears in chat
- [ ] Verify the side choice vote (F1/F2) appears for the winning team
- [ ] Vote and verify the match transitions to live with normal economy

---

## Testing Strategy

### Manual Testing Steps:
1. Load the plugin on a CS2 server with `EnableWarmupMode: true` and `EnableKnifeRound: true`
2. Connect 2 players to the server
3. Verify warmup is active (HUD shows warmup, players respawn)
4. Admin executes `!start`
5. Verify warmup ends and knife round begins (no more "warmup" in HUD, no double restart)
6. One player kills the other
7. Verify `EventRoundEnd` fires (knife winner message appears)
8. Verify side choice vote starts
9. Vote F1 or F2
10. Verify match starts live with $800, normal buy time, pistols

### Edge Cases:
- Test with `EnableKnifeRound: false` to ensure direct match start still works
- Test `!warmup` after a match to verify warmup restart still works
- Test `!knife` toggle during warmup (separate from !start flow)

## References

- Research: `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md`
- MatchZy knife round implementation: https://github.com/shobhit-pathak/MatchZy/blob/dev/Utility.cs
- CS2 warmup commands: `mp_warmup_end`, `mp_warmup_start`, `mp_warmup_pausetimer`
