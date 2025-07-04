-- Create new table for ignored log channels
CREATE TABLE IF NOT EXISTS "LogIgnoredChannels"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0) NOT NULL,
    "ChannelId" NUMERIC(20, 0) NOT NULL,
    "DateAdded" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create index for faster lookups
CREATE INDEX IF NOT EXISTS "IX_LogIgnoredChannels_GuildId" ON "LogIgnoredChannels" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_LogIgnoredChannels_GuildId_ChannelId" ON "LogIgnoredChannels" ("GuildId", "ChannelId");

-- Add unique constraint to prevent duplicate entries
CREATE UNIQUE INDEX IF NOT EXISTS "IX_LogIgnoredChannels_Unique" ON "LogIgnoredChannels" ("GuildId", "ChannelId");

-- Drop the old IgnoredLogChannels table if it exists (it used LogSettingId which is deprecated)
DROP TABLE IF EXISTS "IgnoredLogChannels";