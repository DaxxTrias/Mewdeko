-- Add RepChallenge table for weekly/monthly challenges
CREATE TABLE IF NOT EXISTS "RepChallenge"
(
    "Id"               SERIAL PRIMARY KEY,
    "GuildId"          BIGINT       NOT NULL,
    "Name"             VARCHAR(100) NOT NULL,
    "Description"      TEXT         NOT NULL,
    "ChallengeType"    VARCHAR(20)  NOT NULL DEFAULT 'weekly', -- daily, weekly, monthly, custom
    "GoalType"         VARCHAR(50)  NOT NULL,                  -- give_rep_unique_users, earn_rep_amount, etc
    "TargetValue"      INT          NOT NULL,
    "RepReward"        INT          NOT NULL,
    "XpReward"         INT,
    "BadgeReward"      VARCHAR(50),
    "RoleReward"       BIGINT,
    "StartTime"        TIMESTAMPTZ  NOT NULL,
    "EndTime"          TIMESTAMPTZ  NOT NULL,
    "IsServerWide"     BOOLEAN      NOT NULL DEFAULT FALSE,
    "ServerWideTarget" INT,
    "MinParticipants"  INT,
    "IsActive"         BOOLEAN      NOT NULL DEFAULT TRUE,
    "AnnounceProgress" BOOLEAN      NOT NULL DEFAULT TRUE,
    "AnnounceChannel"  BIGINT,
    "DateAdded"        TIMESTAMPTZ           DEFAULT CURRENT_TIMESTAMP
);

-- Add RepChallengeProgress table to track user progress
CREATE TABLE IF NOT EXISTS "RepChallengeProgress"
(
    "UserId"         BIGINT      NOT NULL,
    "ChallengeId"    INT         NOT NULL,
    "GuildId"        BIGINT      NOT NULL,
    "Progress"       INT         NOT NULL DEFAULT 0,
    "IsCompleted"    BOOLEAN     NOT NULL DEFAULT FALSE,
    "CompletedAt"    TIMESTAMPTZ,
    "RewardsClaimed" BOOLEAN     NOT NULL DEFAULT FALSE,
    "ProgressData"   TEXT, -- JSON data for complex progress tracking
    "LastUpdated"    TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("UserId", "ChallengeId", "GuildId")
);

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_RepChallenge_GuildId" ON "RepChallenge" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepChallenge_IsActive" ON "RepChallenge" ("IsActive");
CREATE INDEX IF NOT EXISTS "IX_RepChallenge_EndTime" ON "RepChallenge" ("EndTime");
CREATE INDEX IF NOT EXISTS "IX_RepChallengeProgress_ChallengeId" ON "RepChallengeProgress" ("ChallengeId");
CREATE INDEX IF NOT EXISTS "IX_RepChallengeProgress_GuildId" ON "RepChallengeProgress" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepChallengeProgress_IsCompleted" ON "RepChallengeProgress" ("IsCompleted");