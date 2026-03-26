-- Run this once on your BlueHost MySQL database.
CREATE TABLE IF NOT EXISTS level_scores (
  id           INT AUTO_INCREMENT PRIMARY KEY,
  level_id     VARCHAR(64)     NOT NULL,
  steam_id     BIGINT UNSIGNED NOT NULL,
  steam_name   VARCHAR(128)    NOT NULL DEFAULT '',
  score        INT UNSIGNED    NOT NULL,
  score_date   DATE            NOT NULL,
  submitted_at DATETIME        NOT NULL DEFAULT UTC_TIMESTAMP(),
  UNIQUE KEY uq_player_level_date (steam_id, level_id, score_date),
  INDEX idx_level_date_score (level_id, score_date, score)
);
