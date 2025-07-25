﻿namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     Service for managing guild timezones.
/// </summary>
public class GuildTimezoneService : INService
{
    private readonly GuildSettingsService gss;

    /// <summary>
    ///     Constructs a new instance of the GuildTimezoneService.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database service.</param>
    /// <param name="gss">The guild config service.</param>
    public GuildTimezoneService(DiscordShardedClient client, IDataConnectionFactory dbFactory,
        GuildSettingsService gss)
    {
        var curUser = client.CurrentUser;
        if (curUser != null)
            AllServices.TryAdd(curUser.Id, this);
        this.gss = gss;
    }

    /// <summary>
    ///     A dictionary of all GuildTimezoneService instances.
    /// </summary>
    public static ConcurrentDictionary<ulong, GuildTimezoneService> AllServices { get; } = new();

    /// <summary>
    ///     Gets the timezone for a guild, or null if no timezone is set.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The timezone for the guild, or null if no timezone is set.</returns>
    public TimeZoneInfo? GetTimeZoneOrDefault(ulong guildId)
    {
        var config = gss.GetGuildConfig(guildId).GetAwaiter().GetResult();

        if (config?.TimeZoneId == null)
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(config.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Sets the timezone for a guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="tz">The timezone to set.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetTimeZone(ulong guildId, TimeZoneInfo? tz)
    {
        var gc = await gss.GetGuildConfig(guildId);

        if (gc == null)
        {
            return;
        }

        gc.TimeZoneId = tz?.Id;
        await gss.UpdateGuildConfig(guildId, gc);
    }

    /// <summary>
    ///     Gets the timezone for a guild, or UTC if no timezone is set.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>The timezone for the guild, or UTC if no timezone is set.</returns>
    public TimeZoneInfo? GetTimeZoneOrUtc(ulong guildId)
    {
        return GetTimeZoneOrDefault(guildId) ?? TimeZoneInfo.Utc;
    }
}