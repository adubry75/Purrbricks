<?php
// POST /community/publish.php
// Body: {steamId, steamName, title, description, jsonData, brickCount}
require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') errorResponse('POST required', 405);

$body = json_decode(file_get_contents('php://input'), true);
if (!$body) errorResponse('Invalid JSON body');

$steamId    = trim($body['steamId']    ?? '');
$steamName  = trim($body['steamName']  ?? '');
$title      = trim($body['title']      ?? '');
$desc       = trim($body['description'] ?? '');
$jsonData   = $body['jsonData']        ?? '';
$brickCount = intval($body['brickCount'] ?? 0);

if (!$steamId)    errorResponse('steamId required');
if (!$title)      errorResponse('title required');
if (!$jsonData)   errorResponse('jsonData required');
if (strlen($title) > 64)  errorResponse('title too long (max 64)');
if (strlen($desc)  > 256) errorResponse('description too long (max 256)');

// Basic JSON validation
if (json_decode($jsonData) === null) errorResponse('jsonData is not valid JSON');

try {
    $db   = getDb();
    $stmt = $db->prepare("
        INSERT INTO community_levels
            (steam_id, steam_name, title, description, json_data, brick_count)
        VALUES
            (:sid, :sname, :title, :desc, :json, :bricks)
    ");
    $stmt->execute([
        ':sid'    => substr($steamId,   0, 32),
        ':sname'  => substr($steamName, 0, 128),
        ':title'  => substr($title,     0, 64),
        ':desc'   => substr($desc,      0, 256),
        ':json'   => $jsonData,
        ':bricks' => $brickCount,
    ]);
    jsonResponse(['id' => (int)$db->lastInsertId()]);
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
