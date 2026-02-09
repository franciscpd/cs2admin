# Add Multi-Language Support (i18n)

## Overview

Add full multi-language support to CS2Admin using CounterStrikeSharp's built-in `Localizer` system (`IStringLocalizer`). This will replace all ~250+ hardcoded English strings with localization keys loaded from `lang/*.json` files, supporting per-player language via `Localizer.ForPlayer()`.

## Current State Analysis

- **~250+ hardcoded English strings** across 8 files
- **10 configurable messages** in `PluginConfig.cs` (will migrate to Localizer)
- **No localization infrastructure** exists - all strings are inline
- `CS2Admin.cs` inherits `BasePlugin` which provides `Localizer` property
- Services/handlers receive config via constructor from `CS2AdminServiceCollection`
- `PrintToChatAll` is used for ~45 broadcasts (needs per-player loop for i18n)

### Key Architecture Points:
- `CS2Admin.cs` (BasePlugin) → owns `Localizer`
- `CS2AdminServiceCollection` → creates all services/handlers, distributes config
- `ChatCommandHandler` → receives `BasePlugin` already (can access `Localizer` through it)
- `MatchService`, `VoteService` → receive individual config values, no access to `Localizer`
- `AdminCommands`, `VoteCommands`, `AdminManagementCommands` → receive `PluginConfig`

## Desired End State

After this plan is complete:
- All user-facing strings come from `lang/*.json` files
- `lang/en.json` contains all English strings (default/fallback)
- `lang/pt.json` contains Portuguese translations
- Per-player messages (`PrintToChat`, menus) use player's language
- Broadcast messages (`PrintToChatAll`) iterate players and localize per-player
- The 10 config messages are removed from `PluginConfig.cs` (replaced by Localizer)
- A `LocalizedBroadcast()` helper replaces `Server.PrintToChatAll()` calls
- Plugin builds and passes all existing functionality

### Verification:
1. Build succeeds: `dotnet build -c Release`
2. Player with `!lang pt` sees Portuguese messages
3. Player with `!lang en` (or default) sees English messages
4. All menus, error messages, broadcasts show correctly in both languages
5. Config file no longer contains message strings (breaking change - documented)

## What We're NOT Doing

- NOT adding more than 2 languages initially (en + pt) - community can add more
- NOT localizing debug/console log messages (Console.WriteLine, Logger.Log)
- NOT localizing SFUI keys (translated by CS2 engine natively)
- NOT adding a language selector menu (players use built-in `!lang <code>`)
- NOT making the ChatPrefix localizable (it stays in config as a brand/server identity)

## Design Decisions

### Key Naming Convention
Dot notation grouped by feature area, following CS2-AdminPlus pattern:
```
"error.no_permission"
"error.player_not_found"
"admin.kick.success"
"admin.ban.success"
"vote.started"
"menu.kick.title"
"menu.ban.duration"
"match.warmup.message"
"knife.winner"
```

### Config Messages Migration
The 10 configurable messages in `PluginConfig.cs` will be **removed** and moved to `lang/*.json`. This is a breaking change for users who customized these messages in the JSON config. The migration path is: edit `lang/en.json` (or their preferred language file) instead.

Messages being removed from config:
- `DefaultBanReason`, `DefaultKickReason`, `DefaultMuteReason`
- `WelcomeMessage`, `PlayerJoinMessage`
- `WarmupMessage`, `MatchStartMessage`
- `KnifeRoundMessage`, `KnifeRoundWinnerMessage`

`ChatPrefix` stays in config (it's a server identity, not translatable content).

### Localizer Distribution
Pass `IStringLocalizer` from `BasePlugin` to `CS2AdminServiceCollection`, which distributes it to all services/handlers via constructor. This is the simplest approach and follows the pattern other CS2 plugins use.

### Broadcast Helper
Create a `LocalizedHelper` static utility class with:
```csharp
public static void BroadcastLocalized(IStringLocalizer localizer, string chatPrefix, string key, params object[] args)
{
    foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && !p.IsHLTV))
    {
        player.PrintToChat($"{chatPrefix} {localizer.ForPlayer(player, key, args)}");
    }
}
```

This replaces all `Server.PrintToChatAll()` calls.

---

## Phase 1: Infrastructure - Create lang files and Localizer plumbing

### Overview
Set up the localization infrastructure: create `lang/en.json` with all keys, add `IStringLocalizer` parameter to all services/handlers, create the broadcast helper.

### Changes Required:

#### 1. Create `lang/en.json`
**File**: `lang/en.json` (NEW)
- All ~250 strings as flat key-value pairs
- Uses `{0}`, `{1}` numbered placeholders (Localizer standard)
- Grouped by dot notation: `error.*`, `admin.*`, `vote.*`, `menu.*`, `match.*`, `knife.*`, `player.*`

#### 2. Create `Utils/LocalizedHelper.cs`
**File**: `Utils/LocalizedHelper.cs` (NEW)
- `BroadcastLocalized(localizer, prefix, key, args)` - per-player broadcast
- `PlayerMessage(localizer, player, prefix, key, args)` - single player message with prefix

#### 3. Update `CS2AdminServiceCollection`
**File**: `CS2AdminServiceCollection.cs`
- Add `IStringLocalizer` parameter to constructor
- Pass it to all handlers/services that emit user-facing messages:
  - `ChatCommandHandler`, `AdminCommands`, `VoteCommands`, `AdminManagementCommands`
  - `VoteService`, `MatchService`, `PlayerConnectionHandler`
- Update `BroadcastMessage` to use `LocalizedHelper.BroadcastLocalized`

#### 4. Update `CS2Admin.cs`
**File**: `CS2Admin.cs`
- Pass `Localizer` to `CS2AdminServiceCollection` constructor
- Replace inline `Server.PrintToChatAll` calls with `LocalizedHelper.BroadcastLocalized`
- Replace inline `player.PrintToChat` calls with localized versions

#### 5. Update all service/handler constructors
**Files**: All services and command handlers
- Add `IStringLocalizer localizer` parameter
- Store as `private readonly IStringLocalizer _localizer`

#### 6. Remove config messages from `PluginConfig.cs`
**File**: `Config/PluginConfig.cs`
- Remove: `DefaultBanReason`, `DefaultKickReason`, `DefaultMuteReason`
- Remove: `WelcomeMessage`, `PlayerJoinMessage`
- Remove: `WarmupMessage`, `MatchStartMessage`
- Remove: `KnifeRoundMessage`, `KnifeRoundWinnerMessage`
- Keep: `ChatPrefix` and all non-message settings

### Success Criteria:
- [ ] Project builds: `dotnet build -c Release`
- [ ] `lang/en.json` exists with all keys
- [ ] All services/handlers accept `IStringLocalizer`

**Implementation Note**: After this phase, the infrastructure is ready but strings are still hardcoded. Phase 2 migrates the actual strings.

---

## Phase 2: Migrate strings in ChatCommandHandler.cs

### Overview
Migrate all ~150+ hardcoded strings in `ChatCommandHandler.cs` to use `_localizer`. This is the largest file and covers most user-facing messages.

### Changes Required:

#### 1. Replace all `PrintToChat` with localized calls
**File**: `Commands/ChatCommandHandler.cs`
- Error messages: `player.PrintToChat($"{_config.ChatPrefix} Player not found.")` → `player.PrintToChat($"{_config.ChatPrefix} {_localizer.ForPlayer(player, "error.player_not_found")}")`
- Permission messages: same pattern with `"error.no_permission"`
- All admin action feedback messages

#### 2. Replace all `Server.PrintToChatAll` with `LocalizedHelper.BroadcastLocalized`
**File**: `Commands/ChatCommandHandler.cs`
- All broadcast messages (kick, ban, mute, slay, slap, respawn, map change, pause, etc.)

#### 3. Replace all menu titles and items
**File**: `Commands/ChatCommandHandler.cs`
- Menu titles use `_localizer.ForPlayer(player, "menu.kick.title")` etc.
- Menu items use `_localizer.ForPlayer(player, "menu.duration.30min")` etc.
- Duration labels, damage labels, navigation items

### Success Criteria:
- [ ] Project builds: `dotnet build -c Release`
- [ ] No hardcoded English strings remain in `ChatCommandHandler.cs` (except flag names and technical identifiers)

---

## Phase 3: Migrate strings in remaining command files

### Overview
Migrate strings in `AdminCommands.cs`, `VoteCommands.cs`, and `AdminManagementCommands.cs`.

### Changes Required:

#### 1. AdminCommands.cs (~40 strings)
- Console command usage messages, error messages, action broadcasts
- Same patterns as ChatCommandHandler

#### 2. VoteCommands.cs (~15 strings)
- Vote-related error messages and descriptions

#### 3. AdminManagementCommands.cs (~45 strings)
- Admin management messages, list formatting, flag validation

### Success Criteria:
- [ ] Project builds: `dotnet build -c Release`
- [ ] No hardcoded English strings remain in command files

---

## Phase 4: Migrate strings in services and handlers

### Overview
Migrate strings in `VoteService.cs`, `MatchService.cs`, `PlayerConnectionHandler.cs`, and `CS2Admin.cs`.

### Changes Required:

#### 1. VoteService.cs (~10 strings)
- Vote status messages, vote descriptions, side choice messages

#### 2. MatchService.cs (~3 strings)
- Pause HUD messages (PrintToCenter), reconnect message

#### 3. PlayerConnectionHandler.cs (~2 strings)
- Ban kick message, disconnect pause message

#### 4. CS2Admin.cs (~1 string + broadcasts)
- Buy blocked message, warmup/knife round broadcasts in event handlers

#### 5. CS2AdminServiceCollection.cs
- `BroadcastMessage` callback and `OnVotePassed` messages

### Success Criteria:
- [ ] Project builds: `dotnet build -c Release`
- [ ] No hardcoded English strings remain in any source file (except debug logs, flag names, and technical identifiers)

---

## Phase 5: Add Portuguese translation and update docs

### Overview
Create `lang/pt.json` with Portuguese translations and update README to document the i18n system.

### Changes Required:

#### 1. Create `lang/pt.json`
**File**: `lang/pt.json` (NEW)
- Full Portuguese translation of all keys from `en.json`

#### 2. Update README.md
**File**: `README.md`
- Add "Multi-Language Support" section documenting:
  - How to use `!lang <code>` to set player language
  - How to add new languages (copy `lang/en.json`, translate, name as `lang/<code>.json`)
  - List of supported languages
  - Note about config messages migration (breaking change)
- Remove the 10 config message entries from the Configuration Options table
- Update the config JSON example

#### 3. Bump version
**File**: `CS2Admin.cs`
- Bump `ModuleVersion` to `0.8.0` (breaking change due to config removal)

### Success Criteria:
- [ ] Project builds: `dotnet build -c Release`
- [ ] `lang/en.json` and `lang/pt.json` both exist with all keys
- [ ] README documents the i18n system
- [ ] Version bumped to 0.8.0

---

## Testing Strategy

### Automated:
- `dotnet build -c Release` after each phase

### Manual Testing Steps:
1. Load plugin on server
2. Verify default (English) messages display correctly
3. Run `!lang pt` and verify Portuguese messages
4. Test all command categories: admin, vote, match, warmup, knife round
5. Test menus show in correct language
6. Test broadcasts show in each player's language
7. Verify `ChatPrefix` still works from config
8. Verify old config message properties are ignored if present in config file

## References

- Research: `thoughts/shared/research/2026-02-09-multi-language-support.md`
- [CounterStrikeSharp WithTranslations Example](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/examples/WithTranslations/WithTranslationsPlugin.cs)
- [CS2-AdminPlus lang files](https://github.com/debr1sj/CS2-AdminPlus)
