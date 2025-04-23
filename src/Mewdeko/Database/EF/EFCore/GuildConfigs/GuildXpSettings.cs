using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mewdeko.Database.EF.EFCore.Base;
using Mewdeko.Database.EF.EFCore.Enums;

namespace Mewdeko.Database.EF.EFCore.GuildConfigs;

/// <summary>
///     Represents XP configuration settings for a guild.
/// </summary>
[Table("GuildXpSettings")]
public class GuildXpSettings : DbEntity
{
    /// <summary>
    ///     Gets or sets the guild ID.
    /// </summary>
    [Required]
    public ulong GuildId { get; set; }

    /// <summary>
    ///     Gets or sets the XP rate multiplier for the guild.
    /// </summary>
    [Required]
    public double XpMultiplier { get; set; } = 1.0;

    /// <summary>
    ///     Gets or sets the base XP per message.
    /// </summary>
    [Required]
    public int XpPerMessage { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the message XP timeout in seconds.
    /// </summary>
    [Required]
    public int MessageXpCooldown { get; set; } = 60;

    /// <summary>
    ///     Gets or sets the voice XP rate per minute.
    /// </summary>
    [Required]
    public int VoiceXpPerMinute { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the voice XP timeout in minutes.
    /// </summary>
    [Required]
    public int VoiceXpTimeout { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the daily first message bonus XP.
    /// </summary>
    [Required]
    public int FirstMessageBonus { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the XP curve type.
    /// </summary>
    [Required]
    public XpCurveType XpCurveType { get; set; } = XpCurveType.Standard;

    /// <summary>
    ///     Gets or sets whether the entire guild is excluded from XP gain.
    /// </summary>
    [Required]
    public bool XpGainDisabled { get; set; }

    /// <summary>
    ///     Gets or sets the custom XP level-up image URL.
    /// </summary>
    public string CustomXpImageUrl { get; set; } = "";

    /// <summary>
    ///     Gets or sets the level-up notification message template.
    /// </summary>
    public string LevelUpMessage { get; set; } = "{UserMention} has reached level {Level}!";

    /// <summary>
    ///     Gets or sets a value indicating whether to use role exclusive rewards.
    /// </summary>
    [Required]
    public bool ExclusiveRoleRewards { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether XP decay is enabled.
    /// </summary>
    [Required]
    public bool EnableXpDecay { get; set; } = false;

    /// <summary>
    ///     Gets or sets the days of inactivity before XP decay starts.
    /// </summary>
    [Required]
    public int InactivityDaysBeforeDecay { get; set; } = 30;

    /// <summary>
    ///     Gets or sets the daily XP decay percentage.
    /// </summary>
    [Required]
    public double DailyDecayPercentage { get; set; } = 0.5;
}