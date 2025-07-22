-- Add guild support to Embeds table
ALTER TABLE "Embeds"
    ADD COLUMN IF NOT EXISTS "GuildId" NUMERIC(20, 0) NULL;

ALTER TABLE "Embeds"
    ADD COLUMN IF NOT EXISTS "IsGuildShared" BOOLEAN NOT NULL DEFAULT FALSE;

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_Embeds_GuildId" ON "Embeds" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_Embeds_UserId_GuildId" ON "Embeds" ("UserId", "GuildId");
CREATE INDEX IF NOT EXISTS "IX_Embeds_GuildId_IsGuildShared" ON "Embeds" ("GuildId", "IsGuildShared") WHERE "IsGuildShared" = TRUE;

-- Create index for fast template lookups
CREATE INDEX IF NOT EXISTS "IX_Embeds_EmbedName_UserId_GuildId" ON "Embeds" ("EmbedName", "UserId", "GuildId");
CREATE INDEX IF NOT EXISTS "IX_Embeds_EmbedName_GuildId_IsGuildShared" ON "Embeds" ("EmbedName", "GuildId", "IsGuildShared") WHERE "IsGuildShared" = TRUE;