-- Add scheduled polls table
CREATE TABLE IF NOT EXISTS scheduled_polls
(
    id               SERIAL PRIMARY KEY,
    guild_id         BIGINT    NOT NULL,
    channel_id       BIGINT    NOT NULL,
    creator_id       BIGINT    NOT NULL,
    question         TEXT      NOT NULL,
    options          TEXT      NOT NULL,
    poll_type        INTEGER   NOT NULL DEFAULT 0,
    settings         TEXT,
    scheduled_for    TIMESTAMP NOT NULL,
    duration_minutes INTEGER,
    scheduled_at     TIMESTAMP NOT NULL DEFAULT NOW(),
    is_executed      BOOLEAN   NOT NULL DEFAULT FALSE,
    executed_at      TIMESTAMP,
    created_poll_id  INTEGER,
    is_cancelled     BOOLEAN   NOT NULL DEFAULT FALSE,
    cancelled_at     TIMESTAMP,
    cancelled_by     BIGINT
);

-- Add indexes for performance
CREATE INDEX IF NOT EXISTS idx_scheduled_polls_guild_id ON scheduled_polls (guild_id);
CREATE INDEX IF NOT EXISTS idx_scheduled_polls_scheduled_for ON scheduled_polls (scheduled_for);
CREATE INDEX IF NOT EXISTS idx_scheduled_polls_pending ON scheduled_polls (is_executed, is_cancelled, scheduled_for) WHERE is_executed = FALSE AND is_cancelled = FALSE;

-- Add foreign key constraint if polls table exists
DO
$$
    BEGIN
        IF EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'polls') THEN
            ALTER TABLE scheduled_polls
                ADD CONSTRAINT fk_scheduled_polls_created_poll_id
                    FOREIGN KEY (created_poll_id) REFERENCES polls (id) ON DELETE SET NULL;
        END IF;
    END
$$;