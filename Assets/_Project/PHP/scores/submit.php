<?php
require_once 'db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') { errorResponse('POST required', 405); }

$body      = json_decode(file_get_contents('php://input'), true) ?? [];
$steamId   = isset($body['steamId'])   ? (string)$body['steamId']                   : '';
$steamName = isset($body['steamName']) ? substr((string)$body['steamName'], 0, 128) : '';
$levelId   = isset($body['levelId'])   ? substr((string)$body['levelId'], 0, 64)    : '';
$levelName = isset($body['levelName']) ? substr((string)$body['levelName'], 0, 128) : '';
$score     = isset($body['score'])     ? (int)$body['score']                        : 0;

if (!$steamId || !$levelId || $score <= 0) {
    errorResponse('steamId, levelId, and score > 0 required');
}

$db = getDb();

// Upsert: one row per (player, level, UTC day) — keep best score for the day.
$db->prepare("
    INSERT INTO level_scores (level_id, level_name, steam_id, steam_name, score, score_date)
    VALUES (:levelId, :levelName, :steamId, :steamName, :score, UTC_DATE())
    ON DUPLICATE KEY UPDATE
        score        = GREATEST(score, VALUES(score)),
        level_name   = VALUES(level_name),
        steam_name   = VALUES(steam_name),
        submitted_at = UTC_TIMESTAMP()
")->execute([
    ':levelId'   => $levelId,
    ':levelName' => $levelName,
    ':steamId'   => $steamId,
    ':steamName' => $steamName,
    ':score'     => $score,
]);

// Daily rank: count distinct players with a higher score today for this level + 1.
$stmt = $db->prepare("
    SELECT COUNT(DISTINCT steam_id) + 1 AS rnk
    FROM level_scores
    WHERE level_id = :levelId
      AND score_date = UTC_DATE()
      AND score > :score
");
$stmt->execute([':levelId' => $levelId, ':score' => $score]);
$dailyRank = (int)$stmt->fetchColumn();

// Weekly rank: count distinct players whose MAX(score) this week exceeds the submitted score + 1.
$stmt = $db->prepare("
    SELECT COUNT(*) + 1 AS rnk
    FROM (
        SELECT steam_id, MAX(score) AS best
        FROM level_scores
        WHERE level_id  = :levelId
          AND YEARWEEK(score_date, 0) = YEARWEEK(UTC_DATE(), 0)
        GROUP BY steam_id
    ) w
    WHERE w.best > :score
");
$stmt->execute([':levelId' => $levelId, ':score' => $score]);
$weeklyRank = (int)$stmt->fetchColumn();

jsonResponse([
    'success'    => true,
    'dailyRank'  => $dailyRank  <= 3 ? $dailyRank  : 0,
    'weeklyRank' => $weeklyRank <= 3 ? $weeklyRank : 0,
]);
