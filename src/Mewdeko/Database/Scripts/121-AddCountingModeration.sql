-- Counting moderation system with default configs and role-based actions
-- Migration: 121-AddCountingModeration.sql

-- Create table for guild-wide default counting moderation configurations
CREATE TABLE IF NOT EXISTS "CountingModerationDefaults"
(
    "Id"                        SERIAL PRIMARY KEY,
    "GuildId"                   NUMERIC(20, 0) NOT NULL UNIQUE,
    "EnableModeration"          BOOLEAN        NOT NULL        DEFAULT FALSE,
    "WrongCountThreshold"       INTEGER        NOT NULL        DEFAULT 3,
    "TimeWindowHours"           INTEGER        NOT NULL        DEFAULT 24,
    "PunishmentAction"          INTEGER        NOT NULL        DEFAULT 0, -- Maps to PunishmentAction enum
    "PunishmentDurationMinutes" INTEGER        NOT NULL        DEFAULT 0, -- 0 = permanent
    "PunishmentRoleId"          NUMERIC(20, 0),                           -- For AddRole punishment
    "IgnoreRoles"               TEXT,                                     -- Comma-separated role IDs to ignore from counting
    "DeleteIgnoredMessages"     BOOLEAN        NOT NULL        DEFAULT FALSE,
    "RequiredRoles"             TEXT,                                     -- Comma-separated role IDs required to count
    "BannedRoles"               TEXT,                                     -- Comma-separated role IDs banned from counting
    "CreatedAt"                 TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt"                 TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Create table for per-channel counting moderation configurations (inherits from defaults)
CREATE TABLE IF NOT EXISTS "CountingModerationConfig"
(
    "Id"                        SERIAL PRIMARY KEY,
    "ChannelId"                 NUMERIC(20, 0) NOT NULL UNIQUE,
    "UseDefaults"               BOOLEAN        NOT NULL        DEFAULT TRUE,
    -- Override fields (NULL means use default)
    "EnableModeration"          BOOLEAN,
    "WrongCountThreshold"       INTEGER,
    "TimeWindowHours"           INTEGER,
    "PunishmentAction"          INTEGER,
    "PunishmentDurationMinutes" INTEGER,
    "PunishmentRoleId"          NUMERIC(20, 0),
    "IgnoreRoles"               TEXT,
    "DeleteIgnoredMessages"     BOOLEAN,
    "RequiredRoles"             TEXT,
    "BannedRoles"               TEXT,
    "CreatedAt"                 TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt"                 TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Create table for tracking user wrong counts in time windows
CREATE TABLE IF NOT EXISTS "CountingUserWrongCounts"
(
    "Id"            SERIAL PRIMARY KEY,
    "ChannelId"     NUMERIC(20, 0) NOT NULL,
    "UserId"        NUMERIC(20, 0) NOT NULL,
    "WrongCount"    INTEGER        NOT NULL        DEFAULT 1,
    "WindowStartAt" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "LastWrongAt"   TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("ChannelId", "UserId", "WindowStartAt")
);

-- Create table for tracking applied punishments
CREATE TABLE IF NOT EXISTS "CountingAppliedPunishments"
(
    "Id"                      SERIAL PRIMARY KEY,
    "ChannelId"               NUMERIC(20, 0) NOT NULL,
    "UserId"                  NUMERIC(20, 0) NOT NULL,
    "PunishmentAction"        INTEGER        NOT NULL,
    "DurationMinutes"         INTEGER        NOT NULL        DEFAULT 0,
    "RoleId"                  NUMERIC(20, 0),
    "AppliedAt"               TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "ExpiresAt"               TIMESTAMP(6) WITHOUT TIME ZONE,
    "WrongCountAtApplication" INTEGER        NOT NULL,
    "Reason"                  TEXT,
    "IsActive"                BOOLEAN        NOT NULL        DEFAULT TRUE
);

-- Create table for tiered counting punishment configurations (similar to WarningPunishments)
CREATE TABLE IF NOT EXISTS "CountingModerationPunishments"
(
    "Id"         SERIAL PRIMARY KEY,
    "GuildId"    NUMERIC(20, 0) NOT NULL,
    "ChannelId"  NUMERIC(20, 0), -- NULL for guild-wide defaults
    "Count"      INTEGER        NOT NULL,
    "Punishment" INTEGER        NOT NULL,
    "Time"       INTEGER        NOT NULL        DEFAULT 0,
    "RoleId"     NUMERIC(20, 0),
    "CreatedAt"  TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE ("GuildId", "ChannelId", "Count")
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_defaults_guild" ON "CountingModerationDefaults" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_config_channel" ON "CountingModerationConfig" ("ChannelId");
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_config_use_defaults" ON "CountingModerationConfig" ("UseDefaults");
CREATE INDEX IF NOT EXISTS "idx_counting_user_wrong_counts_channel_user" ON "CountingUserWrongCounts" ("ChannelId", "UserId");
CREATE INDEX IF NOT EXISTS "idx_counting_user_wrong_counts_window" ON "CountingUserWrongCounts" ("WindowStartAt");
CREATE INDEX IF NOT EXISTS "idx_counting_applied_punishments_channel_user" ON "CountingAppliedPunishments" ("ChannelId", "UserId");
CREATE INDEX IF NOT EXISTS "idx_counting_applied_punishments_active" ON "CountingAppliedPunishments" ("IsActive");
CREATE INDEX IF NOT EXISTS "idx_counting_applied_punishments_expires" ON "CountingAppliedPunishments" ("ExpiresAt");
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_punishments_guild" ON "CountingModerationPunishments" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_punishments_channel" ON "CountingModerationPunishments" ("ChannelId");
CREATE INDEX IF NOT EXISTS "idx_counting_moderation_punishments_count" ON "CountingModerationPunishments" ("Count");

-- Add moderation columns to existing CountingChannelConfig table for backward compatibility
ALTER TABLE "CountingChannelConfig"
    ADD COLUMN IF NOT EXISTS "ModerationEnabled"         BOOLEAN        DEFAULT NULL, -- NULL means use guild default
    ADD COLUMN IF NOT EXISTS "WrongCountThreshold"       INTEGER        DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "TimeWindowHours"           INTEGER        DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PunishmentAction"          INTEGER        DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PunishmentDurationMinutes" INTEGER        DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PunishmentRoleId"          NUMERIC(20, 0) DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "IgnoreRoles"               TEXT           DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "DeleteIgnoredMessages"     BOOLEAN        DEFAULT NULL;