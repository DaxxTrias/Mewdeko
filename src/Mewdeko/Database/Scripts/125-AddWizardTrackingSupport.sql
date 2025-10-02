-- Add first-time wizard tracking support
-- Migration: 125-AddWizardTrackingSupport.sql

-- Add wizard tracking columns to DiscordUser table
ALTER TABLE "DiscordUser"
    ADD COLUMN "HasCompletedAnyWizard"    boolean DEFAULT false,
    ADD COLUMN "FirstDashboardAccess"     timestamp(6) without time zone,
    ADD COLUMN "DashboardExperienceLevel" integer DEFAULT 0,
    ADD COLUMN "PrefersGuidedSetup"       boolean DEFAULT true,
    ADD COLUMN "WizardCompletedGuilds"    text;

-- Add wizard state columns to GuildConfigs table
ALTER TABLE "GuildConfigs"
    ADD COLUMN "WizardCompleted"         boolean DEFAULT false,
    ADD COLUMN "WizardCompletedAt"       timestamp(6) without time zone,
    ADD COLUMN "WizardCompletedByUserId" numeric(20, 0),
    ADD COLUMN "WizardSkipped"           boolean DEFAULT false,
    ADD COLUMN "HasBasicSetup"           boolean DEFAULT false;

-- Create indexes for performance
CREATE INDEX IF NOT EXISTS "IX_DiscordUser_HasCompletedAnyWizard" ON "DiscordUser" ("HasCompletedAnyWizard");
CREATE INDEX IF NOT EXISTS "IX_DiscordUser_DashboardExperienceLevel" ON "DiscordUser" ("DashboardExperienceLevel");
CREATE INDEX IF NOT EXISTS "IX_GuildConfigs_WizardCompleted" ON "GuildConfigs" ("WizardCompleted");
CREATE INDEX IF NOT EXISTS "IX_GuildConfigs_WizardSkipped" ON "GuildConfigs" ("WizardSkipped");
CREATE INDEX IF NOT EXISTS "IX_GuildConfigs_HasBasicSetup" ON "GuildConfigs" ("HasBasicSetup");

-- Comments for documentation
COMMENT ON COLUMN "DiscordUser"."HasCompletedAnyWizard" IS 'Tracks if user has ever completed the dashboard wizard';
COMMENT ON COLUMN "DiscordUser"."FirstDashboardAccess" IS 'Timestamp of users first dashboard access';
COMMENT ON COLUMN "DiscordUser"."DashboardExperienceLevel" IS '0=new, 1=basic, 2=experienced, 3=advanced';
COMMENT ON COLUMN "DiscordUser"."PrefersGuidedSetup" IS 'User preference for showing guided setup for new guilds';
COMMENT ON COLUMN "DiscordUser"."WizardCompletedGuilds" IS 'JSON array of guild IDs where user completed wizard';

COMMENT ON COLUMN "GuildConfigs"."WizardCompleted" IS 'Whether the setup wizard has been completed for this guild';
COMMENT ON COLUMN "GuildConfigs"."WizardCompletedAt" IS 'Timestamp when wizard was completed';
COMMENT ON COLUMN "GuildConfigs"."WizardCompletedByUserId" IS 'Discord user ID who completed the wizard';
COMMENT ON COLUMN "GuildConfigs"."WizardSkipped" IS 'Whether the wizard was explicitly skipped';
COMMENT ON COLUMN "GuildConfigs"."HasBasicSetup" IS 'Quick check if guild has any basic configuration';