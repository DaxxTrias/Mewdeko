CREATE TABLE IF NOT EXISTS "DeletedMessageLogSettings"
(
    "Id"             SERIAL PRIMARY KEY,
    "GuildId"        NUMERIC(20, 0)              NOT NULL,
    "ChannelId"      NUMERIC(20, 0)              NULL,
    "Enabled"        BOOLEAN                     NOT NULL DEFAULT FALSE,
    "MaxAgeMinutes"  INTEGER                     NOT NULL DEFAULT 10,
    "DateAdded"      TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateModified"   TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UX_DeletedMessageLogSettings_GuildId" UNIQUE ("GuildId")
);

CREATE INDEX IF NOT EXISTS "IX_DeletedMessageLogSettings_GuildId"
    ON "DeletedMessageLogSettings" ("GuildId");
