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
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "Stars";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "Star2";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "StarboardChannel";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "RepostThreshold";

-- Drop XP/Leveling columns
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpTxtTimeout";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpTxtRate";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpVoiceRate";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpVoiceTimeout";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "XpImgUrl";

-- Drop Statistics/Tracking columns
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "Joins";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "Leaves";

-- Drop other removed feature columns
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "CurrencyName";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "GiveawayWinEmbedColor";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "WarnMessage";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "GameMasterRole";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "ReactChannel";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "AutoDeleteByeMessages";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "LogSettingId";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "CleverbotChannel";
ALTER TABLE public."GuildConfigs"
    DROP COLUMN IF EXISTS "WarningsInitialized";