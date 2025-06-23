-- Make ChannelNameFormat nullable in PanelButtons
DO
$$
BEGIN
    IF
EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'PanelButtons' AND column_name = 'ChannelNameFormat'
        AND is_nullable = 'NO'
    ) THEN
ALTER TABLE "PanelButtons"
    ALTER COLUMN "ChannelNameFormat" DROP NOT NULL;
END IF;
END $$;

-- Make ChannelNameFormat nullable in SelectMenuOptions (if it exists there too)
DO
$$
BEGIN
    IF
EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'SelectMenuOptions' AND column_name = 'ChannelNameFormat'
        AND is_nullable = 'NO'
    ) THEN
ALTER TABLE "SelectMenuOptions"
    ALTER COLUMN "ChannelNameFormat" DROP NOT NULL;
END IF;
END $$;