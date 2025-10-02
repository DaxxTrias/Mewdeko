-- Add customization columns to CountingChannelConfig table
ALTER TABLE "CountingChannelConfig"
    ADD COLUMN IF NOT EXISTS "SuccessMessage"     text,
    ADD COLUMN IF NOT EXISTS "FailureMessage"     text,
    ADD COLUMN IF NOT EXISTS "MilestoneMessage"   text,
    ADD COLUMN IF NOT EXISTS "MilestoneChannelId" numeric(20, 0),
    ADD COLUMN IF NOT EXISTS "FailureChannelId"   numeric(20, 0),
    ADD COLUMN IF NOT EXISTS "Milestones"         text,
    ADD COLUMN IF NOT EXISTS "FailureThreshold"   integer DEFAULT 3,
    ADD COLUMN IF NOT EXISTS "CooldownSeconds"    integer DEFAULT 0;