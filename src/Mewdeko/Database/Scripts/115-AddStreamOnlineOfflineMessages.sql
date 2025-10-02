-- Rename existing Message field to OnlineMessage and add OfflineMessage field for per-streamer online/offline customization
ALTER TABLE "FollowedStreams"
    RENAME COLUMN "Message" TO "OnlineMessage";

ALTER TABLE "FollowedStreams"
    ADD COLUMN "OfflineMessage" text;