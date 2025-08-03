-- Add Level Up Channel Support
-- This migration adds the ability to set a specific channel for level-up notifications

-- Add LevelUpChannel column to GuildXpSettings table
ALTER TABLE "GuildXpSettings"
    ADD COLUMN IF NOT EXISTS "LevelUpChannel" numeric(20, 0) NOT NULL DEFAULT 0;

-- Add index for performance when looking up level-up channels
CREATE INDEX IF NOT EXISTS "IX_GuildXpSettings_LevelUpChannel"
    ON "GuildXpSettings" ("GuildId", "LevelUpChannel");