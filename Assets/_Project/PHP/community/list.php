<?php
// GET /community/list.php?sort=rating|newest|oldest|plays&page=1&limit=20
require_once __DIR__ . '/db.php';

$sort  = $_GET['sort']  ?? 'rating';
$page  = max(1, intval($_GET['page']  ?? 1));
$limit = min(50, max(1, intval($_GET['limit'] ?? 20)));
$offset = ($page - 1) * $limit;

$orderBy = match($sort) {
    'newest' => 'published_at DESC',
    'oldest' => 'published_at ASC',
    'plays'  => 'play_count DESC',
    default  => 'average_rating DESC, rating_count DESC',
};

try {
    $db = getDb();

    $total = (int)$db->query(
        "SELECT COUNT(*) FROM community_levels WHERE is_published = 1"
    )->fetchColumn();

    $stmt = $db->prepare("
        SELECT id, steam_id, steam_name, title, description,
               brick_count, play_count, average_rating, rating_count,
               published_at
        FROM community_levels
        WHERE is_published = 1
        ORDER BY {$orderBy}
        LIMIT :limit OFFSET :offset
    ");
    $stmt->bindValue(':limit',  $limit,  PDO::PARAM_INT);
    $stmt->bindValue(':offset', $offset, PDO::PARAM_INT);
    $stmt->execute();
    $rows = $stmt->fetchAll();

    foreach ($rows as &$row) {
        $row['averageRating'] = (float)$row['average_rating'];
        $row['ratingCount']   = (int)$row['rating_count'];
        $row['playCount']     = (int)$row['play_count'];
        $row['brickCount']    = (int)$row['brick_count'];
        $row['steamId']       = $row['steam_id'];
        $row['steamName']     = $row['steam_name'];
        $row['publishedAt']   = $row['published_at'];
        unset($row['average_rating'], $row['rating_count'], $row['play_count'],
              $row['brick_count'], $row['steam_id'], $row['steam_name'], $row['published_at']);
    }
    unset($row);

    jsonResponse(['levels' => $rows, 'total' => $total, 'page' => $page, 'perPage' => $limit]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
