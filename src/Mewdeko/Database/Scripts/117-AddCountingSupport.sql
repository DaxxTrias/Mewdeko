-- Add support for counting channels
-- Migration: 117-AddCountingSupport.sql

-- Create counting channels table
CREATE TABLE "CountingChannel" (
    "Id" SERIAL PRIMARY KEY,
    "GuildId" NUMERIC(20,0) NOT NULL,
    "ChannelId" NUMERIC(20,0) NOT NULL UNIQUE,
    "CurrentNumber" BIGINT NOT NULL DEFAULT 0,
    "StartNumber" BIGINT NOT NULL DEFAULT 1,
    "Increment" INTEGER NOT NULL DEFAULT 1,
    "LastUserId" NUMERIC(20,0) NOT NULL DEFAULT 0,
    "LastMessageId" NUMERIC(20,0) NOT NULL DEFAULT 0,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "HighestNumber" BIGINT NOT NULL DEFAULT 0,
    "HighestNumberReachedAt" TIMESTAMP(6) WITHOUT TIME ZONE,
    "TotalCounts" BIGINT NOT NULL DEFAULT 0
);

-- Create counting channel configurations table
CREATE TABLE "CountingChannelConfig" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL UNIQUE,
    "AllowRepeatedUsers" BOOLEAN NOT NULL DEFAULT FALSE,
    "Cooldown" INTEGER NOT NULL DEFAULT 0,
    "RequiredRoles" TEXT,
    "BannedRoles" TEXT,
    "MaxNumber" BIGINT NOT NULL DEFAULT 0,
    "ResetOnError" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeleteWrongMessages" BOOLEAN NOT NULL DEFAULT TRUE,
    "Pattern" INTEGER NOT NULL DEFAULT 0, -- 0=Normal, 1=Roman, 2=Binary, etc.
    "NumberBase" INTEGER NOT NULL DEFAULT 10,
    "SuccessEmote" TEXT,
    "ErrorEmote" TEXT,
    "EnableAchievements" BOOLEAN NOT NULL DEFAULT TRUE,
    "EnableCompetitions" BOOLEAN NOT NULL DEFAULT TRUE
);

-- Create counting statistics table
CREATE TABLE "CountingStats" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL,
    "UserId" NUMERIC(20,0) NOT NULL,
    "ContributionsCount" BIGINT NOT NULL DEFAULT 0,
    "HighestStreak" INTEGER NOT NULL DEFAULT 0,
    "CurrentStreak" INTEGER NOT NULL DEFAULT 0,
    "LastContribution" TIMESTAMP(6) WITHOUT TIME ZONE,
    "TotalNumbersCounted" BIGINT NOT NULL DEFAULT 0,
    "ErrorsCount" INTEGER NOT NULL DEFAULT 0,
    "Accuracy" DOUBLE PRECISION NOT NULL DEFAULT 100.0,
    "TotalTimeSpent" BIGINT NOT NULL DEFAULT 0,
    UNIQUE("ChannelId", "UserId")
);

-- Create counting milestones table
CREATE TABLE "CountingMilestones" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL,
    "Number" BIGINT NOT NULL,
    "ReachedAt" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "UserId" NUMERIC(20,0) NOT NULL,
    "RewardGiven" BOOLEAN NOT NULL DEFAULT FALSE,
    "Type" INTEGER NOT NULL DEFAULT 0, -- Milestone type (100, 500, 1000, etc.)
    UNIQUE("ChannelId", "Number")
);

-- Create counting events table for audit trail
CREATE TABLE "CountingEvents" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL,
    "EventType" INTEGER NOT NULL, -- 0=SuccessfulCount, 1=WrongNumber, 2=Reset, etc.
    "UserId" NUMERIC(20,0) NOT NULL,
    "OldNumber" BIGINT,
    "NewNumber" BIGINT,
    "Timestamp" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "MessageId" NUMERIC(20,0),
    "Details" TEXT
);

-- Create counting saves table for backup/restore functionality
CREATE TABLE "CountingSaves" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL,
    "SavedNumber" BIGINT NOT NULL,
    "SavedAt" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "SavedBy" NUMERIC(20,0) NOT NULL,
    "Reason" TEXT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

-- Create counting leaderboard table
CREATE TABLE "CountingLeaderboard" (
    "Id" SERIAL PRIMARY KEY,
    "ChannelId" NUMERIC(20,0) NOT NULL,
    "UserId" NUMERIC(20,0) NOT NULL,
    "Score" BIGINT NOT NULL DEFAULT 0,
    "Rank" INTEGER NOT NULL DEFAULT 0,
    "LastUpdated" TIMESTAMP(6) WITHOUT TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE("ChannelId", "UserId")
);

-- Create indexes for better performance
CREATE INDEX "idx_counting_channel_guild" ON "CountingChannel" ("GuildId");
CREATE INDEX "idx_counting_channel_active" ON "CountingChannel" ("IsActive");
CREATE INDEX "idx_counting_stats_channel" ON "CountingStats" ("ChannelId");
CREATE INDEX "idx_counting_stats_user" ON "CountingStats" ("UserId");
CREATE INDEX "idx_counting_events_channel" ON "CountingEvents" ("ChannelId");
CREATE INDEX "idx_counting_events_timestamp" ON "CountingEvents" ("Timestamp");
CREATE INDEX "idx_counting_milestones_channel" ON "CountingMilestones" ("ChannelId");
CREATE INDEX "idx_counting_saves_channel" ON "CountingSaves" ("ChannelId");
CREATE INDEX "idx_counting_saves_active" ON "CountingSaves" ("IsActive");
CREATE INDEX "idx_counting_leaderboard_channel" ON "CountingLeaderboard" ("ChannelId");
CREATE INDEX "idx_counting_leaderboard_rank" ON "CountingLeaderboard" ("Rank");