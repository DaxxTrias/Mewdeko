-- Add table for reaction-based reputation configuration
CREATE TABLE IF NOT EXISTS "RepReactionConfig"
(
    "Id"                   SERIAL PRIMARY KEY,
    "GuildId"              NUMERIC(20, 0)                 NOT NULL,
    "EmojiName"            VARCHAR(255)                   NOT NULL,
    "EmojiId"              NUMERIC(20, 0)                 NULL,
    "RepAmount"            INTEGER                        NOT NULL DEFAULT 1,
    "RepType"              VARCHAR(100)                   NOT NULL DEFAULT 'standard',
    "CooldownMinutes"      INTEGER                        NOT NULL DEFAULT 60,
    "RequiredRoleId"       NUMERIC(20, 0)                 NULL,
    "MinMessageAgeMinutes" INTEGER                        NOT NULL DEFAULT 0,
    "MinMessageLength"     INTEGER                        NOT NULL DEFAULT 0,
    "IsEnabled"            BOOLEAN                        NOT NULL DEFAULT TRUE,
    "AllowedChannels"      TEXT                           NULL,
    "AllowedReceiverRoles" TEXT                           NULL,
    "DateAdded"            TIMESTAMP(6) WITHOUT TIME ZONE NULL
);