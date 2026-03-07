<?php
// POST /community/publish.php
// Body: {steamId, steamName, levelGuid, title, description, jsonData, brickCount}
// Behaviour:
//   - If no row exists for levelGuid → INSERT (create)
//   - If a row exists and steam_id matches → UPDATE
//   - If a row exists but steam_id does NOT match → 403
// Response: {id, levelGuid, action: "created"|"updated"}

ini_set('display_errors', '0');
ini_set('log_errors', '1');

require_once __DIR__ . '/db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') errorResponse('POST required', 405);

$body = json_decode(file_get_contents('php://input'), true);
if (!$body) errorResponse('Invalid JSON body');

$steamId    = trim($body['steamId']     ?? '');
$steamName  = trim($body['steamName']   ?? '');
$levelGuid  = trim($body['levelGuid']   ?? '');
$title      = trim($body['title']       ?? '');
$desc       = trim($body['description'] ?? '');
$jsonData   = $body['jsonData']         ?? '';
$brickCount = intval($body['brickCount'] ?? 0);

if (!$steamId)   errorResponse('steamId required');
if (!$levelGuid) errorResponse('levelGuid required');
if (!$title)     errorResponse('title required');
if (!$jsonData)  errorResponse('jsonData required');

if (strlen($title)     > 64)  errorResponse('title too long (max 64)');
if (strlen($desc)      > 256) errorResponse('description too long (max 256)');
if (strlen($levelGuid) > 64)  errorResponse('levelGuid too long (max 64)');

// jsonData may arrive as a nested JSON object (array) or a pre-encoded string
if (is_array($jsonData) || is_object($jsonData)) {
    $jsonData = json_encode($jsonData);
} elseif (is_string($jsonData)) {
    if (json_decode($jsonData) === null) errorResponse('jsonData is not valid JSON');
}

try {
    $db = getDb();

    // Look up existing row by level_guid
    $stmt = $db->prepare("SELECT id, steam_id FROM community_levels WHERE level_guid = ? LIMIT 1");
    $stmt->execute([$levelGuid]);
    $existing = $stmt->fetch();

    if ($existing) {
        // Ownership check — only the original author may update
        if ($existing['steam_id'] !== substr($steamId, 0, 32)) {
            errorResponse('Not the owner of this level', 403);
        }

        $stmt = $db->prepare("
            UPDATE community_levels SET
                steam_name  = :sname,
                title       = :title,
                description = :desc,
                json_data   = :json,
                brick_count = :bricks
            WHERE id = :id
        ");
        $stmt->execute([
            ':sname'  => substr($steamName, 0, 128),
            ':title'  => substr($title,     0, 64),
            ':desc'   => substr($desc,      0, 256),
            ':json'   => $jsonData,
            ':bricks' => $brickCount,
            ':id'     => $existing['id'],
        ]);

        jsonResponse(['id' => (int)$existing['id'], 'levelGuid' => $levelGuid, 'action' => 'updated']);
    } else {
        $stmt = $db->prepare("
            INSERT INTO community_levels
                (steam_id, steam_name, level_guid, title, description, json_data, brick_count)
            VALUES
                (:sid, :sname, :guid, :title, :desc, :json, :bricks)
        ");
        $stmt->execute([
            ':sid'    => substr($steamId,   0, 32),
            ':sname'  => substr($steamName, 0, 128),
            ':guid'   => $levelGuid,
            ':title'  => substr($title,     0, 64),
            ':desc'   => substr($desc,      0, 256),
            ':json'   => $jsonData,
            ':bricks' => $brickCount,
        ]);

        $newId = (int)$db->lastInsertId();
        jsonResponse(['id' => $newId, 'levelGuid' => $levelGuid, 'action' => 'created']);
    }
} catch (Exception $e) {
    errorResponse('DB error: ' . $e->getMessage(), 500);
}
