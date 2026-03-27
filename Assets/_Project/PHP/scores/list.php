<?php
require_once 'db.php';

$levelId = isset($_GET['levelId']) ? substr((string)$_GET['levelId'], 0, 64) : '';
$scope   = isset($_GET['scope'])   ? (string)$_GET['scope']                  : '';
$limit   = min(max((int)($_GET['limit'] ?? 10), 1), 50);
$steamId = isset($_GET['steamId']) ? (string)$_GET['steamId']                : '';

if (!$levelId || !in_array($scope, ['daily', 'weekly', 'alltime'], true)) {
    errorResponse('levelId and scope (daily|weekly|alltime) required');
}

$db = getDb();

if ($scope === 'daily') {
    $stmt = $db->prepare("
        SELECT steam_id, steam_name, score
        FROM level_scores
        WHERE level_id  = :levelId
          AND score_date = UTC_DATE()
        ORDER BY score DESC
        LIMIT :limit
    ");
} elseif ($scope === 'weekly') {
    // Weekly: best score per player within the current UTC week (Sunday start).
    $stmt = $db->prepare("
        SELECT steam_id, ANY_VALUE(steam_name) AS steam_name, MAX(score) AS score
        FROM level_scores
        WHERE level_id = :levelId
          AND YEARWEEK(score_date, 0) = YEARWEEK(UTC_DATE(), 0)
        GROUP BY steam_id
        ORDER BY score DESC
        LIMIT :limit
    ");
} else {
    // All time: best score ever per player.
    $stmt = $db->prepare("
        SELECT steam_id, ANY_VALUE(steam_name) AS steam_name, MAX(score) AS score
        FROM level_scores
        WHERE level_id = :levelId
        GROUP BY steam_id
        ORDER BY score DESC
        LIMIT :limit
    ");
}

$stmt->bindValue(':levelId', $levelId);
$stmt->bindValue(':limit',   $limit, PDO::PARAM_INT);
$stmt->execute();
$rows = $stmt->fetchAll();

$scores = [];
foreach ($rows as $i => $row) {
    $scores[] = [
        'rank'      => $i + 1,
        'steamId'   => $row['steam_id'],
        'steamName' => $row['steam_name'],
        'score'     => (int)$row['score'],
    ];
}

// Player's own rank (0 = not ranked / no score this period).
$playerRank = 0;
if ($steamId) {
    // Check if already in the top-N results.
    foreach ($scores as $entry) {
        if ((string)$entry['steamId'] === (string)$steamId) {
            $playerRank = $entry['rank'];
            break;
        }
    }

    if ($playerRank === 0) {
        // Not in top-N: compute rank separately.
        if ($scope === 'daily') {
            $s = $db->prepare("SELECT score FROM level_scores WHERE level_id=:l AND steam_id=:s AND score_date=UTC_DATE()");
            $s->execute([':l' => $levelId, ':s' => $steamId]);
            $myScore = $s->fetchColumn();
            if ($myScore !== false) {
                $s2 = $db->prepare("SELECT COUNT(DISTINCT steam_id)+1 FROM level_scores WHERE level_id=:l AND score_date=UTC_DATE() AND score>:sc");
                $s2->execute([':l' => $levelId, ':sc' => $myScore]);
                $playerRank = (int)$s2->fetchColumn();
            }
        } elseif ($scope === 'weekly') {
            $s = $db->prepare("SELECT MAX(score) FROM level_scores WHERE level_id=:l AND steam_id=:s AND YEARWEEK(score_date,0)=YEARWEEK(UTC_DATE(),0)");
            $s->execute([':l' => $levelId, ':s' => $steamId]);
            $myBest = $s->fetchColumn();
            if ($myBest !== false && $myBest !== null) {
                $s2 = $db->prepare("SELECT COUNT(*)+1 FROM (SELECT steam_id,MAX(score) AS best FROM level_scores WHERE level_id=:l AND YEARWEEK(score_date,0)=YEARWEEK(UTC_DATE(),0) GROUP BY steam_id) w WHERE w.best>:sc");
                $s2->execute([':l' => $levelId, ':sc' => $myBest]);
                $playerRank = (int)$s2->fetchColumn();
            }
        } else {
            $s = $db->prepare("SELECT MAX(score) FROM level_scores WHERE level_id=:l AND steam_id=:s");
            $s->execute([':l' => $levelId, ':s' => $steamId]);
            $myBest = $s->fetchColumn();
            if ($myBest !== false && $myBest !== null) {
                $s2 = $db->prepare("SELECT COUNT(*)+1 FROM (SELECT steam_id,MAX(score) AS best FROM level_scores WHERE level_id=:l GROUP BY steam_id) w WHERE w.best>:sc");
                $s2->execute([':l' => $levelId, ':sc' => $myBest]);
                $playerRank = (int)$s2->fetchColumn();
            }
        }
    }
}

jsonResponse(['scores' => $scores, 'playerRank' => $playerRank]);
