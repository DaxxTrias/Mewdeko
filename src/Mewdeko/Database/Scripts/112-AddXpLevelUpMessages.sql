-- Add Custom Level Up Messages Support
-- This migration adds support for multiple customizable level-up messages with placeholders

-- XP Level Up Messages table - supports multiple custom messages per guild
CREATE TABLE IF NOT EXISTS "XpLevelUpMessages"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        numeric(20, 0)                 NOT NULL,
    "MessageContent" text                           NOT NULL,
    "IsEnabled"      boolean                        NOT NULL DEFAULT TRUE,
    "DateAdded"      timestamp(6) without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS "IX_XpLevelUpMessages_GuildId_IsEnabled"
    ON "XpLevelUpMessages" ("GuildId", "IsEnabled");

-- Default level-up message for existing guilds that don't have custom messages
-- This ensures backward compatibility with the existing LevelUpMessage field