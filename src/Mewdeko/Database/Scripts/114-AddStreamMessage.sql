-- Add StreamMessage field to GuildConfigs for custom stream notification messages
ALTER TABLE "GuildConfigs"
    ADD COLUMN "StreamMessage" text;