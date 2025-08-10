-- Add WebSearchEnabled column to GuildAiConfig table
ALTER TABLE public."GuildAiConfig"
    ADD COLUMN IF NOT EXISTS "WebSearchEnabled" boolean NOT NULL DEFAULT false;