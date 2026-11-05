-- Additional channels where guild AI prompts are accepted.
-- GuildAiConfig.ChannelId remains the primary channel; 0 means all channels.
CREATE TABLE IF NOT EXISTS public."GuildAiChannels"
(
    "Id"        serial PRIMARY KEY,
    "GuildId"   numeric(20, 0)                 NOT NULL,
    "ChannelId" numeric(20, 0)                 NOT NULL,
    "DateAdded" timestamp(6) without time zone DEFAULT NOW(),
    UNIQUE ("GuildId", "ChannelId")
);

CREATE INDEX IF NOT EXISTS "IX_GuildAiChannels_GuildId"
    ON public."GuildAiChannels" ("GuildId");
