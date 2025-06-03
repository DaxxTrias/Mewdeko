-- Update existing tickets to not be deleted by default
UPDATE "Tickets"
SET "IsDeleted" = FALSE
WHERE "IsDeleted" IS NULL;

-- Update existing buttons with sensible defaults
UPDATE "PanelButtons"
SET
    "DeleteOnClose" = FALSE,
    "LockOnClose" = TRUE,
    "RenameOnClose" = TRUE,
    "RemoveCreatorOnClose" = TRUE,
    "DeleteDelay" = INTERVAL '5 minutes',
    "LockOnArchive" = TRUE,
    "RenameOnArchive" = TRUE,
    "RemoveCreatorOnArchive" = FALSE,
    "AutoArchiveOnClose" = FALSE
WHERE
    "DeleteOnClose" IS NULL
   OR "LockOnClose" IS NULL
   OR "RenameOnClose" IS NULL;

-- Update existing select menu options with sensible defaults
UPDATE "SelectMenuOptions"
SET
    "DeleteOnClose" = FALSE,
    "LockOnClose" = TRUE,
    "RenameOnClose" = TRUE,
    "RemoveCreatorOnClose" = TRUE,
    "DeleteDelay" = INTERVAL '5 minutes',
    "LockOnArchive" = TRUE,
    "RenameOnArchive" = TRUE,
    "RemoveCreatorOnArchive" = FALSE,
    "AutoArchiveOnClose" = FALSE
WHERE
    "DeleteOnClose" IS NULL
   OR "LockOnClose" IS NULL
   OR "RenameOnClose" IS NULL;

-- Update existing guild settings with sensible defaults
UPDATE "GuildTicketSettings"
SET
    "DeleteTicketsOnClose" = FALSE,
    "LockTicketsOnClose" = TRUE,
    "RenameTicketsOnClose" = TRUE,
    "RemoveCreatorOnClose" = TRUE,
    "DeleteDelay" = INTERVAL '5 minutes',
    "LockTicketsOnArchive" = TRUE,
    "RenameTicketsOnArchive" = TRUE,
    "RemoveCreatorOnArchive" = FALSE,
    "AutoArchiveOnClose" = FALSE
WHERE
    "DeleteTicketsOnClose" IS NULL
   OR "LockTicketsOnClose" IS NULL
   OR "RenameTicketsOnClose" IS NULL;