using System.Collections.Immutable;

namespace Mewdeko.Services;

public interface IBotCredentials
{
    string Token { get; }
    string ClientSecret { get; }
    string GoogleApiKey { get; }
    ImmutableArray<ulong> OwnerIds { get; }
    public string GeniusKey { get; }
    string StatcordKey { get; }
    ImmutableArray<ulong> OfficialMods { get; }
    string MashapeKey { get; }
    string SpotifyClientId { get; }
    string SpotifyClientSecret { get; }
    string OsuApiKey { get; }
    string ShardRunPort { get; }
    string ChatSavePath { get; }
    int TotalShards { get; }
    string TwitchClientId { get; }
    string TwitchClientSecret { get; }
    string TrovoClientId { get; }
    string CleverbotApiKey { get; }
    RestartConfig RestartCommand { get; }
    string VotesToken { get; }
    string BotListToken { get; }
    string RedisOptions { get; }
    string LocationIqApiKey { get; }
    string TimezoneDbApiKey { get; }
    ulong ConfessionReportChannelId { get; }
    public string CfClearance { get; }
    public string UserAgent { get; }
    public string CsrfToken { get; }

    bool IsOwner(IUser u);
    bool IsOfficialMod(IUser u);
}

public class RestartConfig
{
    public RestartConfig(string cmd, string args)
    {
        Cmd = cmd;
        Args = args;
    }

    public string Cmd { get; }
    public string Args { get; }
}

public class DbConfig
{
    public DbConfig(string type, string connectionString)
    {
        Type = type;
        ConnectionString = connectionString;
    }

    public string Type { get; }
    public string ConnectionString { get; }
}