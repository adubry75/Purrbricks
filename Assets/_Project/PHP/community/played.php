<?php
// POST /community/played.php
// Body: {levelId}  — fire-and-forget play count increment
require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') errorResponse('POST required', 405);

$body    = json_decode(file_get_contents('php://input'), true);
$levelId = intval($body['levelId'] ?? 0);

if ($levelId <= 0) errorResponse('levelId required');

try {
    $db   = getDb();
    $stmt = $db->prepare("
        UPDATE community_levels
        SET play_count = play_count + 1
        WHERE id = :id AND is_published = 1
    ");
    $stmt->execute([':id' => $levelId]);
    jsonResponse(['ok' => true]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
