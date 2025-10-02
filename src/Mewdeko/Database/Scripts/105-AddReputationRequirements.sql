-- Create reputation command requirements table
CREATE TABLE IF NOT EXISTS "RepCommandRequirements"
(
    "Id"                 SERIAL PRIMARY KEY,
    "GuildId"            NUMERIC(20, 0)                 NOT NULL,
    "CommandName"        VARCHAR(200)                   NOT NULL,
    "MinReputation"      INTEGER                        NOT NULL DEFAULT 0,
    "RequiredRepType"    VARCHAR(100),
    "RestrictedChannels" TEXT,
    "DenialMessage"      TEXT,
    "IsActive"           BOOLEAN                        NOT NULL DEFAULT TRUE,
    "BypassRoles"        TEXT,
    "ShowInHelp"         BOOLEAN                        NOT NULL DEFAULT TRUE,
    "DateAdded"          TIMESTAMP(6) WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE ("GuildId", "CommandName")
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS "IX_RepCommandRequirements_GuildId" ON "RepCommandRequirements" ("GuildId");
CREATE INDEX IF NOT EXISTS "IX_RepCommandRequirements_CommandName" ON "RepCommandRequirements" ("CommandName");
CREATE INDEX IF NOT EXISTS "IX_RepCommandRequirements_IsActive" ON "RepCommandRequirements" ("IsActive");