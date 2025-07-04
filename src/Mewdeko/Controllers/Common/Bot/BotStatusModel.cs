namespace Mewdeko.Controllers.Common.Bot;

/// <summary>
///     The model defining bot status info
/// </summary>
public class BotStatusModel
{
    /// <summary>
    ///     The version of the bot
    /// </summary>
    public string BotVersion { get; set; }

    /// <summary>
    ///     The current commit hash of the bot
    /// </summary>
    public string CommitHash { get; set; }

    /// <summary>
    ///     The latency to discord
    /// </summary>
    public int BotLatency { get; set; }

    /// <summary>
    ///     The name of the bot
    /// </summary>
    public string BotName { get; set; }

    /// <summary>
    ///     The bots avatar.
    /// </summary>
    public string BotAvatar { get; set; }

    /// <summary>
    ///     The bots banner, if any
    /// </summary>
    public string BotBanner { get; set; }

    /// <summary>
    ///     The number of commands
    /// </summary>
    public int CommandsCount { get; set; }

    /// <summary>
    ///     The number of modules
    /// </summary>
    public int ModulesCount { get; set; }

    /// <summary>
    ///     The version of Discord.Net the bot is using
    /// </summary>
    public string DNetVersion { get; set; }

    /// <summary>
    ///     The bots current status (idle, afk, etc)
    /// </summary>
    public string BotStatus { get; set; }

    /// <summary>
    ///     The number of users in every guild (separated by distinct)
    /// </summary>
    public int UserCount { get; set; }

    /// <summary>
    ///     The bots userId
    /// </summary>
    public ulong BotId { get; set; }

    /// <summary>
    ///     The api url of this instance.
    /// </summary>
    public string InstanceUrl { get; set; }
}