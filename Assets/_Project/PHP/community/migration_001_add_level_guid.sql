-- Migration 001: add level_guid and updated_at to community_levels
-- Run once in phpMyAdmin (or via CLI) against your Bluehost database.
-- Safe to run on an empty table; also handles pre-existing rows.

-- Step 1: Add level_guid column (no unique key yet so existing rows can be NULL/empty)
ALTER TABLE community_levels
    ADD COLUMN level_guid VARCHAR(64) NOT NULL DEFAULT '' AFTER id,
    ADD COLUMN updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP AFTER published_at;

-- Step 2: Give any pre-existing rows a server-generated GUID so the unique key can be applied
--         (These old rows won't match any client GUID, so they won't be "updatable" by anyone,
--          but they stay visible in the community browser.)
UPDATE community_levels
    SET level_guid = REPLACE(UUID(), '-', '')
    WHERE level_guid = '';

-- Step 3: Now it's safe to add the unique index
ALTER TABLE community_levels
    ADD UNIQUE KEY uidx_level_guid (level_guid);

-- Step 4: Also add an index on steam_id for the my-levels.php endpoint
ALTER TABLE community_levels
    ADD INDEX idx_steam_id (steam_id);
