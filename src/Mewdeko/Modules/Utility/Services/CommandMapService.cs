using DataModel;
using LinqToDB;
using Mewdeko.Common.ModuleBehaviors;


namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Manages the transformation of input commands based on alias mappings, allowing customization of command triggers.
/// </summary>
public class CommandMapService(IDataConnectionFactory dbFactory, GuildSettingsService gss) : IInputTransformer, INService
{
    /// <summary>
    ///     Transforms an input command based on alias mappings for the specific guild.
    /// </summary>
    /// <param name="guild">The guild where the command was issued.</param>
    /// <param name="channel">The channel where the command was issued.</param>
    /// <param name="user">The user who issued the command.</param>
    /// <param name="input">The original command input.</param>
    /// <returns>The transformed command input if an alias is matched; otherwise, the original input.</returns>
    public async Task<string> TransformInput(IGuild? guild, IMessageChannel channel, IUser user, string input)
    {
        await Task.Yield();

        if (guild == null || string.IsNullOrWhiteSpace(input))
            return input;

        var aliases = await GetCommandMap(guild.Id);

        // ReSharper disable once HeuristicUnreachableCode
        if (guild == null) return input;
        if (aliases is null || aliases.Count == 0) return input;
        var keys = aliases.Keys
            .OrderByDescending(x => x.Length);

        foreach (var k in keys)
        {
            string newInput;
            if (input.StartsWith($"{k} ", StringComparison.InvariantCultureIgnoreCase))
                newInput = string.Concat(aliases[k], input.AsSpan(k.Length, input.Length - k.Length));
            else if (input.Equals(k, StringComparison.InvariantCultureIgnoreCase))
                newInput = aliases[k];
            else
                continue;
            return newInput;
        }

        return input;
    }

    /// <summary>
    ///     Gets the command map for the specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to get the command map.</param>
    /// <returns>A dictionary of command aliases and their mappings.</returns>
    public async Task<Dictionary<string, string>?> GetCommandMap(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var commandAliases = await dbContext.CommandAliases
            .Where(ca => ca.GuildId == guildId)
            .ToListAsync();

        return commandAliases?.Distinct(new CommandAliasEqualityComparer())
            .ToDictionary(ca => ca.Trigger, ca => ca.Mapping);
    }


    /// <summary>
    ///     Clears all command aliases for a specified guild.
    /// </summary>
    /// <param name="guildId">The ID of the guild for which to clear aliases.</param>
    /// <returns>The number of aliases cleared.</returns>
    public async Task<int> ClearAliases(ulong guildId)
    {
        await using var dbContext = await dbFactory.CreateConnectionAsync();

        // Direct query with GuildId filter
        var commandAliases = await dbContext.CommandAliases
            .Where(ca => ca.GuildId == guildId)
            .ToListAsync();

        if (commandAliases.Count == 0)
            return 0;

        // Remove all aliases
        await dbContext.DeleteAsync(commandAliases);

        return commandAliases.Count;
    }
}

/// <summary>
///     This class provides a way to compare two CommandAlias objects.
///     It implements the IEqualityComparer interface which defines methods to support the comparison of objects for
///     equality.
/// </summary>
public class CommandAliasEqualityComparer : IEqualityComparer<CommandAlias>
{
    /// <summary>
    ///     Determines whether the specified CommandAlias objects are equal.
    /// </summary>
    /// <param name="x">The first CommandAlias object to compare.</param>
    /// <param name="y">The second CommandAlias object to compare.</param>
    /// <returns>true if the specified CommandAlias objects are equal; otherwise, false.</returns>
    public bool Equals(CommandAlias? x, CommandAlias? y)
    {
        return x?.Trigger == y?.Trigger;
    }

    /// <summary>
    ///     Returns a hash code for the specified CommandAlias object.
    /// </summary>
    /// <param name="obj">The CommandAlias object for which a hash code is to be returned.</param>
    /// <returns>A hash code for the specified CommandAlias object.</returns>
    public int GetHashCode(CommandAlias obj)
    {
        return obj.Trigger.GetHashCode(StringComparison.InvariantCulture);
    }
}