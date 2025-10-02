-- Add sticky message features to repeaters
-- Migration: 123-AddStickyMessageFeatures.sql

-- Add columns for sticky message functionality to GuildRepeater table
ALTER TABLE "GuildRepeater"
    ADD COLUMN IF NOT EXISTS "TriggerMode"            INTEGER NOT NULL DEFAULT 0, -- 0 = TimeInterval
    ADD COLUMN IF NOT EXISTS "ActivityThreshold"      INTEGER NOT NULL DEFAULT 5, -- messages needed for activity trigger
    ADD COLUMN IF NOT EXISTS "ActivityTimeWindow"     TEXT    NOT NULL DEFAULT '00:05:00', -- 5 minute window
    ADD COLUMN IF NOT EXISTS "ConversationDetection"  BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "ConversationThreshold"  INTEGER NOT NULL DEFAULT 3, -- messages per minute for active conversation
    ADD COLUMN IF NOT EXISTS "Priority"               INTEGER NOT NULL DEFAULT 50, -- 0-100, higher = more important
    ADD COLUMN IF NOT EXISTS "QueuePosition"          INTEGER NOT NULL DEFAULT 0, -- rotation order within priority
    ADD COLUMN IF NOT EXISTS "TimeConditions"         TEXT, -- JSON for time-based rules
    ADD COLUMN IF NOT EXISTS "MaxAge"                 TEXT, -- TimeSpan for auto-expire
    ADD COLUMN IF NOT EXISTS "MaxTriggers"            INTEGER, -- max displays before auto-expire
    ADD COLUMN IF NOT EXISTS "ThreadAutoSticky"       BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "ForumTagConditions"     TEXT, -- JSON for forum tag rules
    ADD COLUMN IF NOT EXISTS "IsEnabled"              BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "DisplayCount"           INTEGER NOT NULL DEFAULT 0, -- track how many times displayed
    ADD COLUMN IF NOT EXISTS "LastDisplayed"          TIMESTAMP, -- when last displayed
    ADD COLUMN IF NOT EXISTS "ActivityBasedLastCheck" TIMESTAMP, -- last activity check
    ADD COLUMN IF NOT EXISTS "ThreadOnlyMode"         BOOLEAN NOT NULL DEFAULT FALSE, -- only post in threads, not parent channel
    ADD COLUMN IF NOT EXISTS "ThreadStickyMessages"   TEXT; -- JSON tracking thread sticky message IDs