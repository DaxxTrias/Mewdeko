-- Add RepMetrics table for analytics data
CREATE TABLE IF NOT EXISTS "RepMetrics"
(
    "GuildId"                 BIGINT PRIMARY KEY,
    "TotalRepGiven"           BIGINT         NOT NULL DEFAULT 0,
    "UniqueGivers"            INT            NOT NULL DEFAULT 0,
    "UniqueReceivers"         INT            NOT NULL DEFAULT 0,
    "AverageRepPerUser"       DECIMAL(10, 2) NOT NULL DEFAULT 0,
    "PeakHour"                INT            NOT NULL DEFAULT 0, -- 0-23
    "PeakDayOfWeek"           INT            NOT NULL DEFAULT 0, -- 0-6
    "ChannelActivityJson"     TEXT,                              -- JSON with channel activity data
    "RepTypeDistributionJson" TEXT,                              -- JSON with rep type distribution
    "RetentionRate"           DECIMAL(5, 2)  NOT NULL DEFAULT 0, -- Percentage
    "StartDate"               TIMESTAMPTZ    NOT NULL,
    "EndDate"                 TIMESTAMPTZ    NOT NULL,
    "LastCalculated"          TIMESTAMPTZ    NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Add RepHourlyActivity table for heatmap generation
CREATE TABLE IF NOT EXISTS "RepHourlyActivity"
(
    "GuildId"          BIGINT NOT NULL,
    "DayOfWeek"        INT    NOT NULL, -- 0-6
    "Hour"             INT    NOT NULL, -- 0-23
    "TransactionCount" INT    NOT NULL DEFAULT 0,
    "TotalRep"         INT    NOT NULL DEFAULT 0,
    "Date"             DATE   NOT NULL,
    PRIMARY KEY ("GuildId", "Date", "DayOfWeek", "Hour")
);

-- Add RepChannelMetrics table for channel-specific analytics
CREATE TABLE IF NOT EXISTS "RepChannelMetrics"
(
    "GuildId"          BIGINT         NOT NULL,
    "ChannelId"        BIGINT         NOT NULL,
    "ChannelName"      VARCHAR(100)   NOT NULL, -- Cached for performance
    "TotalRep"         INT            NOT NULL DEFAULT 0,
    "TransactionCount" INT            NOT NULL DEFAULT 0,
    "AverageRep"       DECIMAL(10, 2) NOT NULL DEFAULT 0,
    "TopUsersJson"     TEXT,                    -- JSON with top users data
    "LastUpdated"      TIMESTAMPTZ    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY ("GuildId", "ChannelId")
);

-- Create indexes
CREATE INDEX IF NOT EXISTS "IX_RepHourlyActivity_GuildId" ON "RepHourlyActivity" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepHourlyActivity_Date" ON "RepHourlyActivity" ("Date");
CREATE INDEX IF NOT EXISTS "IX_RepChannelMetrics_GuildId" ON "RepChannelMetrics" ("GuildId");

-- Add columns to RepChannelConfig for forum support
ALTER TABLE "RepChannelConfig"
    ADD COLUMN IF NOT EXISTS "ForumTagMultipliers" TEXT; -- JSON mapping tags to multipliers
ALTER TABLE "RepChannelConfig"
    ADD COLUMN IF NOT EXISTS "AutoRepSolution" BOOLEAN DEFAULT FALSE;
ALTER TABLE "RepChannelConfig"
    ADD COLUMN IF NOT EXISTS "SolutionRepAmount" INT DEFAULT 5;
ALTER TABLE "RepChannelConfig"
    ADD COLUMN IF NOT EXISTS "CategoryId" BIGINT;
-- For category-wide settings

-- Add index for category queries
CREATE INDEX IF NOT EXISTS "IX_RepChannelConfig_CategoryId" ON "RepChannelConfig" ("CategoryId");