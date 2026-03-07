-- Purrbricks Community Levels Schema
-- Run once via phpMyAdmin on your Bluehost MySQL database.

CREATE TABLE IF NOT EXISTS community_levels (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  steam_id      VARCHAR(32)      NOT NULL,
  steam_name    VARCHAR(128)     NOT NULL,
  title         VARCHAR(64)      NOT NULL,
  description   VARCHAR(256)     DEFAULT '',
  json_data     MEDIUMTEXT       NOT NULL,
  brick_count   INT              DEFAULT 0,
  play_count    INT              DEFAULT 0,
  report_count  INT              DEFAULT 0,
  is_published  TINYINT(1)       DEFAULT 1,
  average_rating DECIMAL(3,2)   DEFAULT 0.00,
  rating_count  INT              DEFAULT 0,
  published_at  DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_rating  (average_rating),
  INDEX idx_plays   (play_count),
  INDEX idx_date    (published_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS community_ratings (
  level_id  INT          NOT NULL,
  steam_id  VARCHAR(32)  NOT NULL,
  rating    TINYINT      NOT NULL,   -- 1-5
  rated_at  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (level_id, steam_id),
  INDEX idx_level (level_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
