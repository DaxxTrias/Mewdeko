CREATE TABLE IF NOT EXISTS "SecurityAuditSettings"
(
    "Id"                      SERIAL PRIMARY KEY,
    "GuildId"                 NUMERIC(20, 0)              NOT NULL,
    "Enabled"                 BOOLEAN                     NOT NULL DEFAULT TRUE,
    "AlertChannelId"          NUMERIC(20, 0)              NULL,
    "DmBotOwnersOnCritical"   BOOLEAN                     NOT NULL DEFAULT FALSE,
    "PingRoleId"              NUMERIC(20, 0)              NULL,
    "LastSeenAuditLogId"      NUMERIC(20, 0)              NULL,
    "DateAdded"               TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_SecurityAuditSettings_GuildId"
    ON "SecurityAuditSettings" ("GuildId");

CREATE INDEX IF NOT EXISTS "IX_SecurityAuditSettings_Enabled"
    ON "SecurityAuditSettings" ("Enabled");
