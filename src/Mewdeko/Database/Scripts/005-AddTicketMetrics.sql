CREATE
MATERIALIZED VIEW IF NOT EXISTS "TicketStatistics" AS
SELECT "GuildId",
       COUNT(*)                         as "TotalTickets",
       COUNT(*)                            FILTER (WHERE "ClosedAt" IS NULL AND "IsDeleted" = FALSE) as "OpenTickets", COUNT(*) FILTER (WHERE "ClosedAt" IS NOT NULL) as "ClosedTickets", COUNT(*) FILTER (WHERE "IsArchived" = TRUE) as "ArchivedTickets", COUNT(*) FILTER (WHERE "IsDeleted" = TRUE) as "DeletedTickets", AVG(EXTRACT(EPOCH FROM ("ClosedAt" - "CreatedAt")) / 3600) as "AvgResolutionHours",
       DATE_TRUNC('month', "CreatedAt") as "Month"
FROM "Tickets"
GROUP BY "GuildId", DATE_TRUNC('month', "CreatedAt");

-- Index for fast lookups on the materialized view
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TicketStatistics_Guild_Month"
    ON "TicketStatistics" ("GuildId", "Month");

-- Create function to refresh statistics (call this periodically)
CREATE
OR REPLACE FUNCTION refresh_ticket_statistics()
    RETURNS VOID AS $$
BEGIN
    REFRESH
MATERIALIZED VIEW CONCURRENTLY "TicketStatistics";
END;
$$
LANGUAGE plpgsql;