# Rename !help to !command and Freeze Player on Menu Open

## Overview

Rename the `!help` chat command to `!command` and enable player freezing while any WASD menu is open, using CS2MenuManager's built-in `WasdMenu_FreezePlayer` property.

## Current State Analysis

- The main admin menu is opened via `!help` (`ChatCommandHandler.cs:87`)
- All menus use `WasdMenu` from CS2MenuManager v1.0.30
- No player freezing is currently applied when menus are open
- CS2MenuManager has a built-in `WasdMenu_FreezePlayer` property that, when set to `true`, automatically freezes the player while the menu is displayed and unfreezes on close

### Key Discoveries:
- `ChatCommandHandler.cs:84` - switch statement routing chat commands
- `ChatCommandHandler.cs:87` - `"help"` case that opens the admin menu
- CS2MenuManager's `WasdMenu` class exposes `WasdMenu_FreezePlayer` boolean property
- When `WasdMenu_FreezePlayer = true`, the library calls `Player.Freeze()` every tick and `Player.Unfreeze()` on close

## Desired End State

1. `!command` opens the admin menu (replacing `!help`)
2. `!help` no longer works
3. All WASD menus freeze the player while open (no walking, no accidental game menu interactions)
4. Player is automatically unfrozen when menu closes (selection, exit, or timeout)

### Verification:
- Type `!command` in chat -> admin menu opens, player is frozen
- Type `!help` in chat -> nothing happens
- Navigate menu with W/S/E/A/R -> player does not move
- Close menu -> player can move again
- All existing admin sub-menus (kick, ban, etc.) also freeze the player

## What We're NOT Doing

- Not blocking native game menus (buy menu, scoreboard) via AddCommandListener - freezing is sufficient
- Not adding CSSharpUtils dependency - using CS2MenuManager's built-in freeze
- Not changing any console commands (css_* commands remain unchanged)
- Not changing VoteCommands.cs menus (those are vote menus, not admin menus) - though they will also benefit from the freeze

## Implementation Approach

Two simple changes:
1. Rename the command string in the switch statement
2. Set `WasdMenu_FreezePlayer = true` on every `WasdMenu` instance after creation

## Phase 1: Rename !help to !command and Enable Freeze

### Overview
Single phase - rename the command and add freeze to all menus.

### Changes Required:

#### 1. Rename command in switch statement
**File**: `Commands/ChatCommandHandler.cs`
**Change**: Replace `"help"` with `"command"` in the switch case (line 87)

```csharp
// Before:
"help" => HandleHelp(player),

// After:
"command" => HandleHelp(player),
```

#### 2. Enable WasdMenu_FreezePlayer on all menus in ChatCommandHandler
**File**: `Commands/ChatCommandHandler.cs`

Set `WasdMenu_FreezePlayer = true` on every `new WasdMenu(...)` instance. There are ~19 locations. The pattern is:

```csharp
// Before:
var menu = new WasdMenu("Title", _plugin);

// After:
var menu = new WasdMenu("Title", _plugin) { WasdMenu_FreezePlayer = true };
```

All locations in `ChatCommandHandler.cs`:
- Line 147: `ShowPlayerSelectionMenu` - player selection
- Line 243: `ShowMapSelectionMenu` - map selection
- Line 347: `ShowBanDurationMenu` - ban duration
- Line 446: `ShowMuteDurationMenu` - mute duration
- Line 576: `ShowSlapDamageMenu` - slap damage
- Line 828: `ShowTeamSelectionMenu` - team selection
- Line 890: `HandleHelp` - main menu
- Line 918: `ShowVoteCommands` - vote commands
- Line 931: `ShowAdminCommands` - admin commands list
- Line 963: `ShowRootAdminCommands` - root admin commands
- Line 987: `ShowAddAdminMenu` - add admin player select
- Line 1000: `ShowFlagSelectionMenu` - flag selection
- Line 1035: `ShowRemoveAdminMenu` - remove admin
- Line 1071: `ShowEditAdminMenu` - edit admin
- Line 1090: `ShowAdminPermissionsMenu` - admin permissions
- Line 1106: `ShowAddFlagsMenu` - toggle flags
- Line 1148: `ShowFlagSelectionMenuForEdit` - role preset
- Line 1182: `ShowSetGroupMenu` - set group admin select
- Line 1208: `ShowGroupSelectionForAdmin` - group selection

#### 3. Enable WasdMenu_FreezePlayer on VoteCommands menus
**File**: `Commands/VoteCommands.cs`

Same pattern for vote command menus:
- Line 72: Vote kick player selection
- Line 158: Vote map selection

```csharp
// Before:
var menu = new WasdMenu("Vote Kick - Select Player", _plugin);

// After:
var menu = new WasdMenu("Vote Kick - Select Player", _plugin) { WasdMenu_FreezePlayer = true };
```

#### 4. Update README documentation
**File**: `README.md`

- Replace `!help` references with `!command`
- Add note about player freeze during menu navigation

### Success Criteria:

#### Automated Verification:
- [ ] Project builds cleanly: `dotnet build`
- [ ] No `"help"` command reference remains in switch statement
- [ ] All `new WasdMenu(` instances have `WasdMenu_FreezePlayer = true`

#### Manual Verification:
- [ ] Type `!command` in game chat -> menu opens, player cannot move
- [ ] Type `!help` in game chat -> no response (command not found)
- [ ] Navigate menu with W/S -> player stays frozen in place
- [ ] Close menu (R key or timeout) -> player can move again
- [ ] Sub-menus (e.g., `!kick` without args) also freeze player
- [ ] Vote menus (`!votekick` without args) also freeze player

## References

- Research: `thoughts/shared/research/2026-02-09-admin-command-rename-and-menu-input-blocking.md`
- CS2MenuManager source: `WasdMenu_FreezePlayer` property in `BaseMenu_Variables.cs`
- CS2MenuManager freeze implementation: `WasdMenuInstance.OnTick()` calls `Player.Freeze()` when enabled
