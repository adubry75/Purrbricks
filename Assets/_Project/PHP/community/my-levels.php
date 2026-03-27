<?php
// GET /community/my-levels.php?steamId=<steamId>
// Returns ALL levels belonging to this Steam user (published and private), newest first.
// Response: {levels: [...], total: N}

ini_set('display_errors', '0');
ini_set('log_errors', '1');

require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'GET') errorResponse('GET required', 405);

$steamId = trim($_GET['steamId'] ?? '');
if (!$steamId) errorResponse('steamId required');

try {
    $db   = getDb();
    $stmt = $db->prepare("
        SELECT id, level_guid, steam_name, title, description, brick_count,
               play_count, average_rating, rating_count, published_at, is_published,
               json_data
        FROM community_levels
        WHERE steam_id = ?
        ORDER BY id DESC
    ");
    $stmt->execute([substr($steamId, 0, 32)]);
    $rows = $stmt->fetchAll();

    foreach ($rows as &$row) {
        $row['levelGuid']     = $row['level_guid'];
        $row['steamName']     = $row['steam_name'];
        $row['averageRating'] = (float)$row['average_rating'];
        $row['ratingCount']   = (int)$row['rating_count'];
        $row['playCount']     = (int)$row['play_count'];
        $row['brickCount']    = (int)$row['brick_count'];
        $row['publishedAt']   = $row['published_at'];
        $row['jsonData']      = $row['json_data'];
        unset($row['level_guid'], $row['steam_name'], $row['average_rating'],
              $row['rating_count'], $row['play_count'], $row['brick_count'],
              $row['published_at'], $row['json_data']);
    }
    unset($row);

    jsonResponse(['levels' => $rows, 'total' => count($rows)]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
