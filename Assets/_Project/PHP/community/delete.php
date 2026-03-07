<?php
// POST /community/delete.php
// Body: {levelId, steamId}  — unpublishes; only the original publisher can delete
require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') errorResponse('POST required', 405);

$body    = json_decode(file_get_contents('php://input'), true);
$levelId = intval($body['levelId'] ?? 0);
$steamId = trim($body['steamId']   ?? '');

if ($levelId <= 0) errorResponse('levelId required');
if (!$steamId)     errorResponse('steamId required');

try {
    $db   = getDb();
    $stmt = $db->prepare("
        UPDATE community_levels
        SET is_published = 0
        WHERE id = :id AND steam_id = :sid
    ");
    $stmt->execute([':id' => $levelId, ':sid' => $steamId]);
    if ($stmt->rowCount() === 0)
        errorResponse('Level not found or you are not the author', 403);
    jsonResponse(['ok' => true]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
