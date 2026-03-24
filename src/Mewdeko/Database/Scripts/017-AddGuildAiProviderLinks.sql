-- Add normalized AI provider links table for multi-provider support
CREATE TABLE IF NOT EXISTS public."GuildAiProviderLinks"
(
    "Id"           serial PRIMARY KEY,
    "GuildId"      numeric(20, 0)                         NOT NULL,
    "Provider"     integer                                NOT NULL,
    "ApiKey"       text                                   NOT NULL DEFAULT '',
    "DefaultModel" text,
    "IsEnabled"    boolean                                NOT NULL DEFAULT true,
    "IsDefault"    boolean                                NOT NULL DEFAULT false,
    "DateAdded"    timestamp(6) without time zone         DEFAULT NOW(),
    "DateUpdated"  timestamp(6) without time zone         DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_GuildAiProviderLinks_GuildId_Provider"
    ON public."GuildAiProviderLinks" ("GuildId", "Provider");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_GuildAiProviderLinks_GuildId_IsDefault_True"
    ON public."GuildAiProviderLinks" ("GuildId")
    WHERE "IsDefault" = true;

CREATE INDEX IF NOT EXISTS "IX_GuildAiProviderLinks_GuildId"
    ON public."GuildAiProviderLinks" ("GuildId");

-- Backfill existing single-provider guild config into normalized links table.
INSERT INTO public."GuildAiProviderLinks"
(
    "GuildId",
    "Provider",
    "ApiKey",
    "DefaultModel",
    "IsEnabled",
    "IsDefault",
    "DateAdded",
    "DateUpdated"
)
SELECT
    g."GuildId",
    g."Provider",
    COALESCE(g."ApiKey", ''),
    g."Model",
    true,
    true,
    COALESCE(g."DateAdded", NOW()),
    NOW()
FROM public."GuildAiConfig" g
WHERE COALESCE(g."ApiKey", '') <> ''
ON CONFLICT ("GuildId", "Provider")
DO UPDATE SET
    "ApiKey" = EXCLUDED."ApiKey",
    "DefaultModel" = COALESCE(EXCLUDED."DefaultModel", public."GuildAiProviderLinks"."DefaultModel"),
    "IsEnabled" = true,
    "IsDefault" = true,
    "DateUpdated" = NOW();
