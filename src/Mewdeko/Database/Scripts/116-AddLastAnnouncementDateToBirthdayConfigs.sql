-- Add LastAnnouncementDate field to BirthdayConfigs table
ALTER TABLE "BirthdayConfigs" 
ADD COLUMN "LastAnnouncementDate" DATE;

-- Set default value for existing records (yesterday to ensure announcements run today)
UPDATE "BirthdayConfigs"
SET "LastAnnouncementDate" = CURRENT_DATE - INTERVAL '1 day'
WHERE "LastAnnouncementDate" IS NULL;
