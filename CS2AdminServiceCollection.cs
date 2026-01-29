using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CS2Admin.Commands;
using CS2Admin.Config;
using CS2Admin.Database;
using CS2Admin.Handlers;
using CS2Admin.Models;
using CS2Admin.Services;

namespace CS2Admin;

public class CS2AdminServiceCollection : IDisposable
{
    public PluginConfig Config { get; }
    public DatabaseService Database { get; }
    public BanService BanService { get; }
    public MuteService MuteService { get; }
    public PlayerService PlayerService { get; }
    public MatchService MatchService { get; }
    public VoteService VoteService { get; }
    public AdminService AdminService { get; }
    public AdminCommands AdminCommands { get; }
    public VoteCommands VoteCommands { get; }
    public AdminManagementCommands AdminManagementCommands { get; }
    public ChatCommandHandler ChatCommandHandler { get; }
    public PlayerConnectionHandler PlayerConnectionHandler { get; }

    private bool _disposed;

    public CS2AdminServiceCollection(PluginConfig config, string moduleDirectory, BasePlugin plugin)
    {
        Config = config;

        var dbPath = Path.IsPathRooted(config.DatabasePath)
            ? config.DatabasePath
            : Path.Combine(moduleDirectory, config.DatabasePath);

        Database = new DatabaseService(dbPath);
        BanService = new BanService(Database, config.EnableLogging);
        MuteService = new MuteService(Database, config.EnableLogging);
        PlayerService = new PlayerService(Database, config.EnableLogging);
        MatchService = new MatchService(Database, config.EnableLogging, config.WarmupMoney);
        MatchService.SetPlugin(plugin);

        VoteService = new VoteService(
            plugin,
            config.VoteThresholdPercent,
            config.VoteDurationSeconds,
            config.VoteCooldownSeconds,
            config.MinimumVotersRequired,
            BroadcastMessage,
            OnVotePassed
        );

        AdminService = new AdminService(Database, config.EnableLogging);

        AdminCommands = new AdminCommands(config, PlayerService, BanService, MuteService, MatchService);
        VoteCommands = new VoteCommands(config, VoteService);
        AdminManagementCommands = new AdminManagementCommands(config, AdminService);
        ChatCommandHandler = new ChatCommandHandler(plugin, config, PlayerService, BanService, MuteService, MatchService, VoteService, AdminManagementCommands, AdminService);
        PlayerConnectionHandler = new PlayerConnectionHandler(BanService, MuteService, MatchService, config);
    }

    private void BroadcastMessage(string message)
    {
        Server.PrintToChatAll($"{Config.ChatPrefix} {message}");
    }

    private void OnVotePassed(Vote vote)
    {
        switch (vote.Type)
        {
            case VoteType.Kick:
                if (vote.TargetPlayer?.IsValid == true)
                {
                    PlayerService.KickPlayer(vote.TargetPlayer, null, "Kicked by vote");
                }
                break;

            case VoteType.Pause:
                MatchService.PauseMatch();
                break;

            case VoteType.Restart:
                MatchService.RestartMatch();
                break;

            case VoteType.ChangeMap:
                if (!string.IsNullOrEmpty(vote.TargetMap))
                {
                    MatchService.ChangeMap(vote.TargetMap);
                }
                break;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Database.Dispose();
        }

        _disposed = true;
    }
}
