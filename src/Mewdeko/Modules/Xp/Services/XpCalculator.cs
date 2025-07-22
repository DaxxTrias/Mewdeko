using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Modules.Xp.Services;

/// <summary>
///     Provides calculation methods for XP-related functionality.
/// </summary>
public static class XpCalculator
{
    /// <summary>
    ///     Calculates the level for a given amount of XP.
    /// </summary>
    /// <param name="xp">The total XP amount.</param>
    /// <param name="curveType">The XP curve type to use for calculation.</param>
    /// <returns>The calculated level.</returns>
    public static int CalculateLevel(long xp, XpCurveType curveType = XpCurveType.Standard)
    {
        if (xp < XpService.BaseXpLvl1)
            return 0;

        switch (curveType)
        {
            case XpCurveType.Linear:
                return (int)(xp / XpService.BaseXpLvl1);

            case XpCurveType.Accelerated:
                return (int)Math.Pow(xp / XpService.BaseXpLvl1, 0.8);

            case XpCurveType.Decelerated:
                return (int)Math.Pow(xp / XpService.BaseXpLvl1, 0.4);

            case XpCurveType.Custom:
                // Custom formula would be implemented by guild owners
                return (int)Math.Floor(Math.Sqrt(xp / XpService.BaseXpLvl1));

            case XpCurveType.Legacy:
                // Legacy calculation method from the original LevelStats class
                var totalXpAccumulated = 0;
                var lvl = 1;

                while (true)
                {
                    var required = (int)(XpService.BaseXpLvl1 + XpService.BaseXpLvl1 / 4.0 * (lvl - 1));
                    if (required + totalXpAccumulated > xp)
                        break;

                    totalXpAccumulated += required;
                    lvl++;
                }

                return lvl - 1;

            case XpCurveType.Standard:
            default:
                return (int)Math.Floor(Math.Sqrt(xp / XpService.BaseXpLvl1));
        }
    }

    /// <summary>
    ///     Calculates the amount of XP needed for a specific level.
    /// </summary>
    /// <param name="level">The level to calculate XP for.</param>
    /// <param name="curveType">The XP curve type to use for calculation.</param>
    /// <returns>The amount of XP needed for the specified level.</returns>
    public static long CalculateXpForLevel(int level, XpCurveType curveType = XpCurveType.Standard)
    {
        if (level <= 0)
            return 0;

        switch (curveType)
        {
            case XpCurveType.Linear:
                return level * XpService.BaseXpLvl1;

            case XpCurveType.Accelerated:
                return (long)Math.Pow(level, 1.25) * XpService.BaseXpLvl1;

            case XpCurveType.Decelerated:
                return (long)Math.Pow(level, 2.5) * XpService.BaseXpLvl1;

            case XpCurveType.Custom:
                // Custom formula would be implemented by guild owners
                return level * level * XpService.BaseXpLvl1;

            case XpCurveType.Legacy:
                // Legacy calculation - total XP required to reach this level
                long totalXp = 0;
                for (var i = 1; i <= level; i++)
                {
                    var levelRequirement = (long)(XpService.BaseXpLvl1 + XpService.BaseXpLvl1 / 4.0 * (i - 1));
                    totalXp += levelRequirement;
                }

                return totalXp;

            case XpCurveType.Standard:
            default:
                return level * level * XpService.BaseXpLvl1;
        }
    }

    /// <summary>
    ///     Calculates the XP within the current level.
    /// </summary>
    /// <param name="totalXp">The total XP amount.</param>
    /// <param name="level">The current level.</param>
    /// <param name="curveType">The XP curve type to use for calculation.</param>
    /// <returns>The amount of XP within the current level.</returns>
    public static long CalculateLevelXp(long totalXp, int level, XpCurveType curveType = XpCurveType.Standard)
    {
        if (level <= 0)
            return totalXp;

        var xpForCurrentLevel = CalculateXpForLevel(level, curveType);
        return totalXp - xpForCurrentLevel;
    }
}