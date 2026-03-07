<?php
// GET /community/get.php?id=42
require_once __DIR__ . '/db.php';

$id = intval($_GET['id'] ?? 0);
if ($id <= 0) errorResponse('Missing or invalid id');

try {
    $db   = getDb();
    $stmt = $db->prepare("
        SELECT id, steam_id, steam_name, title, description,
               brick_count, play_count, average_rating, rating_count,
               published_at, json_data
        FROM community_levels
        WHERE id = :id AND is_published = 1
    ");
    $stmt->execute([':id' => $id]);
    $row = $stmt->fetch();
    if (!$row) errorResponse('Level not found', 404);

    jsonResponse([
        'id'            => (int)$row['id'],
        'steamId'       => $row['steam_id'],
        'steamName'     => $row['steam_name'],
        'title'         => $row['title'],
        'description'   => $row['description'],
        'brickCount'    => (int)$row['brick_count'],
        'playCount'     => (int)$row['play_count'],
        'averageRating' => (float)$row['average_rating'],
        'ratingCount'   => (int)$row['rating_count'],
        'publishedAt'   => $row['published_at'],
        'jsonData'      => $row['json_data'],
    ]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
