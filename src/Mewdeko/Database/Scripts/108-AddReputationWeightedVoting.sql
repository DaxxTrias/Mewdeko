-- Add RepWeightedVote table for reputation-weighted polls
CREATE TABLE IF NOT EXISTS "RepWeightedVote"
(
    "Id"               SERIAL PRIMARY KEY,
    "GuildId"          BIGINT       NOT NULL,
    "ChannelId"        BIGINT       NOT NULL,
    "MessageId"        BIGINT       NOT NULL,
    "CreatorId"        BIGINT       NOT NULL,
    "Title"            VARCHAR(200) NOT NULL,
    "Description"      TEXT         NOT NULL,
    "OptionsJson"      TEXT         NOT NULL DEFAULT '[]',            -- JSON array of options
    "VoteType"         VARCHAR(20)  NOT NULL DEFAULT 'single_choice', -- single_choice, multiple_choice, yes_no
    "WeightMethod"     VARCHAR(20)  NOT NULL DEFAULT 'linear',        -- linear, logarithmic, tiered
    "WeightConfigJson" TEXT,                                          -- JSON configuration for weight calculation
    "MinRepToVote"     INT          NOT NULL DEFAULT 0,
    "MaxWeightPerUser" INT          NOT NULL DEFAULT 0,               -- 0 = unlimited
    "ShowLiveResults"  BOOLEAN      NOT NULL DEFAULT TRUE,
    "ShowVoterNames"   BOOLEAN      NOT NULL DEFAULT FALSE,
    "AllowAnonymous"   BOOLEAN      NOT NULL DEFAULT FALSE,
    "StartTime"        TIMESTAMPTZ  NOT NULL,
    "EndTime"          TIMESTAMPTZ  NOT NULL,
    "IsClosed"         BOOLEAN      NOT NULL DEFAULT FALSE,
    "RequiredRoles"    TEXT,                                          -- JSON array of role IDs
    "CustomRepType"    VARCHAR(50),                                   -- Use custom rep type for weighting
    "DateAdded"        TIMESTAMPTZ           DEFAULT CURRENT_TIMESTAMP
);

-- Add RepWeightedVoteRecord table to track individual votes
CREATE TABLE IF NOT EXISTS "RepWeightedVoteRecord"
(
    "VoteId"            INT            NOT NULL,
    "UserId"            BIGINT         NOT NULL,
    "GuildId"           BIGINT         NOT NULL,
    "ChosenOptionsJson" TEXT           NOT NULL DEFAULT '[]', -- JSON array for multiple choice
    "UserReputation"    INT            NOT NULL,
    "VoteWeight"        DECIMAL(10, 2) NOT NULL,
    "IsAnonymous"       BOOLEAN        NOT NULL DEFAULT FALSE,
    "VotedAt"           TIMESTAMPTZ    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("VoteId", "UserId"),
    FOREIGN KEY ("VoteId") REFERENCES "RepWeightedVote" ("Id") ON DELETE CASCADE
);

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_RepWeightedVote_GuildId" ON "RepWeightedVote" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepWeightedVote_IsClosed" ON "RepWeightedVote" ("IsClosed");
CREATE INDEX IF NOT EXISTS "IX_RepWeightedVote_EndTime" ON "RepWeightedVote" ("EndTime");
CREATE INDEX IF NOT EXISTS "IX_RepWeightedVoteRecord_UserId" ON "RepWeightedVoteRecord" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_RepWeightedVoteRecord_GuildId" ON "RepWeightedVoteRecord" ("GuildId");