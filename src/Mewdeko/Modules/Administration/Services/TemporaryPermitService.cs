using System.Collections.Concurrent;

namespace Mewdeko.Modules.Administration.Services;

/// <summary>
///     In-memory store of temporary user permits to bypass command permission checks for a limited time.
/// </summary>
public sealed class TemporaryPermitService : INService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(ulong GuildId, ulong UserId, string CommandKey), DateTimeOffset> permits =
        new();

    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///     Grants a temporary permit for a user to execute a command until the provided expiry.
    /// </summary>
    public void Grant(ulong guildId, ulong userId, IEnumerable<string> commandKeys, TimeSpan duration)
    {
        var expiry = DateTimeOffset.UtcNow.Add(duration);
        foreach (var rawKey in commandKeys)
        {
            var key = NormalizeKey(rawKey);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            permits[(guildId, userId, key)] = expiry;
        }
    }

    /// <summary>
    ///     Revokes a previously granted permit.
    /// </summary>
    public void Revoke(ulong guildId, ulong userId, IEnumerable<string> commandKeys)
    {
        foreach (var rawKey in commandKeys)
        {
            var key = NormalizeKey(rawKey);
            permits.TryRemove((guildId, userId, key), out _);
        }
    }

    /// <summary>
    ///     Checks whether the user currently has an active permit for the specified command key.
    /// </summary>
    public bool IsPermitted(ulong guildId, ulong userId, string commandKey)
    {
        var key = NormalizeKey(commandKey);
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!permits.TryGetValue((guildId, userId, key), out var expiry))
            return false;

        if (expiry <= DateTimeOffset.UtcNow)
        {
            permits.TryRemove((guildId, userId, key), out _);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Maps user-friendly names/aliases to normalized precondition command keys.
    ///     For interactions we use method names; for text commands we use command names – all lower case.
    /// </summary>
    public static IEnumerable<string> NormalizeKeysForGrant(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            yield break;

        var key = NormalizeKey(input);

        // Special cases for known commands where method names differ from visible labels
        // Emote Stealer message command method name is "Steal" in Server_Management.EmoteStealer
        if (KeyComparer.Equals(key, "emotestealer") ||
            KeyComparer.Equals(key, "emote-stealer") ||
            KeyComparer.Equals(key, "stealemotes") ||
            KeyComparer.Equals(key, "stealemote") ||
            KeyComparer.Equals(key, "steal_emotes") ||
            KeyComparer.Equals(key, "steal"))
        {
            yield return "steal"; // interaction method name
            yield break;
        }

        yield return key;
    }

    private static string NormalizeKey(string key)
        => key?.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty)
               .ToLowerInvariant() ?? string.Empty;
}


