# CS2Admin

A comprehensive server administration plugin for Counter-Strike 2, built with CounterStrikeSharp framework.

## Features

- **Admin Commands**: Kick, ban, mute, slay, slap, respawn players
- **Match Control**: Pause, unpause, restart matches, change maps
- **Vote System**: Public voting for kick, pause, restart, and map changes
- **Admin Management**: Add/remove admins and groups via chat or console
- **Persistent Storage**: SQLite database for bans, mutes, admins, and groups
- **Chat Commands**: Both `.command` and console `css_command` syntax supported

## Requirements

- Counter-Strike 2 Dedicated Server
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) v1.0.305+
- .NET 8.0 Runtime

## Installation

1. Build the plugin: `dotnet build -c Release`
2. Copy the `bin/Release/net8.0/` contents to your server's `addons/counterstrikesharp/plugins/CS2Admin/` folder
3. Restart the server or use `css_plugins load CS2Admin`

## Commands

### Public Chat Commands (`.` prefix)

| Command | Description |
|---------|-------------|
| `.votekick <player>` | Start vote to kick player |
| `.votepause` | Start vote to pause match |
| `.voterestart` | Start vote to restart match |
| `.votechangemap <map>` | Start vote to change map |
| `.yes` / `.no` | Cast vote |

### Admin Chat Commands (`.` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `.kick <player> [reason]` | @css/kick | Kick player |
| `.ban <player> <duration> [reason]` | @css/ban | Ban player |
| `.unban <steamid>` | @css/ban | Unban by SteamID |
| `.mute <player> [duration]` | @css/chat | Mute player |
| `.unmute <player>` | @css/chat | Unmute player |
| `.slay <player>` | @css/slay | Kill player |
| `.slap <player> [damage]` | @css/slay | Slap player |
| `.respawn <player>` | @css/slay | Respawn player |
| `.changemap <map>` | @css/changemap | Change map |
| `.pause` / `.unpause` | @css/generic | Pause/unpause match |
| `.restart` | @css/generic | Restart match |

### Admin Management Commands (`.` prefix)

| Command | Permission | Description |
|---------|------------|-------------|
| `.add_admin <steamid\|player> <flags>` | @css/root | Add a new admin |
| `.remove_admin <steamid\|player>` | @css/root | Remove an admin |
| `.list_admins` | @css/root | List all admins |
| `.add_group <name> <flags> [immunity]` | @css/root | Create admin group |
| `.remove_group <name>` | @css/root | Remove admin group |
| `.list_groups` | @css/root | List all groups |
| `.set_group <steamid\|player> <group>` | @css/root | Assign admin to group |
| `.reload_admins` | @css/root | Reload admins from database |

### Console Commands (`css_` prefix)

All chat commands are available as console commands with `css_` prefix:
- `css_kick`, `css_ban`, `css_votekick`, etc.

## Duration Format

For ban and mute durations:
- `0` - Permanent
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
  "EnableLogging": true
}
```

### Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `DatabasePath` | `data/cs2admin.db` | SQLite database path (relative to plugin folder) |
| `VoteThresholdPercent` | `60` | Percentage of yes votes needed to pass |
| `VoteDurationSeconds` | `30` | How long votes last |
| `VoteCooldownSeconds` | `60` | Cooldown between same vote types |
| `MinimumVotersRequired` | `3` | Minimum players to start a vote |
| `ChatPrefix` | `[CS2Admin]` | Prefix for chat messages |
| `EnableLogging` | `true` | Log admin actions to database |

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

## Admin Permissions

Admins can be managed in two ways:

### 1. In-Game (Recommended)

Use chat or console commands to manage admins dynamically:

```
.add_admin 76561198012345678 @css/kick,@css/ban,@css/slay
.add_group moderator @css/kick,@css/ban,@css/chat 50
.set_group 76561198012345678 moderator
```

### 2. CounterStrikeSharp Config File

Configure in `addons/counterstrikesharp/configs/admins.json`:

```json
{
  "76561198012345678": {
    "identity": "76561198012345678",
    "flags": ["@css/kick", "@css/ban", "@css/slay", "@css/chat", "@css/changemap", "@css/generic"]
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
| `@css/generic` | Pause, unpause, restart match |
| `@css/vip` | VIP flag |
| `@css/reservation` | Reserved slot |

## License

MIT License
