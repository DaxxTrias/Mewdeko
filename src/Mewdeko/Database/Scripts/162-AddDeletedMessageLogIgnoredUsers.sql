CREATE TABLE IF NOT EXISTS "DeletedMessageLogIgnoredUsers"
(
    "Id"           SERIAL PRIMARY KEY,
    "GuildId"      NUMERIC(20, 0)              NOT NULL,
    "UserId"       NUMERIC(20, 0)              NOT NULL,
    "Note"         TEXT                        NULL,
    "DateAdded"    TIMESTAMP WITHOUT TIME ZONE NULL,
    "DateModified" TIMESTAMP WITHOUT TIME ZONE NULL,
    CONSTRAINT "UX_DeletedMessageLogIgnoredUsers_GuildId_UserId" UNIQUE ("GuildId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_DeletedMessageLogIgnoredUsers_GuildId"
    ON "DeletedMessageLogIgnoredUsers" ("GuildId");
