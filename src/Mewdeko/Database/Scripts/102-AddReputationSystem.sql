-- Create core reputation system tables

-- RepConfig table - Guild-wide reputation configuration
CREATE TABLE IF NOT EXISTS "RepConfig"
(
    "GuildId"                  NUMERIC(20, 0) PRIMARY KEY,
    "Enabled"                  BOOLEAN                        NOT NULL DEFAULT TRUE,
    "DefaultCooldownMinutes"   INTEGER                        NOT NULL DEFAULT 60,
    "DailyLimit"               INTEGER                        NOT NULL DEFAULT 10,
    "WeeklyLimit"              INTEGER                        NULL,
    "MinAccountAgeDays"        INTEGER                        NOT NULL DEFAULT 7,
    "MinServerMembershipHours" INTEGER                        NOT NULL DEFAULT 24,
    "MinMessageCount"          INTEGER                        NOT NULL DEFAULT 10,
    "EnableNegativeRep"        BOOLEAN                        NOT NULL DEFAULT FALSE,
    "EnableAnonymous"          BOOLEAN                        NOT NULL DEFAULT FALSE,
    "EnableDecay"              BOOLEAN                        NOT NULL DEFAULT FALSE,
    "DecayType"                VARCHAR(50)                    NOT NULL DEFAULT 'weekly',
    "DecayAmount"              INTEGER                        NOT NULL DEFAULT 1,
    "DecayInactiveDays"        INTEGER                        NOT NULL DEFAULT 30,
    "NotificationChannel"      NUMERIC(20, 0)                 NULL,
    "DateAdded"                TIMESTAMP(6) WITHOUT TIME ZONE NULL
);

-- UserReputation table - User reputation data per guild
CREATE TABLE IF NOT EXISTS "UserReputation"
(
    "Id"             SERIAL PRIMARY KEY,
    "UserId"         NUMERIC(20, 0)                 NOT NULL,
    "GuildId"        NUMERIC(20, 0)                 NOT NULL,
    "TotalRep"       INTEGER                        NOT NULL DEFAULT 0,
    "HelperRep"      INTEGER                        NULL,
    "ArtistRep"      INTEGER                        NULL,
    "MemerRep"       INTEGER                        NULL,
    "LastGivenAt"    TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    "LastReceivedAt" TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    "CurrentStreak"  INTEGER                        NOT NULL DEFAULT 0,
    "LongestStreak"  INTEGER                        NOT NULL DEFAULT 0,
    "IsFrozen"       BOOLEAN                        NOT NULL DEFAULT FALSE,
    "DateAdded"      TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    UNIQUE ("UserId", "GuildId")
);

-- RepHistory table - History of all reputation transactions
CREATE TABLE IF NOT EXISTS "RepHistory"
(
    "Id"          SERIAL PRIMARY KEY,
    "GiverId"     NUMERIC(20, 0)                 NOT NULL,
    "ReceiverId"  NUMERIC(20, 0)                 NOT NULL,
    "GuildId"     NUMERIC(20, 0)                 NOT NULL,
    "ChannelId"   NUMERIC(20, 0)                 NOT NULL,
    "Amount"      INTEGER                        NOT NULL,
    "RepType"     VARCHAR(100)                   NOT NULL,
    "Reason"      TEXT                           NULL,
    "IsAnonymous" BOOLEAN                        NOT NULL DEFAULT FALSE,
    "Timestamp"   TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

-- RepCooldowns table - Active cooldowns between users
CREATE TABLE IF NOT EXISTS "RepCooldowns"
(
    "GiverId"    NUMERIC(20, 0)                 NOT NULL,
    "ReceiverId" NUMERIC(20, 0)                 NOT NULL,
    "GuildId"    NUMERIC(20, 0)                 NOT NULL,
    "ExpiresAt"  TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL,
    PRIMARY KEY ("GiverId", "ReceiverId", "GuildId")
);

-- RepChannelConfig table - Channel-specific reputation settings
CREATE TABLE IF NOT EXISTS "RepChannelConfig"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)                 NOT NULL,
    "ChannelId"      NUMERIC(20, 0)                 NOT NULL,
    "State"          VARCHAR(50)                    NOT NULL DEFAULT 'enabled',
    "Multiplier"     DECIMAL(5, 2)                  NOT NULL DEFAULT 1.0,
    "CustomCooldown" INTEGER                        NULL,
    "RepType"        VARCHAR(100)                   NULL,
    "DateAdded"      TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    UNIQUE ("GuildId", "ChannelId")
);

-- RepRoleRewards table - Role rewards for reputation milestones
CREATE TABLE IF NOT EXISTS "RepRoleRewards"
(
    "Id"              SERIAL PRIMARY KEY,
    "GuildId"         NUMERIC(20, 0)                 NOT NULL,
    "RoleId"          NUMERIC(20, 0)                 NOT NULL,
    "RepRequired"     INTEGER                        NOT NULL,
    "RemoveOnDrop"    BOOLEAN                        NOT NULL DEFAULT TRUE,
    "AnnounceChannel" NUMERIC(20, 0)                 NULL,
    "AnnounceDM"      BOOLEAN                        NOT NULL DEFAULT FALSE,
    "XPReward"        INTEGER                        NULL,
    "DateAdded"       TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    UNIQUE ("GuildId", "RoleId")
);

-- RepBadges table - Achievement badges for users
CREATE TABLE IF NOT EXISTS "RepBadges"
(
    "Id"        SERIAL PRIMARY KEY,
    "UserId"    NUMERIC(20, 0)                 NOT NULL,
    "GuildId"   NUMERIC(20, 0)                 NOT NULL,
    "BadgeType" VARCHAR(100)                   NOT NULL,
    "EarnedAt"  TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE ("UserId", "GuildId", "BadgeType")
);

-- RepUserSettings table - User-specific reputation preferences
CREATE TABLE IF NOT EXISTS "RepUserSettings"
(
    "UserId"        NUMERIC(20, 0)                 NOT NULL,
    "GuildId"       NUMERIC(20, 0)                 NOT NULL,
    "ReceiveDMs"    BOOLEAN                        NOT NULL DEFAULT TRUE,
    "DMThreshold"   INTEGER                        NOT NULL DEFAULT 10,
    "PublicHistory" BOOLEAN                        NOT NULL DEFAULT TRUE,
    "DateAdded"     TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    PRIMARY KEY ("UserId", "GuildId")
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS "idx_userreputation_guild" ON "UserReputation" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_userreputation_user" ON "UserReputation" ("UserId");
CREATE INDEX IF NOT EXISTS "idx_userreputation_totalrep" ON "UserReputation" ("TotalRep" DESC);

CREATE INDEX IF NOT EXISTS "idx_rephistory_guild" ON "RepHistory" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_rephistory_giver" ON "RepHistory" ("GiverId");
CREATE INDEX IF NOT EXISTS "idx_rephistory_receiver" ON "RepHistory" ("ReceiverId");
CREATE INDEX IF NOT EXISTS "idx_rephistory_timestamp" ON "RepHistory" ("Timestamp" DESC);

CREATE INDEX IF NOT EXISTS "idx_repcooldowns_expires" ON "RepCooldowns" ("ExpiresAt");

CREATE INDEX IF NOT EXISTS "idx_repchannelconfig_guild" ON "RepChannelConfig" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_repchannelconfig_channel" ON "RepChannelConfig" ("ChannelId");

CREATE INDEX IF NOT EXISTS "idx_reprolerewards_guild" ON "RepRoleRewards" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_reprolerewards_rep" ON "RepRoleRewards" ("RepRequired");

CREATE INDEX IF NOT EXISTS "idx_repbadges_user_guild" ON "RepBadges" ("UserId", "GuildId");
CREATE INDEX IF NOT EXISTS "idx_repbadges_type" ON "RepBadges" ("BadgeType");