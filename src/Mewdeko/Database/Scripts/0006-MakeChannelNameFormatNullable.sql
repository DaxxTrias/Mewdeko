-- Make ChannelNameFormat nullable in PanelButtons
ALTER TABLE "PanelButtons"
    ALTER COLUMN "ChannelNameFormat" DROP NOT NULL;

-- Make ChannelNameFormat nullable in SelectMenuOptions (if it exists there too)
ALTER TABLE "SelectMenuOptions"
    ALTER COLUMN "ChannelNameFormat" DROP NOT NULL;