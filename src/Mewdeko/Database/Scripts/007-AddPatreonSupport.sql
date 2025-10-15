-- Add Patreon support columns to GuildConfigs table
ALTER TABLE "GuildConfigs"
    ADD COLUMN IF NOT EXISTS "PatreonChannelId" numeric (20,0) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "PatreonMessage" text NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonAnnouncementDay" integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "PatreonEnabled" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "PatreonLastAnnouncement" timestamp (6) without time zone NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonCampaignId" text NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonAccessToken" text NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonRefreshToken" text NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonTokenExpiry" timestamp (6) without time zone NULL DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS "PatreonRoleSync" boolean NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "PatreonGoalChannel" numeric (20,0) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "PatreonStatsChannel" numeric (20,0) NOT NULL DEFAULT 0;

-- Create Patreon tiers table for role mapping
CREATE TABLE IF NOT EXISTS "PatreonTiers"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "GuildId"
    numeric
(
    20,
    0
) NOT NULL,
    "TierId" text NOT NULL,
    "TierTitle" text NOT NULL,
    "AmountCents" integer NOT NULL,
    "DiscordRoleId" numeric
(
    20,
    0
) NOT NULL DEFAULT 0,
    "Description" text NULL,
    "DateAdded" timestamp
(
    6
) without time zone NOT NULL DEFAULT NOW
(
),
    "IsActive" boolean NOT NULL DEFAULT TRUE
    );

-- Create Patreon supporters table for caching
CREATE TABLE IF NOT EXISTS "PatreonSupporters"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "GuildId"
    numeric
(
    20,
    0
) NOT NULL,
    "PatreonUserId" text NOT NULL,
    "DiscordUserId" numeric
(
    20,
    0
) NOT NULL DEFAULT 0,
    "FullName" text NOT NULL,
    "Email" text NULL,
    "TierId" text NULL,
    "AmountCents" integer NOT NULL DEFAULT 0,
    "PatronStatus" text NOT NULL,
    "PledgeRelationshipStart" timestamp
(
    6
) without time zone NULL,
    "LastChargeDate" timestamp
(
    6
)
  without time zone NULL,
    "LastChargeStatus" text NULL,
    "LifetimeAmountCents" integer NOT NULL DEFAULT 0,
    "CurrentlyEntitledAmountCents" integer NOT NULL DEFAULT 0,
    "LastUpdated" timestamp
(
    6
)
  without time zone NOT NULL DEFAULT NOW
(
),
    UNIQUE
(
    "GuildId",
    "PatreonUserId"
)
    );

-- Create Patreon goals table
CREATE TABLE IF NOT EXISTS "PatreonGoals"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "GuildId"
    numeric
(
    20,
    0
) NOT NULL,
    "GoalId" text NOT NULL,
    "Title" text NOT NULL,
    "Description" text NULL,
    "AmountCents" integer NOT NULL,
    "CompletedPercentage" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp
(
    6
) without time zone NOT NULL,
    "ReachedAt" timestamp
(
    6
)
  without time zone NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "LastUpdated" timestamp
(
    6
)
  without time zone NOT NULL DEFAULT NOW
(
),
    UNIQUE
(
    "GuildId",
    "GoalId"
)
    );

-- Create indexes for efficient queries
CREATE INDEX IF NOT EXISTS "IX_GuildConfigs_PatreonEnabled_AnnouncementDay"
    ON "GuildConfigs" ("PatreonEnabled", "PatreonAnnouncementDay")
    WHERE "PatreonEnabled" = TRUE;

CREATE INDEX IF NOT EXISTS "IX_PatreonTiers_GuildId_IsActive"
    ON "PatreonTiers" ("GuildId", "IsActive")
    WHERE "IsActive" = TRUE;

CREATE INDEX IF NOT EXISTS "IX_PatreonSupporters_GuildId_PatronStatus"
    ON "PatreonSupporters" ("GuildId", "PatronStatus");

CREATE INDEX IF NOT EXISTS "IX_PatreonSupporters_DiscordUserId"
    ON "PatreonSupporters" ("DiscordUserId")
    WHERE "DiscordUserId" != 0;

CREATE INDEX IF NOT EXISTS "IX_PatreonGoals_GuildId_IsActive"
    ON "PatreonGoals" ("GuildId", "IsActive")
    WHERE "IsActive" = TRUE;