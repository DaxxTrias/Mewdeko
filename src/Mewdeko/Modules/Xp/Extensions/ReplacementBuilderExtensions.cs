using Humanizer;

namespace Mewdeko.Modules.Xp.Extensions;

/// <summary>
///     Extensions for ReplacementBuilder to add XP-specific placeholders.
/// </summary>
public static class ReplacementBuilderExtensions
{
    /// <summary>
    ///     Adds XP-specific placeholders to the replacement builder.
    /// </summary>
    /// <param name="builder">The replacement builder.</param>
    /// <param name="user">The user who leveled up.</param>
    /// <param name="guild">The guild where the level up occurred.</param>
    /// <param name="channel">The channel where XP was gained (optional).</param>
    /// <param name="oldLevel">The user's previous level.</param>
    /// <param name="newLevel">The user's new level.</param>
    /// <param name="totalXp">The user's total XP.</param>
    /// <param name="currentLevelXp">XP earned in the current level.</param>
    /// <param name="nextLevelXp">XP required for the next level.</param>
    /// <param name="xpGained">Amount of XP gained in this session.</param>
    /// <param name="rank">The user's current rank in the guild.</param>
    /// <param name="triggerUser">The user who triggered the notification (for mentions).</param>
    /// <param name="pingsDisabled">Whether the user has pings disabled.</param>
    /// <returns>The ReplacementBuilder instance with XP placeholders added.</returns>
    public static ReplacementBuilder WithXpPlaceholders(
        this ReplacementBuilder builder,
        IGuildUser user,
        IGuild guild,
        ITextChannel? channel,
        int oldLevel,
        int newLevel,
        int totalXp,
        int currentLevelXp,
        int nextLevelXp,
        int xpGained,
        int rank,
        IUser? triggerUser = null,
        bool pingsDisabled = false)
    {
        // User-related placeholders
        builder.WithOverride("%xp.user%",
            () => pingsDisabled
                ? user.Username.EscapeWeirdStuff()
                : user.ToString()?.EscapeWeirdStuff() ?? "Unknown User");
        builder.WithOverride("%xp.user.mention%",
            () => pingsDisabled ? user.Username.EscapeWeirdStuff() : user.Mention);
        builder.WithOverride("%xp.user.name%", () => user.Username.EscapeWeirdStuff());
        builder.WithOverride("%xp.user.displayname%", () => user.DisplayName.EscapeWeirdStuff());
        builder.WithOverride("%xp.user.nickname%",
            () => user.Nickname?.EscapeWeirdStuff() ?? user.Username.EscapeWeirdStuff());
        builder.WithOverride("%xp.user.avatar%", () => user.GetAvatarUrl(size: 2048) ?? user.GetDefaultAvatarUrl());
        builder.WithOverride("%xp.user.id%", () => user.Id.ToString());
        builder.WithOverride("%xp.user.created%", () => user.CreatedAt.ToString("dd/MM/yyyy"));
        builder.WithOverride("%xp.user.joined%", () => user.JoinedAt?.ToString("dd/MM/yyyy") ?? "Unknown");

        // Level and XP placeholders
        builder.WithOverride("%xp.level.old%", oldLevel.ToString);
        builder.WithOverride("%xp.level.new%", newLevel.ToString);
        builder.WithOverride("%xp.level.current%", () => newLevel.ToString()); // Alias for new level
        builder.WithOverride("%xp.level.next%", () => (newLevel + 1).ToString());
        builder.WithOverride("%xp.level.difference%", () => (newLevel - oldLevel).ToString());

        builder.WithOverride("%xp.total%", totalXp.ToString);
        builder.WithOverride("%xp.current%", currentLevelXp.ToString);
        builder.WithOverride("%xp.needed%", nextLevelXp.ToString);
        builder.WithOverride("%xp.remaining%", () => Math.Max(0, nextLevelXp - currentLevelXp).ToString());
        builder.WithOverride("%xp.gained%", xpGained.ToString);
        builder.WithOverride("%xp.progress%",
            () => nextLevelXp > 0 ? $"{currentLevelXp * 100.0 / nextLevelXp:F1}%" : "100%");

        // Rank placeholders
        builder.WithOverride("%xp.rank%", rank.ToString);
        builder.WithOverride("%xp.rank.ordinal%", () => rank.ToOrdinalWords());
        builder.WithOverride("%xp.rank.suffix%", () => GetOrdinalSuffix(rank));

        // Guild/Server placeholders
        builder.WithOverride("%xp.guild%", () => guild.Name.EscapeWeirdStuff());
        builder.WithOverride("%xp.guild.name%", () => guild.Name.EscapeWeirdStuff());
        builder.WithOverride("%xp.guild.id%", () => guild.Id.ToString());
        builder.WithOverride("%xp.guild.membercount%", () => guild.ApproximateMemberCount.ToString());
        builder.WithOverride("%xp.guild.icon%", () => guild.IconUrl ?? "");
        builder.WithOverride("%xp.guild.banner%", () => guild.BannerUrl ?? "");

        // Channel placeholders (if provided)
        if (channel != null)
        {
            builder.WithOverride("%xp.channel%", () => channel.Mention);
            builder.WithOverride("%xp.channel.mention%", () => channel.Mention);
            builder.WithOverride("%xp.channel.name%", () => channel.Name.EscapeWeirdStuff());
            builder.WithOverride("%xp.channel.id%", () => channel.Id.ToString());
        }
        else
        {
            builder.WithOverride("%xp.channel%", () => "Unknown Channel");
            builder.WithOverride("%xp.channel.mention%", () => "Unknown Channel");
            builder.WithOverride("%xp.channel.name%", () => "Unknown Channel");
            builder.WithOverride("%xp.channel.id%", () => "0");
        }

        // Trigger user placeholders (if provided)
        if (triggerUser != null)
        {
            builder.WithOverride("%xp.triggeruser%",
                () => triggerUser.ToString()?.EscapeWeirdStuff() ?? "Unknown User");
            builder.WithOverride("%xp.triggeruser.mention%", () => triggerUser.Mention);
            builder.WithOverride("%xp.triggeruser.name%", () => triggerUser.Username.EscapeWeirdStuff());
            builder.WithOverride("%xp.triggeruser.avatar%", () => triggerUser.RealAvatarUrl().ToString());
            builder.WithOverride("%xp.triggeruser.id%", () => triggerUser.Id.ToString());
        }
        else
        {
            builder.WithOverride("%xp.triggeruser%", () => "System");
            builder.WithOverride("%xp.triggeruser.mention%", () => "System");
            builder.WithOverride("%xp.triggeruser.name%", () => "System");
            builder.WithOverride("%xp.triggeruser.avatar%", () => "");
            builder.WithOverride("%xp.triggeruser.id%", () => "0");
        }

        // Time placeholders
        builder.WithOverride("%xp.time%", () => DateTime.UtcNow.ToString("HH:mm"));
        builder.WithOverride("%xp.time.full%", () => DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm"));
        builder.WithOverride("%xp.date%", () => DateTime.UtcNow.ToString("dd/MM/yyyy"));
        builder.WithOverride("%xp.timestamp%", () => TimestampTag.FromDateTime(DateTime.UtcNow).ToString());
        builder.WithOverride("%xp.timestamp.relative%",
            () => TimestampTag.FromDateTime(DateTime.UtcNow, TimestampTagStyles.Relative).ToString());

        return builder;
    }

    /// <summary>
    ///     Sanitizes mentions in a message for users who have pings disabled.
    ///     Only filters mentions for the specific user, leaving other mentions intact.
    /// </summary>
    /// <param name="message">The message to sanitize.</param>
    /// <param name="user">The user to replace mentions for.</param>
    /// <param name="pingsDisabled">Whether pings are disabled for the user.</param>
    /// <returns>The sanitized message.</returns>
    public static string SanitizeMentionsForUser(string message, IGuildUser user, bool pingsDisabled)
    {
        if (!pingsDisabled)
            return message;

        var username = user.Username.EscapeWeirdStuff();
        var userId = user.Id.ToString();

        // Replace various mention formats with username in plain text
        message = message
            .Replace(user.Mention, username)
            .Replace($"<@{user.Id}>", username)
            .Replace($"<@!{user.Id}>", username);

        // Handle bypass attempts - but be more specific to avoid false positives
        // Only replace if it's clearly trying to mention this specific user
        message = message
            .Replace($"@{user.Username}", username)
            .Replace($"@{user.DisplayName}", username);

        if (user.Nickname != null)
        {
            message = message.Replace($"@{user.Nickname}", username);
        }

        // Handle placeholder-based mentions in the raw message
        // This catches cases where placeholders expand to mentions
        message = message
            .Replace($"<@{userId}>", username)
            .Replace($"<@!{userId}>", username);

        return message;
    }

    /// <summary>
    ///     Processes a level-up message with smart mention filtering.
    ///     Filters mentions only for the user who has pings disabled, preserving other mentions.
    /// </summary>
    /// <param name="message">The raw message template.</param>
    /// <param name="replacer">The built replacer with all placeholders.</param>
    /// <param name="user">The user who leveled up.</param>
    /// <param name="pingsDisabled">Whether the user has pings disabled.</param>
    /// <returns>The processed message with appropriate mention filtering.</returns>
    public static string ProcessLevelUpMessage(string message, Replacer replacer, IGuildUser user, bool pingsDisabled)
    {
        // First, apply all placeholder replacements
        var processedMessage = replacer.Replace(message);

        // Then, if the user has pings disabled, sanitize only their mentions
        if (pingsDisabled)
        {
            processedMessage = SanitizeMentionsForUser(processedMessage, user, true);
        }

        return processedMessage;
    }

    /// <summary>
    ///     Gets the ordinal suffix for a number (1st, 2nd, 3rd, etc.).
    /// </summary>
    /// <param name="number">The number to get the suffix for.</param>
    /// <returns>The ordinal suffix.</returns>
    private static string GetOrdinalSuffix(int number)
    {
        if (number <= 0) return "";

        return (number % 100) switch
        {
            11 or 12 or 13 => "th",
            _ => (number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            }
        };
    }
}