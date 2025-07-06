namespace Mewdeko.Modules.Xp.Models;

/// <summary>
///     Defines the type of XP competition.
/// </summary>
public enum XpCompetitionType
{
    /// <summary>
    ///     Competition based on who gains the most XP during the competition period.
    /// </summary>
    MostGained,

    /// <summary>
    ///     Competition based on who reaches a specific target level first.
    /// </summary>
    ReachLevel,

    /// <summary>
    ///     Competition based on who has the highest total XP at the end of the competition.
    /// </summary>
    HighestTotal
}