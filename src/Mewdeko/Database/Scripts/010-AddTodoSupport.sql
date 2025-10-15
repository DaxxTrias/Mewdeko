-- Add Todo Support Tables
-- This migration adds support for unlimited todo lists per user/server with granular permissions

-- Todo Lists table - supports both personal and server-wide lists
CREATE TABLE IF NOT EXISTS "TodoLists"
(
    "Id"           SERIAL PRIMARY KEY,
    "Name"         text                           NOT NULL,
    "Description"  text                           NULL,
    "GuildId"      numeric(20, 0)                 NOT NULL,
    "OwnerId"      numeric(20, 0)                 NOT NULL,                   -- User who created the list
    "IsServerList" boolean                        NOT NULL DEFAULT FALSE,     -- Personal (false) or Server-wide (true) list
    "IsPublic"     boolean                        NOT NULL DEFAULT TRUE,      -- Can others view this list
    "Color"        text                           NULL     DEFAULT '#7289da', -- Embed color for the list
    "CreatedAt"    timestamp(6) without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt"    timestamp(6) without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Todo Items table - individual tasks within lists
CREATE TABLE IF NOT EXISTS "TodoItems"
(
    "Id"           SERIAL PRIMARY KEY,
    "TodoListId"   integer                        NOT NULL REFERENCES "TodoLists" ("Id") ON DELETE CASCADE,
    "Title"        text                           NOT NULL,
    "Description"  text                           NULL,
    "IsCompleted"  boolean                        NOT NULL DEFAULT FALSE,
    "Priority"     integer                        NOT NULL DEFAULT 1,    -- 1=Low, 2=Medium, 3=High, 4=Critical
    "DueDate"      timestamp(6) without time zone NULL,
    "CreatedAt"    timestamp(6) without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CompletedAt"  timestamp(6) without time zone NULL,
    "CreatedBy"    numeric(20, 0)                 NOT NULL,              -- User who created this item
    "CompletedBy"  numeric(20, 0)                 NULL,                  -- User who completed this item
    "Tags"         text[]                         NULL     DEFAULT '{}', -- Array of tag strings
    "ReminderTime" timestamp(6) without time zone NULL,
    "Position"     integer                        NOT NULL DEFAULT 0     -- For manual ordering of items
);

-- Todo List Permissions table - granular access control for server lists
CREATE TABLE IF NOT EXISTS "TodoListPermissions"
(
    "Id"            SERIAL PRIMARY KEY,
    "TodoListId"    integer                        NOT NULL REFERENCES "TodoLists" ("Id") ON DELETE CASCADE,
    "UserId"        numeric(20, 0)                 NOT NULL,               -- User being granted permission
    "CanView"       boolean                        NOT NULL DEFAULT TRUE,
    "CanAdd"        boolean                        NOT NULL DEFAULT FALSE,
    "CanEdit"       boolean                        NOT NULL DEFAULT FALSE, -- Edit items created by others
    "CanComplete"   boolean                        NOT NULL DEFAULT FALSE, -- Complete items created by others
    "CanDelete"     boolean                        NOT NULL DEFAULT FALSE, -- Delete items created by others
    "CanManageList" boolean                        NOT NULL DEFAULT FALSE, -- Edit list settings, permissions
    "GrantedBy"     numeric(20, 0)                 NOT NULL,               -- User who granted these permissions
    "GrantedAt"     timestamp(6) without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for performance
CREATE INDEX "IX_TodoLists_GuildId_OwnerId" ON "TodoLists" ("GuildId", "OwnerId");
CREATE INDEX "IX_TodoLists_GuildId_IsServerList" ON "TodoLists" ("GuildId", "IsServerList");
CREATE INDEX "IX_TodoItems_TodoListId" ON "TodoItems" ("TodoListId");
CREATE INDEX "IX_TodoItems_DueDate" ON "TodoItems" ("DueDate") WHERE "DueDate" IS NOT NULL;
CREATE INDEX "IX_TodoItems_ReminderTime" ON "TodoItems" ("ReminderTime") WHERE "ReminderTime" IS NOT NULL;
CREATE INDEX "IX_TodoItems_Priority_IsCompleted" ON "TodoItems" ("Priority", "IsCompleted");
CREATE INDEX "IX_TodoListPermissions_TodoListId_UserId" ON "TodoListPermissions" ("TodoListId", "UserId");

-- Unique constraint to prevent duplicate permissions for same user on same list
CREATE UNIQUE INDEX "IX_TodoListPermissions_Unique" ON "TodoListPermissions" ("TodoListId", "UserId");

-- Unique constraint for list names within the same guild/owner scope
CREATE UNIQUE INDEX "IX_TodoLists_Name_Unique" ON "TodoLists" ("GuildId", "OwnerId", "Name", "IsServerList");