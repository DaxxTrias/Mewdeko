-- Add LevelUpInfoShown field to track if user has seen the first-time level-up info message
ALTER TABLE "DiscordUser"
    ADD COLUMN "LevelUpInfoShown" boolean NOT NULL DEFAULT false;