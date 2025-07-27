-- Add HideWebSearchMessages column to GuildAiConfig table
ALTER TABLE public."GuildAiConfig"
    ADD COLUMN IF NOT EXISTS "HideWebSearchMessages" boolean NOT NULL DEFAULT true;