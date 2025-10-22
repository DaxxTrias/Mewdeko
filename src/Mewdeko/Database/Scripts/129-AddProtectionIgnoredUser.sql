-- Migration: 125-AddProtectionIgnoredUser.sql
CREATE TABLE IF NOT EXISTS "ProtectionIgnoredUser"
(
    "Id"        INTEGER PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    "GuildId"   NUMERIC(20,0) NOT NULL,
    "UserId"    NUMERIC(20,0) NOT NULL,
    "DateAdded" TIMESTAMP(6) NULL,
    "Note"      TEXT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ProtectionIgnoredUser_GuildId" ON "ProtectionIgnoredUser" ("GuildId");
CREATE UNIQUE INDEX IF NOT EXISTS "UX_ProtectionIgnoredUser_Guild_User" ON "ProtectionIgnoredUser" ("GuildId","UserId");

