using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2Admin.Config;
using CS2Admin.Services;

namespace CS2Admin.Handlers;

public class PlayerConnectionHandler
{
    private readonly BanService _banService;
    private readonly MuteService _muteService;
    private readonly MatchService _matchService;
    private readonly PluginConfig _config;
    private BasePlugin? _plugin;

    public PlayerConnectionHandler(BanService banService, MuteService muteService, MatchService matchService, PluginConfig config)
    {
        _banService = banService;
        _muteService = muteService;
        _matchService = matchService;
        _config = config;
    }

    public void SetPlugin(BasePlugin plugin)
    {
        _plugin = plugin;
    }

    public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        // Check for active ban
        var ban = _banService.GetActiveBan(player.SteamID);
        if (ban != null)
        {
            var durationText = ban.IsPermanent ? "permanently" : $"until {ban.ExpiresAt:g}";
            var reason = string.IsNullOrEmpty(ban.Reason) ? "No reason provided" : ban.Reason;

            Server.NextFrame(() =>
            {
                if (player.IsValid)
                {
                    Server.ExecuteCommand($"kickid {player.UserId} \"You are banned {durationText}. Reason: {reason}\"");
                }
            });

            return HookResult.Continue;
        }

        // Load mute status
        _muteService.LoadMutesForPlayer(player.SteamID);

        // Announce player join to all
        if (_config.AnnouncePlayerJoin)
        {
            var joinMessage = _config.PlayerJoinMessage
                .Replace("{player}", player.PlayerName)
                .Replace("{steamid}", player.SteamID.ToString());

            Server.NextFrame(() =>
            {
                Server.PrintToChatAll($"{_config.ChatPrefix} {joinMessage}");
            });
        }

        // Send welcome message to the player
        if (_config.EnableWelcomeMessage && _plugin != null)
        {
            var playerName = player.PlayerName;
            var steamId = player.SteamID;

            _plugin.AddTimer(_config.WelcomeMessageDelay, () =>
            {
                // Find player again after delay (they might have disconnected)
                var targetPlayer = Utilities.GetPlayerFromSteamId(steamId);
                if (targetPlayer != null && targetPlayer.IsValid)
                {
                    var welcomeMessage = _config.WelcomeMessage
                        .Replace("{player}", playerName)
                        .Replace("{steamid}", steamId.ToString());

                    targetPlayer.PrintToChat($"{_config.ChatPrefix} {welcomeMessage}");

                    // Show warmup message if in warmup mode
                    if (_matchService.IsWarmup)
                    {
                        targetPlayer.PrintToChat($"{_config.ChatPrefix} {_config.WarmupMessage}");
                    }
                }
            });
        }

        return HookResult.Continue;
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        // Clean up session mute
        _muteService.RemoveSessionMute(player.SteamID);

        return HookResult.Continue;
    }
}
