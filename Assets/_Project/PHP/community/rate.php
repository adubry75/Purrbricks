<?php
// POST /community/rate.php
// Body: {levelId, steamId, rating}  rating=0 to unrate
require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') errorResponse('POST required', 405);

$body = json_decode(file_get_contents('php://input'), true);
if (!$body) errorResponse('Invalid JSON body');

$levelId = intval($body['levelId'] ?? 0);
$steamId = trim($body['steamId']   ?? '');
$rating  = intval($body['rating']  ?? 0);

if ($levelId <= 0) errorResponse('levelId required');
if (!$steamId)     errorResponse('steamId required');
if ($rating < 0 || $rating > 5) errorResponse('rating must be 0-5');

try {
    $db = getDb();

    // Verify level exists
    $exists = $db->prepare("SELECT 1 FROM community_levels WHERE id = :id AND is_published = 1");
    $exists->execute([':id' => $levelId]);
    if (!$exists->fetch()) errorResponse('Level not found', 404);

    if ($rating === 0) {
        // Delete rating
        $del = $db->prepare("DELETE FROM community_ratings WHERE level_id = :lid AND steam_id = :sid");
        $del->execute([':lid' => $levelId, ':sid' => $steamId]);
    } else {
        // Upsert rating
        $upsert = $db->prepare("
            INSERT INTO community_ratings (level_id, steam_id, rating)
            VALUES (:lid, :sid, :r)
            ON DUPLICATE KEY UPDATE rating = :r2, rated_at = NOW()
        ");
        $upsert->execute([':lid' => $levelId, ':sid' => $steamId, ':r' => $rating, ':r2' => $rating]);
    }

    // Recompute average_rating + rating_count on community_levels
    $recompute = $db->prepare("
        UPDATE community_levels cl
        SET
            rating_count  = (SELECT COUNT(*) FROM community_ratings WHERE level_id = :lid1),
            average_rating = (SELECT COALESCE(AVG(rating), 0) FROM community_ratings WHERE level_id = :lid2)
        WHERE id = :lid3
    ");
    $recompute->execute([':lid1' => $levelId, ':lid2' => $levelId, ':lid3' => $levelId]);

    jsonResponse(['ok' => true]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
