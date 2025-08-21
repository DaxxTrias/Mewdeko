-- Add anti-pattern protection system
-- Migration: 124-AddAntiPatternSupport.sql

-- Create AntiPatternSetting table
CREATE TABLE IF NOT EXISTS "AntiPatternSetting"
(
    "Id"                  SERIAL PRIMARY KEY,
    "GuildId"             BIGINT           NOT NULL,
    "Action"              INTEGER          NOT NULL DEFAULT 0,
    "PunishDuration"      INTEGER          NOT NULL DEFAULT 0,
    "RoleId"              BIGINT           NULL,
    "DateAdded"           TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CheckAccountAge"     BOOLEAN          NOT NULL DEFAULT FALSE,
    "MaxAccountAgeMonths" INTEGER          NOT NULL DEFAULT 6,
    "CheckJoinTiming"     BOOLEAN          NOT NULL DEFAULT FALSE,
    "MaxJoinHours"        DOUBLE PRECISION NOT NULL DEFAULT 48.0,
    "CheckBatchCreation"  BOOLEAN          NOT NULL DEFAULT FALSE,
    "CheckOfflineStatus"  BOOLEAN          NOT NULL DEFAULT FALSE,
    "CheckNewAccounts"    BOOLEAN          NOT NULL DEFAULT FALSE,
    "NewAccountDays"      INTEGER          NOT NULL DEFAULT 7,
    "MinimumScore"        INTEGER          NOT NULL DEFAULT 15
);

-- Create AntiPatternPattern table to store regex patterns
CREATE TABLE IF NOT EXISTS "AntiPatternPattern"
(
    "Id"                   SERIAL PRIMARY KEY,
    "AntiPatternSettingId" INTEGER   NOT NULL,
    "Pattern"              TEXT      NOT NULL,
    "Name"                 TEXT      NULL,
    "CheckUsername"        BOOLEAN   NOT NULL DEFAULT TRUE,
    "CheckDisplayName"     BOOLEAN   NOT NULL DEFAULT TRUE,
    "DateAdded"            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("AntiPatternSettingId") REFERENCES "AntiPatternSetting" ("Id") ON DELETE CASCADE
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_AntiPatternSetting_GuildId" ON "AntiPatternSetting" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_AntiPatternPattern_AntiPatternSettingId" ON "AntiPatternPattern" ("AntiPatternSettingId");