-- Add RepEvent table for time-based reputation events
CREATE TABLE IF NOT EXISTS "RepEvent"
(
    "Id"                 SERIAL PRIMARY KEY,
    "GuildId"            BIGINT        NOT NULL,
    "Name"               VARCHAR(100)  NOT NULL,
    "Description"        TEXT,
    "EventType"          VARCHAR(50)   NOT NULL DEFAULT 'custom',
    "StartTime"          TIMESTAMPTZ   NOT NULL,
    "EndTime"            TIMESTAMPTZ   NOT NULL,
    "Multiplier"         DECIMAL(5, 2) NOT NULL DEFAULT 2.0,
    "BonusAmount"        INT           NOT NULL DEFAULT 0,
    "RestrictedChannels" TEXT, -- JSON array of channel IDs
    "RestrictedRoles"    TEXT, -- JSON array of role IDs
    "IsRecurring"        BOOLEAN       NOT NULL DEFAULT FALSE,
    "RecurrencePattern"  VARCHAR(50),
    "ActiveMessage"      TEXT,
    "IsEnabled"          BOOLEAN       NOT NULL DEFAULT TRUE,
    "EventBadge"         VARCHAR(50),
    "IsAnnounced"        BOOLEAN       NOT NULL DEFAULT FALSE,
    "DateAdded"          TIMESTAMPTZ            DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS "IX_RepEvent_GuildId" ON "RepEvent" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepEvent_IsEnabled" ON "RepEvent" ("IsEnabled");
CREATE INDEX IF NOT EXISTS "IX_RepEvent_StartTime_EndTime" ON "RepEvent" ("StartTime", "EndTime");

-- Add protected roles column to RepConfig for decay immunity
ALTER TABLE "RepConfig"
    ADD COLUMN IF NOT EXISTS "DecayProtectedRoles" TEXT;

-- Add decay type options to support percentage decay
ALTER TABLE "RepConfig"
    ADD COLUMN IF NOT EXISTS "DecayCalculationType" VARCHAR(20) DEFAULT 'fixed';