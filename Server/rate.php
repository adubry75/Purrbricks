<?php
// ============================================================
// Purrbricks — Level Rating API Endpoint
// POST /rate.php  (Content-Type: application/json)
//
// Expected JSON body:
//   { "levelId": "level_01", "levelIndex": 1,
//     "steamId": "76561198...", "steamName": "PlayerName",
//     "rating": 4,
//     "createdAt": "2025-01-01T00:00:00Z",
//     "updatedAt": "2025-01-02T00:00:00Z" }
// ============================================================

require __DIR__ . '/config.php';   // DB_HOST, DB_NAME, DB_USER, DB_PASS

header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');

// Handle CORS preflight
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['error' => 'Method not allowed']);
    exit;
}

// ── Parse body ───────────────────────────────────────────────────────────────
$raw  = file_get_contents('php://input');
$data = json_decode($raw, true);

if (!is_array($data)) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid JSON body']);
    exit;
}

$levelId    = trim($data['levelId']    ?? '');
$levelIndex = isset($data['levelIndex']) ? (int)$data['levelIndex'] : -1;
$steamId    = trim($data['steamId']    ?? '');
$steamName  = mb_substr(trim($data['steamName'] ?? ''), 0, 128);
$rating     = isset($data['rating'])    ? (int)$data['rating']     : -1;

// ── Validate ─────────────────────────────────────────────────────────────────
if ($levelId === '' || $steamId === '' || $levelIndex < 0
    || $rating < 0 || $rating > 5) {
    http_response_code(400);
    echo json_encode(['error' => 'Missing or invalid fields',
                      'received' => ['levelId' => $levelId,
                                     'steamId' => $steamId,
                                     'levelIndex' => $levelIndex,
                                     'rating' => $rating]]);
    exit;
}

// ── Write to DB ───────────────────────────────────────────────────────────────
try {
    $pdo = new PDO(
        'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
        DB_USER,
        DB_PASS,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
         PDO::ATTR_EMULATE_PREPARES => false]
    );

    // INSERT ... ON DUPLICATE KEY UPDATE so re-rating just updates the row
    $sql = "
        INSERT INTO ratings
            (level_id, level_index, steam_id, steam_name, rating)
        VALUES
            (:level_id, :level_index, :steam_id, :steam_name, :rating)
        ON DUPLICATE KEY UPDATE
            rating      = VALUES(rating),
            steam_name  = VALUES(steam_name),
            updated_at  = NOW()
    ";

    $stmt = $pdo->prepare($sql);
    $stmt->execute([
        ':level_id'    => $levelId,
        ':level_index' => $levelIndex,
        ':steam_id'    => $steamId,
        ':steam_name'  => $steamName,
        ':rating'      => $rating,
    ]);

    echo json_encode(['success' => true,
                      'levelId' => $levelId,
                      'rating'  => $rating]);

} catch (PDOException $e) {
    // Log full error server-side, return generic message to client
    error_log('Purrbricks rate.php PDOException: ' . $e->getMessage());
    http_response_code(500);
    echo json_encode(['error' => 'Database error']);
}
