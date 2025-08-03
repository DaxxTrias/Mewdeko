-- Add Level Up Pings Disabled Support
-- This migration adds the ability for users to disable level-up pings/mentions

-- Add LevelUpPingsDisabled column to DiscordUser table
ALTER TABLE "DiscordUser"
    ADD COLUMN IF NOT EXISTS "LevelUpPingsDisabled" boolean NOT NULL DEFAULT FALSE;

-- Add index for performance when checking ping preferences
CREATE INDEX IF NOT EXISTS "IX_DiscordUser_LevelUpPingsDisabled"
    ON "DiscordUser" ("UserId", "LevelUpPingsDisabled");