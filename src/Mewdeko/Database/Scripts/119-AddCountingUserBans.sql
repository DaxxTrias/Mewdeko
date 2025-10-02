-- Create CountingUserBans table for managing user bans from counting channels
CREATE TABLE IF NOT EXISTS "CountingUserBans"
(
    "Id"        serial PRIMARY KEY,
    "ChannelId" numeric(20, 0)              NOT NULL,
    "UserId"    numeric(20, 0)              NOT NULL,
    "BannedBy"  numeric(20, 0)              NOT NULL,
    "BannedAt"  timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ExpiresAt" timestamp without time zone,
    "Reason"    text,
    "IsActive"  boolean                     NOT NULL DEFAULT true
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_CountingUserBans_ChannelId_UserId" ON "CountingUserBans" ("ChannelId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_CountingUserBans_ExpiresAt" ON "CountingUserBans" ("ExpiresAt") WHERE "ExpiresAt" IS NOT NULL;