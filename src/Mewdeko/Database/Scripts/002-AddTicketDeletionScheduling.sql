CREATE TABLE IF NOT EXISTS "ScheduledTicketDeletions"
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
    "ScheduledAt" TIMESTAMP
(
    6
)
  WITHOUT TIME ZONE NOT NULL DEFAULT
(
    NOW
(
) AT TIME ZONE 'utc'),
    "ExecuteAt" TIMESTAMP
(
    6
)
  WITHOUT TIME ZONE NOT NULL,
    "IsProcessed" BOOLEAN NOT NULL DEFAULT FALSE,
    "ProcessedAt" TIMESTAMP
(
    6
)
  WITHOUT TIME ZONE,
    "FailureReason" TEXT,
    "RetryCount" INTEGER NOT NULL DEFAULT 0
    );

-- Index for finding scheduled deletions ready to execute
CREATE INDEX IF NOT EXISTS "IX_ScheduledTicketDeletions_ExecuteAt"
    ON "ScheduledTicketDeletions" ("ExecuteAt", "IsProcessed")
    WHERE "IsProcessed" = FALSE;

-- Index for cleanup of old processed records
CREATE INDEX IF NOT EXISTS "IX_ScheduledTicketDeletions_ProcessedAt"
    ON "ScheduledTicketDeletions" ("ProcessedAt")
    WHERE "IsProcessed" = TRUE;