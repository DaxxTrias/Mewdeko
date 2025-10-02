-- Create BirthdayConfig table for birthday announcement configuration
CREATE TABLE IF NOT EXISTS "BirthdayConfigs"
(
    "Id"
                           SERIAL
        PRIMARY
            KEY,
    "GuildId"
                           numeric(20,
                               0)                NOT NULL UNIQUE,
    "BirthdayChannelId"    numeric(20,
                               0)                NULL,
    "BirthdayRoleId"       numeric(20,
                               0)                NULL,
    "BirthdayMessage"      text                  NULL,
    "BirthdayPingRoleId"   numeric(20,
                               0)                NULL,
    "BirthdayReminderDays" integer               NOT NULL DEFAULT 0,
    "DefaultTimezone"      text                  NULL     DEFAULT 'UTC',
    "EnabledFeatures"      integer               NOT NULL DEFAULT 0,
    "DateAdded"            timestamp(6)
                               without time zone NULL     DEFAULT CURRENT_TIMESTAMP,
    "DateModified"         timestamp(6)
                               without time zone NULL     DEFAULT CURRENT_TIMESTAMP
);

-- Create index for faster guild lookups
CREATE INDEX IF NOT EXISTS "IX_BirthdayConfigs_GuildId"
    ON "BirthdayConfigs" ("GuildId");

-- Add birthday announcement fields to DiscordUser table
ALTER TABLE "DiscordUser"
    ADD COLUMN IF NOT EXISTS "BirthdayAnnouncementsEnabled" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "DiscordUser"
    ADD COLUMN IF NOT EXISTS "BirthdayTimezone" text NULL;

-- Create index for birthday queries
CREATE INDEX IF NOT EXISTS "IX_DiscordUser_Birthday_Announcements"
    ON "DiscordUser" ("Birthday", "BirthdayAnnouncementsEnabled")
    WHERE "Birthday" IS NOT NULL AND "BirthdayAnnouncementsEnabled" = TRUE;