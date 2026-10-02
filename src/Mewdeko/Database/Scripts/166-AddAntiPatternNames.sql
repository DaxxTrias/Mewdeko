CREATE TABLE IF NOT EXISTS "AntiPatternName"
(
    "Id"                   SERIAL PRIMARY KEY,
    "AntiPatternSettingId" INTEGER   NOT NULL,
    "OriginalName"         TEXT      NOT NULL,
    "NormalizedName"       TEXT      NOT NULL,
    "CheckUsername"        BOOLEAN   NOT NULL DEFAULT TRUE,
    "CheckDisplayName"     BOOLEAN   NOT NULL DEFAULT TRUE,
    "DateAdded"            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("AntiPatternSettingId") REFERENCES "AntiPatternSetting" ("Id") ON DELETE CASCADE,
    UNIQUE ("AntiPatternSettingId", "NormalizedName")
);

CREATE INDEX IF NOT EXISTS "IX_AntiPatternName_AntiPatternSettingId"
    ON "AntiPatternName" ("AntiPatternSettingId");
