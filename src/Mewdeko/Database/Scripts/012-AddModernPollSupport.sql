-- Migration script for modern poll system
-- Adds support for multiple concurrent polls with advanced features

-- Create Polls table
CREATE TABLE IF NOT EXISTS "Polls"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)                 NOT NULL,
    "ChannelId" NUMERIC(20, 0)                 NOT NULL,
    "MessageId" NUMERIC(20, 0)                 NOT NULL,
    "CreatorId" NUMERIC(20, 0)                 NOT NULL,
    "Question"  TEXT                           NOT NULL,
    "Type"      INTEGER                        NOT NULL DEFAULT 0,
    "Settings"  TEXT,
    "CreatedAt" TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP(6) WITHOUT TIME ZONE,
    "ClosedAt"  TIMESTAMP(6) WITHOUT TIME ZONE,
    "IsActive"  BOOLEAN                        NOT NULL DEFAULT true
);

-- Create PollOptions table
CREATE TABLE IF NOT EXISTS "PollOptions"
(
    "Id"     SERIAL PRIMARY KEY,
    "PollId" INTEGER NOT NULL,
    "Text"   TEXT    NOT NULL,
    "Index"  INTEGER NOT NULL,
    "Color"  TEXT,
    "Emote"  TEXT,
    CONSTRAINT "FK_PollOptions_Polls_PollId" FOREIGN KEY ("PollId") REFERENCES "Polls" ("Id") ON DELETE CASCADE
);

-- Create PollVotes table
CREATE TABLE IF NOT EXISTS "PollVotes"
(
    "Id"            SERIAL PRIMARY KEY,
    "PollId"        INTEGER                        NOT NULL,
    "UserId"        NUMERIC(20, 0)                 NOT NULL,
    "OptionIndices" TEXT                           NOT NULL,
    "VotedAt"       TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    "IsAnonymous"   BOOLEAN                        NOT NULL DEFAULT false,
    CONSTRAINT "FK_PollVotes_Polls_PollId" FOREIGN KEY ("PollId") REFERENCES "Polls" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_PollVotes_PollId_UserId" UNIQUE ("PollId", "UserId")
);

-- Create PollTemplates table
CREATE TABLE IF NOT EXISTS "PollTemplates"
(
    "Id"        SERIAL PRIMARY KEY,
    "GuildId"   NUMERIC(20, 0)                 NOT NULL,
    "Name"      TEXT                           NOT NULL,
    "Question"  TEXT                           NOT NULL,
    "Options"   TEXT                           NOT NULL,
    "Settings"  TEXT,
    "CreatorId" NUMERIC(20, 0)                 NOT NULL,
    "CreatedAt" TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_Polls_GuildId" ON "Polls" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_Polls_ChannelId" ON "Polls" ("ChannelId");
CREATE INDEX IF NOT EXISTS "IX_Polls_MessageId" ON "Polls" ("MessageId");
CREATE INDEX IF NOT EXISTS "IX_Polls_IsActive" ON "Polls" ("IsActive");
CREATE INDEX IF NOT EXISTS "IX_Polls_ExpiresAt" ON "Polls" ("ExpiresAt") WHERE "ExpiresAt" IS NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_PollOptions_PollId" ON "PollOptions" ("PollId");
CREATE INDEX IF NOT EXISTS "IX_PollOptions_Index" ON "PollOptions" ("Index");

CREATE INDEX IF NOT EXISTS "IX_PollVotes_PollId" ON "PollVotes" ("PollId");
CREATE INDEX IF NOT EXISTS "IX_PollVotes_UserId" ON "PollVotes" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_PollVotes_VotedAt" ON "PollVotes" ("VotedAt");

CREATE INDEX IF NOT EXISTS "IX_PollTemplates_GuildId" ON "PollTemplates" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_PollTemplates_CreatorId" ON "PollTemplates" ("CreatorId");