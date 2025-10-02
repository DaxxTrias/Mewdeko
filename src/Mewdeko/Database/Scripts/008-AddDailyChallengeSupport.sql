-- Create DailyChallenge table for currency system daily challenges
CREATE TABLE IF NOT EXISTS "DailyChallenges"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "UserId"
    numeric
(
    20,
    0
) NOT NULL,
    "GuildId" numeric
(
    20,
    0
) NOT NULL,
    "Date" date NOT NULL,
    "ChallengeType" integer NOT NULL,
    "Description" text NOT NULL,
    "RequiredAmount" integer NOT NULL,
    "Progress" integer NOT NULL DEFAULT 0,
    "RewardAmount" bigint NOT NULL,
    "IsCompleted" boolean NOT NULL DEFAULT FALSE,
    "CompletedAt" timestamp
(
    6
) without time zone NULL,
    "DateAdded" timestamp
(
    6
)
  without time zone NULL DEFAULT CURRENT_TIMESTAMP
    );

-- Create index for faster lookups
CREATE INDEX IF NOT EXISTS "IX_DailyChallenges_UserId_GuildId_Date"
    ON "DailyChallenges" ("UserId", "GuildId", "Date");

CREATE INDEX IF NOT EXISTS "IX_DailyChallenges_GuildId_Date_IsCompleted"
    ON "DailyChallenges" ("GuildId", "Date", "IsCompleted");