using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2Admin.Config;
using Microsoft.Extensions.Logging;

namespace CS2Admin;

public class CS2Admin : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "CS2Admin";
    public override string ModuleVersion => "0.3.3";
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
        _services = new CS2AdminServiceCollection(Config, ModuleDirectory, this);
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
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);

        // Register map/round events for warmup and knife round
        if (Config.EnableWarmupMode)
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
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

        // Configure GOTV if enabled
        if (Config.EnableGOTV)
        {
            ConfigureGOTV();
        }

        Logger.LogInformation("CS2Admin loaded successfully!");
    }

    public override void Unload(bool hotReload)
    {
        _services?.Dispose();
        _services = null;

        Logger.LogInformation("CS2Admin unloaded.");
    }

    private void ConfigureGOTV()
    {
        Server.ExecuteCommand($"tv_enable 1");
        Server.ExecuteCommand($"tv_port {Config.GOTVPort}");
        Server.ExecuteCommand($"tv_maxclients {Config.GOTVMaxClients}");
        Server.ExecuteCommand($"tv_name \"{Config.GOTVName}\"");
        Server.ExecuteCommand($"tv_delay {Config.GOTVDelay}");

        if (Config.GOTVAutoRecord)
        {
            Server.ExecuteCommand("tv_autorecord 1");
        }

        Logger.LogInformation($"GOTV configured on port {Config.GOTVPort} with {Config.GOTVMaxClients} max clients");
    }

    private void OnMapStart(string mapName)
    {
        _warmupStartedOnMapLoad = false;

        if (_services == null || !Config.EnableWarmupMode) return;

        // Reset knife round state
        _services.MatchService.ResetKnifeRoundState();

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

        // Show knife round message and enforce knife-only settings
        if (_services.MatchService.IsKnifeRound)
        {
            Server.PrintToChatAll($"{Config.ChatPrefix} {Config.KnifeRoundMessage}");
            Server.ExecuteCommand("mp_buy_allow_guns 0");  // Reinforce after restart
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        if (_services == null) return HookResult.Continue;

        // Handle knife round end
        if (_services.MatchService.IsKnifeRound)
        {
            var winnerTeam = @event.Winner; // 2 = T, 3 = CT
            _services.MatchService.EndKnifeRound(winnerTeam);

            var teamName = winnerTeam == 2 ? "Terrorists" : "Counter-Terrorists";
            var message = Config.KnifeRoundWinnerMessage.Replace("{team}", teamName);
            Server.PrintToChatAll($"{Config.ChatPrefix} {message}");
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        if (_services == null) return HookResult.Continue;

        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
            return HookResult.Continue;

        if (_services.MatchService.IsWarmup)
        {
            AddTimer(0.1f, () =>
            {
                if (player.IsValid && player.PawnIsAlive)
                {
                    _services.MatchService.GiveMoneyToPlayer(player);
                }
            });
        }

        // Strip weapons during knife only mode
        if (_services.MatchService.IsKnifeOnly)
        {
            AddTimer(0.1f, () =>
            {
                if (player.IsValid && player.PawnIsAlive)
                {
                    _services.MatchService.StripPlayerWeapons(player);
                }
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
