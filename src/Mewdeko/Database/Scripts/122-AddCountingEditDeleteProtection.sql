-- Add edit and delete protection for counting channels
-- Migration: 122-AddCountingEditDeleteProtection.sql

-- Add columns for non-number message and edit protection to defaults table
ALTER TABLE "CountingModerationDefaults"
    ADD COLUMN IF NOT EXISTS "PunishNonNumbers" BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeleteNonNumbers" BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS "PunishEdits"      BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS "DeleteEdits"      BOOLEAN NOT NULL DEFAULT TRUE;

-- Add columns for non-number message and edit protection to config table
ALTER TABLE "CountingModerationConfig"
    ADD COLUMN IF NOT EXISTS "PunishNonNumbers" BOOLEAN,
    ADD COLUMN IF NOT EXISTS "DeleteNonNumbers" BOOLEAN,
    ADD COLUMN IF NOT EXISTS "PunishEdits"      BOOLEAN,
    ADD COLUMN IF NOT EXISTS "DeleteEdits"      BOOLEAN;