CREATE TABLE IF NOT EXISTS "TicketActionLogs"
(
    "Id"
    SERIAL
    PRIMARY
    KEY,
    "TicketId"
    INTEGER
    NOT
    NULL
    REFERENCES
    "Tickets"
(
    "Id"
) ON DELETE CASCADE,
    "GuildId" NUMERIC
(
    20,
    0
) NOT NULL,
    "ChannelId" NUMERIC
(
    20,
    0
) NOT NULL,
    "UserId" NUMERIC
(
    20,
    0
) NOT NULL,
    "Action" VARCHAR
(
    50
) NOT NULL, -- 'Created', 'Claimed', 'Unclaimed', 'Closed', 'Archived', 'Deleted', etc.
    "Details" JSONB, -- Additional action-specific data
    "Timestamp" TIMESTAMP
(
    6
)
  WITHOUT TIME ZONE NOT NULL DEFAULT
(
    NOW
(
) AT TIME ZONE 'utc')
    );

-- Index for finding logs by ticket
CREATE INDEX IF NOT EXISTS "IX_TicketActionLogs_TicketId"
    ON "TicketActionLogs" ("TicketId", "Timestamp");

-- Index for finding logs by guild and timeframe
CREATE INDEX IF NOT EXISTS "IX_TicketActionLogs_Guild_Timestamp"
    ON "TicketActionLogs" ("GuildId", "Timestamp");

-- Index for finding logs by action type
CREATE INDEX IF NOT EXISTS "IX_TicketActionLogs_Action"
    ON "TicketActionLogs" ("Action", "Timestamp");