-- Migration: Remove unused columns from GuildConfigs table
-- Date: 2025-09-02
-- Description: Drops columns that were removed from the GuildConfig C# model

-- Drop Starboard-related columns
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardAllowBots";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardRemoveOnDelete";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardRemoveOnReactionsClear";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardRemoveOnBelowThreshold";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "UseStarboardBlacklist";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardCheckChannels";

-- Drop other removed columns
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "CurrencyName";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "GiveawayWinEmbedColor";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpImgUrl";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "WarnMessage";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "GameMasterRole";
