using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace CS2Admin.Config;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("DatabasePath")]
    public string DatabasePath { get; set; } = "data/cs2admin.db";

    [JsonPropertyName("VoteThresholdPercent")]
    public int VoteThresholdPercent { get; set; } = 60;

    [JsonPropertyName("VoteDurationSeconds")]
    public int VoteDurationSeconds { get; set; } = 30;

    [JsonPropertyName("VoteCooldownSeconds")]
    public int VoteCooldownSeconds { get; set; } = 60;

    [JsonPropertyName("MinimumVotersRequired")]
    public int MinimumVotersRequired { get; set; } = 3;

    [JsonPropertyName("DefaultBanReason")]
    public string DefaultBanReason { get; set; } = "Banned by admin";

    [JsonPropertyName("DefaultKickReason")]
    public string DefaultKickReason { get; set; } = "Kicked by admin";

    [JsonPropertyName("DefaultMuteReason")]
    public string DefaultMuteReason { get; set; } = "Muted by admin";

    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = "[CS2Admin]";

    [JsonPropertyName("EnableLogging")]
    public bool EnableLogging { get; set; } = true;

    [JsonPropertyName("EnableWelcomeMessage")]
    public bool EnableWelcomeMessage { get; set; } = true;

    [JsonPropertyName("WelcomeMessage")]
    public string WelcomeMessage { get; set; } = "Welcome to the server, {player}!";

    [JsonPropertyName("WelcomeMessageDelay")]
    public float WelcomeMessageDelay { get; set; } = 3.0f;

    [JsonPropertyName("AnnouncePlayerJoin")]
    public bool AnnouncePlayerJoin { get; set; } = true;

    [JsonPropertyName("PlayerJoinMessage")]
    public string PlayerJoinMessage { get; set; } = "{player} joined the server.";

    [JsonPropertyName("EnableWarmupMode")]
    public bool EnableWarmupMode { get; set; } = true;

    [JsonPropertyName("WarmupMoney")]
    public int WarmupMoney { get; set; } = 60000;

    [JsonPropertyName("WarmupMessage")]
    public string WarmupMessage { get; set; } = "Server is in warmup. Waiting for admin to start the match.";

    [JsonPropertyName("MatchStartMessage")]
    public string MatchStartMessage { get; set; } = "Match starting! Good luck, have fun!";

    [JsonPropertyName("MinPlayersToStart")]
    public int MinPlayersToStart { get; set; } = 2;

    [JsonPropertyName("EnableKnifeRound")]
    public bool EnableKnifeRound { get; set; } = true;

    [JsonPropertyName("KnifeRoundMessage")]
    public string KnifeRoundMessage { get; set; } = "Knife round! Winner chooses side.";

    [JsonPropertyName("KnifeRoundWinnerMessage")]
    public string KnifeRoundWinnerMessage { get; set; } = "{team} won the knife round! Type !stay or !switch to choose side.";

    // GOTV Settings
    [JsonPropertyName("EnableGOTV")]
    public bool EnableGOTV { get; set; } = false;

    [JsonPropertyName("GOTVPort")]
    public int GOTVPort { get; set; } = 27020;

    [JsonPropertyName("GOTVMaxClients")]
    public int GOTVMaxClients { get; set; } = 10;

    [JsonPropertyName("GOTVName")]
    public string GOTVName { get; set; } = "CS2Admin GOTV";

    [JsonPropertyName("GOTVDelay")]
    public int GOTVDelay { get; set; } = 30;

    [JsonPropertyName("GOTVAutoRecord")]
    public bool GOTVAutoRecord { get; set; } = false;

    // Vote Maps
    [JsonPropertyName("VoteMaps")]
    public List<string> VoteMaps { get; set; } = new()
    {
        "de_mirage",
        "de_ancient",
        "de_dust2",
        "de_inferno",
        "de_nuke",
        "de_anubis",
        "de_overpass"
    };
}
