-- Add table for custom reputation types
CREATE TABLE IF NOT EXISTS "RepCustomType"
(
    "Id"                SERIAL PRIMARY KEY,
    "GuildId"           NUMERIC(20, 0)                 NOT NULL,
    "TypeName"          VARCHAR(100)                   NOT NULL,
    "DisplayName"       VARCHAR(200)                   NOT NULL,
    "Description"       TEXT                           NULL,
    "EmojiIcon"         VARCHAR(50)                    NULL,
    "Color"             VARCHAR(7)                     NULL,
    "IsActive"          BOOLEAN                        NOT NULL DEFAULT TRUE,
    "Multiplier"        DECIMAL(5, 2)                  NOT NULL DEFAULT 1.0,
    "CountsTowardTotal" BOOLEAN                        NOT NULL DEFAULT TRUE,
    "DateAdded"         TIMESTAMP(6) WITHOUT TIME ZONE NULL
);

-- Add table for user custom reputation amounts
CREATE TABLE IF NOT EXISTS "UserCustomReputation"
(
    "Id"           SERIAL PRIMARY KEY,
    "UserId"       NUMERIC(20, 0)                 NOT NULL,
    "GuildId"      NUMERIC(20, 0)                 NOT NULL,
    "CustomTypeId" INTEGER                        NOT NULL,
    "Amount"       INTEGER                        NOT NULL DEFAULT 0,
    "LastUpdated"  TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL,
    "DateAdded"    TIMESTAMP(6) WITHOUT TIME ZONE NULL,
    FOREIGN KEY ("CustomTypeId") REFERENCES "RepCustomType" ("Id") ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS "idx_repcustomtype_guild" ON "RepCustomType" ("GuildId");
CREATE INDEX IF NOT EXISTS "idx_repcustomtype_guild_type" ON "RepCustomType" ("GuildId", "TypeName");
CREATE INDEX IF NOT EXISTS "idx_usercustomrep_user_guild" ON "UserCustomReputation" ("UserId", "GuildId");
CREATE INDEX IF NOT EXISTS "idx_usercustomrep_guild_type" ON "UserCustomReputation" ("GuildId", "CustomTypeId");