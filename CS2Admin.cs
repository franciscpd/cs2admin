using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using Microsoft.Extensions.Logging;

namespace CS2Admin;

public class CS2Admin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "CS2Admin";
    public override string ModuleVersion => "0.1.0";
    public override string ModuleAuthor => "CS2Admin Team";
    public override string ModuleDescription => "Server administration plugin for Counter-Strike 2";

    public PluginConfig Config { get; set; } = new();

    private CS2AdminServiceCollection? _services;
    private bool _warmupStartedOnMapLoad;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _services = new CS2AdminServiceCollection(Config, ModuleDirectory);
        _services.PlayerConnectionHandler.SetPlugin(this);

        // Register console commands
        _services.AdminCommands.RegisterCommands(this);
        _services.VoteCommands.RegisterCommands(this);
        _services.AdminManagementCommands.RegisterCommands(this);

        // Register chat command listener for . prefix
        AddCommandListener("say", OnPlayerSay);
        AddCommandListener("say_team", OnPlayerSay);

        // Register event handlers
        RegisterEventHandler<EventPlayerConnectFull>(_services.PlayerConnectionHandler.OnPlayerConnect);
        RegisterEventHandler<EventPlayerDisconnect>(_services.PlayerConnectionHandler.OnPlayerDisconnect);

        // Register map/round events for warmup
        if (Config.EnableWarmupMode)
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
        }

        // Load admins from database
        _services.AdminService.LoadAdminsToGame();

        // If hot reload, start warmup if enabled
        if (hotReload && Config.EnableWarmupMode)
        {
            _services.MatchService.StartWarmup();
            Server.PrintToChatAll($"{Config.ChatPrefix} {Config.WarmupMessage}");
        }

        Logger.LogInformation("CS2Admin loaded successfully!");
    }

    public override void Unload(bool hotReload)
    {
        _services?.Dispose();
        _services = null;

        Logger.LogInformation("CS2Admin unloaded.");
    }

    private void OnMapStart(string mapName)
    {
        _warmupStartedOnMapLoad = false;

        if (_services == null || !Config.EnableWarmupMode) return;

        // Delay warmup start to ensure server is ready
        AddTimer(2.0f, () =>
        {
            if (_services != null && !_warmupStartedOnMapLoad)
            {
                _warmupStartedOnMapLoad = true;
                _services.MatchService.StartWarmup();
                Server.PrintToChatAll($"{Config.ChatPrefix} {Config.WarmupMessage}");
                Logger.LogInformation($"Warmup started on map: {mapName}");
            }
        });
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_services == null) return HookResult.Continue;

        // Show warmup message at round start if in warmup mode
        if (_services.MatchService.IsWarmup)
        {
            AddTimer(1.0f, () =>
            {
                Server.PrintToChatAll($"{Config.ChatPrefix} {Config.WarmupMessage}");
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSay(CCSPlayerController? player, CommandInfo info)
    {
        if (_services == null) return HookResult.Continue;
        return _services.ChatCommandHandler.OnPlayerChat(player, info);
    }
}
