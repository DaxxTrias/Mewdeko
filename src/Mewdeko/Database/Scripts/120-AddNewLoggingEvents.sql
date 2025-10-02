-- Add new logging event channel columns to LoggingV2 table
-- This adds support for InviteCreated, InviteDeleted, MessagesBulkDeleted, and ReactionEvents logging

ALTER TABLE "LoggingV2"
    ADD COLUMN "InviteCreatedId"       numeric(20, 0) NULL,
    ADD COLUMN "InviteDeletedId"       numeric(20, 0) NULL,
    ADD COLUMN "MessagesBulkDeletedId" numeric(20, 0) NULL,
    ADD COLUMN "ReactionEventsId"      numeric(20, 0) NULL;

-- Create indexes for the new columns for better performance
CREATE INDEX IF NOT EXISTS "IX_LoggingV2_InviteCreatedId" ON "LoggingV2" ("InviteCreatedId");
CREATE INDEX IF NOT EXISTS "IX_LoggingV2_InviteDeletedId" ON "LoggingV2" ("InviteDeletedId");
CREATE INDEX IF NOT EXISTS "IX_LoggingV2_MessagesBulkDeletedId" ON "LoggingV2" ("MessagesBulkDeletedId");
CREATE INDEX IF NOT EXISTS "IX_LoggingV2_ReactionEventsId" ON "LoggingV2" ("ReactionEventsId");