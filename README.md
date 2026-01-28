# CS2Admin

A comprehensive server administration plugin for Counter-Strike 2, built with CounterStrikeSharp framework.

## Features

- **Admin Commands**: Kick, ban, mute, slay, slap, respawn players
- **Match Control**: Pause, unpause, restart matches, change maps
- **Warmup Mode**: Automatic warmup with unlimited money until admin starts match
- **Vote System**: Public voting for kick, pause, restart, and map changes
- **Admin Management**: Add/remove admins and groups via chat or console
- **Welcome Messages**: Customizable welcome and join announcements
- **Persistent Storage**: SQLite database for bans, mutes, admins, and logs
- **Chat Commands**: Both `!command` and console `css_command` syntax supported
- **Interactive Menus**: Player selection menus for admin commands

## Requirements

- Counter-Strike 2 Dedicated Server
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) v1.0.305+
- [Metamod:Source](https://www.sourcemm.net/) 2.x

## Installation

### Option 1: Download Release (Recommended)

1. Download the latest release from [GitHub Releases](https://github.com/YOUR_USERNAME/cs2admin/releases)
2. Extract the ZIP file
3. Copy all files to your server's `addons/counterstrikesharp/plugins/CS2Admin/` folder
4. Restart the server

### Option 2: Build from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/cs2admin.git
   cd cs2admin
   ```

2. Build the plugin:
   ```bash
   dotnet build -c Release
   ```

3. Copy files to your server:
   ```bash
   cp -r bin/Release/net8.0/* /path/to/server/addons/counterstrikesharp/plugins/CS2Admin/
   ```

4. Restart the server or load the plugin:
   ```
   css_plugins load CS2Admin
   ```

### Verify Installation

After installation, you should see in the server console:
```
[CS2Admin] CS2Admin loaded successfully!
```

The plugin will automatically create:
- `plugins/CS2Admin/data/cs2admin.db` - SQLite database
- `configs/plugins/CS2Admin/CS2Admin.json` - Configuration file

### First Admin Setup

To set up your first admin, edit `addons/counterstrikesharp/configs/admins.json`:

```json
{
  "YOUR_STEAM_ID_64": {
    "identity": "YOUR_STEAM_ID_64",
    "flags": ["@css/root"]
  }
}
```

Replace `YOUR_STEAM_ID_64` with your Steam ID (e.g., `76561198012345678`).

After that, you can manage admins in-game using `!add_admin` command.

## Commands

### Public Chat Commands (`!` prefix)

| Command | Description |
|---------|-------------|
| `!help` | Show interactive help menu |
| `!votekick [player]` | Start vote to kick player (shows menu if no player specified) |
| `!votepause` | Start vote to pause match |
| `!voterestart` | Start vote to restart match |
| `!votemap <map>` | Start vote to change map |
| `!yes` / `!no` | Cast vote |

### Admin Chat Commands (`!` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `!kick [player] [reason]` | @css/kick | Kick player (shows menu if no player) |
| `!ban [player] [duration] [reason]` | @css/ban | Ban player (shows menu if no player) |
| `!unban <steamid>` | @css/ban | Unban by SteamID |
| `!mute [player] [duration]` | @css/chat | Mute player (shows menu if no player) |
| `!unmute [player]` | @css/chat | Unmute player (shows menu if no player) |
| `!slay [player]` | @css/slay | Kill player (shows menu if no player) |
| `!slap [player] [damage]` | @css/slay | Slap player (shows menu if no player) |
| `!respawn [player]` | @css/slay | Respawn player (shows menu if no player) |
| `!map <map>` | @css/changemap | Change map |
| `!pause` / `!unpause` | @css/generic | Pause/unpause match |
| `!restart` | @css/generic | Restart match |

### Warmup & Match Commands (`!` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `!start` | @css/generic | Start match (knife round if enabled) |
| `!warmup` | @css/generic | Start warmup mode |
| `!endwarmup` | @css/generic | End warmup without restart |
| `!knife` | @css/generic | Toggle knife only mode |

### Knife Round Commands (`!` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `!stay` | Winner team | Stay on current side after knife round |
| `!switch` | Winner team | Switch sides after knife round |

### Admin Management Commands (`!` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `!add_admin <steamid\|player> <flags>` | @css/root | Add a new admin |
| `!remove_admin <steamid\|player>` | @css/root | Remove an admin |
| `!list_admins` | @css/root | List all admins |
| `!add_group <name> <flags> [immunity]` | @css/root | Create admin group |
| `!remove_group <name>` | @css/root | Remove admin group |
| `!list_groups` | @css/root | List all groups |
| `!set_group <steamid\|player> <group>` | @css/root | Assign admin to group |
| `!reload_admins` | @css/root | Reload admins from database |

### Console Commands (`css_` prefix)

All chat commands are available as console commands with `css_` prefix:
- `css_kick`, `css_ban`, `css_votekick`, `css_start`, `css_warmup`, etc.

## Interactive Player Selection

When you run admin commands without specifying a player, an interactive menu appears:

1. **Simple commands** (`!kick`, `!slay`, `!respawn`, `!unmute`, `!votekick`):
   - Shows a list of players to select

2. **Commands with duration** (`!ban`, `!mute`):
   - First shows player selection
   - Then shows duration options (e.g., 30min, 1h, 1d, permanent)

3. **Slap command** (`!slap`):
   - First shows player selection
   - Then shows damage options (0, 5, 10, 25, 50)

**Example:**
```
Player types: !kick
Menu appears: "Kick - Select Player"
   1. PlayerOne
   2. PlayerTwo
   3. PlayerThree
Player selects option 2
PlayerTwo is kicked
```

## Duration Format

For ban and mute durations:
- `0` - Permanent
- `30s` - 30 seconds
- `30m` - 30 minutes
- `1h` - 1 hour
- `1d` - 1 day
- `1w` - 1 week
- `1M` - 1 month (30 days)
- `1y` - 1 year (365 days)

## Configuration

Configuration is stored in `addons/counterstrikesharp/configs/plugins/CS2Admin/CS2Admin.json`:

```json
{
  "DatabasePath": "data/cs2admin.db",
  "VoteThresholdPercent": 60,
  "VoteDurationSeconds": 30,
  "VoteCooldownSeconds": 60,
  "MinimumVotersRequired": 3,
  "DefaultBanReason": "Banned by admin",
  "DefaultKickReason": "Kicked by admin",
  "DefaultMuteReason": "Muted by admin",
  "ChatPrefix": "[CS2Admin]",
  "EnableLogging": true,
  "EnableWelcomeMessage": true,
  "WelcomeMessage": "Welcome to the server, {player}!",
  "WelcomeMessageDelay": 3.0,
  "AnnouncePlayerJoin": true,
  "PlayerJoinMessage": "{player} joined the server.",
  "EnableWarmupMode": true,
  "WarmupMoney": 60000,
  "WarmupMessage": "Server is in warmup. Waiting for admin to start the match.",
  "MatchStartMessage": "Match starting! Good luck, have fun!",
  "MinPlayersToStart": 2,
  "EnableKnifeRound": true,
  "KnifeRoundMessage": "Knife round! Winner chooses side.",
  "KnifeRoundWinnerMessage": "{team} won the knife round! Type !stay or !switch to choose side."
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `DatabasePath` | `data/cs2admin.db` | SQLite database path |
| `VoteThresholdPercent` | `60` | Percentage of yes votes needed |
| `VoteDurationSeconds` | `30` | How long votes last |
| `VoteCooldownSeconds` | `60` | Cooldown between same vote types |
| `MinimumVotersRequired` | `3` | Minimum players to start a vote |
| `ChatPrefix` | `[CS2Admin]` | Prefix for chat messages |
| `EnableLogging` | `true` | Log admin actions to database |

### Welcome Message Options

| Option | Default | Description |
|--------|---------|-------------|
| `EnableWelcomeMessage` | `true` | Show welcome message to joining players |
| `WelcomeMessage` | `Welcome to the server, {player}!` | Message shown to player |
| `WelcomeMessageDelay` | `3.0` | Delay in seconds before showing |
| `AnnouncePlayerJoin` | `true` | Announce joins to all players |
| `PlayerJoinMessage` | `{player} joined the server.` | Join announcement |

**Available variables:** `{player}`, `{steamid}`

### Warmup Options

| Option | Default | Description |
|--------|---------|-------------|
| `EnableWarmupMode` | `true` | Enable automatic warmup on map load |
| `WarmupMoney` | `60000` | Money during warmup |
| `WarmupMessage` | `Server is in warmup...` | Message shown during warmup |
| `MatchStartMessage` | `Match starting!...` | Message when match starts |
| `MinPlayersToStart` | `2` | Minimum players to start match |

### Knife Round Options

| Option | Default | Description |
|--------|---------|-------------|
| `EnableKnifeRound` | `true` | Enable knife round before match |
| `KnifeRoundMessage` | `Knife round!...` | Message shown during knife round |
| `KnifeRoundWinnerMessage` | `{team} won...` | Message when team wins |

**Available variables:** `{team}` (Terrorists or Counter-Terrorists)

## Warmup Mode

When enabled, the server automatically enters warmup mode on map load:

- Unlimited money ($60,000 by default)
- Automatic respawn on death
- Buy anywhere on the map
- Free armor
- Infinite buy time

Use `!start` to begin the match when ready.

## Knife Round

When enabled, a knife-only round is played before the match starts:

1. Admin uses `!start` to end warmup
2. Knife round begins - players only have knives
3. The winning team is announced
4. Winning team captain types `!stay` or `!switch` to choose side
5. Match begins with the chosen sides

**Manual knife mode:** Admins can toggle knife-only mode at any time with `!knife`

## Player Targeting

Players can be targeted by:
- **Name**: Partial or exact name (case-insensitive)
- **User ID**: `#123`
- **SteamID64**: `76561198012345678`

## Database

The plugin stores data in SQLite at `plugins/CS2Admin/data/cs2admin.db`:
- `bans` - Player ban records
- `mutes` - Player mute records
- `admins` - Admin records with flags and groups
- `admin_groups` - Admin groups with permissions
- `logs` - Admin action logs (when enabled)

The database is created automatically on first run.

## Admin Permissions

### Managing Admins In-Game

Use chat or console commands to manage admins:

```
!add_admin 76561198012345678 @css/kick,@css/ban,@css/slay
!add_group moderator @css/kick,@css/ban,@css/chat 50
!set_group 76561198012345678 moderator
```

### Using Config File

Configure in `addons/counterstrikesharp/configs/admins.json`:

```json
{
  "76561198012345678": {
    "identity": "76561198012345678",
    "flags": ["@css/root"]
  }
}
```

### Available Flags

| Flag | Description |
|------|-------------|
| `@css/root` | Full access (required for admin management) |
| `@css/kick` | Kick players |
| `@css/ban` | Ban/unban players |
| `@css/slay` | Slay, slap, respawn players |
| `@css/chat` | Mute/unmute players |
| `@css/changemap` | Change maps |
| `@css/generic` | Pause, unpause, restart, warmup control |
| `@css/vip` | VIP flag |
| `@css/reservation` | Reserved slot |

## Troubleshooting

### Plugin not loading
- Verify CounterStrikeSharp is installed correctly
- Check server console for error messages
- Ensure all DLL files are in the plugin folder

### Commands not working
- Verify you have the required permissions
- Check if the command prefix is correct (`!` for chat, `css_` for console)
- Ensure the player name/SteamID is valid

### Database errors
- The `data/` folder is created automatically
- Ensure the server has write permissions to the plugin folder

## License

MIT License
