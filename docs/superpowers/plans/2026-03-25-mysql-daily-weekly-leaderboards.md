# MySQL Daily/Weekly Leaderboards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Steam daily/weekly per-level leaderboards with a MySQL-backed system on BlueHost, keeping all-time leaderboards on Steam unchanged.

**Architecture:** Two PHP endpoints (`submit.php`, `list.php`) in a new `scores/` folder on the server handle score storage and rank queries. A new `LevelScoreService.cs` singleton in Unity communicates with these endpoints. `VictoryUI` submits via `LevelScoreService` and passes the returned daily/weekly ranks to `PurrBucksManager`. `HighScoresUI` routes per-level Daily/Weekly tab fetches to `LevelScoreService` instead of Steam.

**Tech Stack:** PHP 7+, MySQL 5.7, PDO, Unity C#, UnityWebRequest, Steamworks.NET (Steam all-time boards unchanged)

**Spec:** `docs/superpowers/specs/2026-03-25-mysql-daily-weekly-leaderboards-design.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `Assets/_Project/PHP/scores/db.php` | PDO connection + JSON response helpers |
| Create | `Assets/_Project/PHP/scores/schema.sql` | `level_scores` table DDL |
| Create | `Assets/_Project/PHP/scores/submit.php` | Upsert score, return daily+weekly rank |
| Create | `Assets/_Project/PHP/scores/list.php` | Fetch leaderboard entries for display |
| Create | `Assets/_Project/Scripts/Services/LevelScoreService.cs` | Unity HTTP client for scores API |
| Modify | `Assets/_Project/Scripts/Services/PurrBucksManager.cs` | Accept daily/weekly ranks as params |
| Modify | `Assets/_Project/Scripts/UI/VictoryUI.cs` | Submit via LevelScoreService, remove Steam daily/weekly |
| Modify | `Assets/_Project/Scripts/UI/HighScoresUI.cs` | Route Daily/Weekly tabs to MySQL |
| Modify | `Assets/_Project/Scripts/Services/PurrbricksLeaderboards.cs` | Add usage comment |
| Modify | `Assets/_Project/Scripts/Editor/PurrbricksSetup.cs` | Auto-create LevelScoreService GO |

---

## Task 1: Database Schema + PHP Infrastructure

**Files:**
- Create: `Assets/_Project/PHP/scores/schema.sql`
- Create: `Assets/_Project/PHP/scores/db.php`

- [ ] **Step 1: Create `schema.sql`**

```sql
-- Run this once on your BlueHost MySQL database.
CREATE TABLE IF NOT EXISTS level_scores (
  id           INT AUTO_INCREMENT PRIMARY KEY,
  level_id     VARCHAR(64)     NOT NULL,
  steam_id     BIGINT UNSIGNED NOT NULL,
  steam_name   VARCHAR(128)    NOT NULL DEFAULT '',
  score        INT UNSIGNED    NOT NULL,
  score_date   DATE            NOT NULL,
  submitted_at DATETIME        NOT NULL DEFAULT UTC_TIMESTAMP(),
  UNIQUE KEY uq_player_level_date (steam_id, level_id, score_date),
  INDEX idx_level_date_score (level_id, score_date, score)
);
```

- [ ] **Step 2: Run `schema.sql` on BlueHost**

In BlueHost cPanel → phpMyAdmin, select your database, click the SQL tab, paste the contents of `schema.sql` and execute. Confirm the `level_scores` table appears with the correct columns and indexes.

- [ ] **Step 3: Create `db.php`**

```php
<?php
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type');
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') { http_response_code(204); exit; }

// ── Edit these to match your community/db.php credentials ─────────────────────
define('DB_HOST', 'localhost');
define('DB_NAME', 'your_db_name');
define('DB_USER', 'your_db_user');
define('DB_PASS', 'your_db_pass');

function getDb(): PDO {
    static $pdo = null;
    if ($pdo === null) {
        $pdo = new PDO(
            'mysql:host=' . DB_HOST . ';dbname=' . DB_NAME . ';charset=utf8mb4',
            DB_USER, DB_PASS,
            [PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
             PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC]
        );
    }
    return $pdo;
}

function jsonResponse(array $data, int $status = 200): void {
    http_response_code($status);
    header('Content-Type: application/json');
    echo json_encode($data);
    exit;
}

function errorResponse(string $message, int $status = 400): void {
    jsonResponse(['error' => $message], $status);
}
```

- [ ] **Step 4: Upload `db.php` to `/home3/dubrycom/purrbricks-api/scores/`**

Copy the DB credentials from your existing `community/db.php` into this file before uploading.

- [ ] **Step 5: Commit**

```bash
git add "Assets/_Project/PHP/scores/schema.sql" "Assets/_Project/PHP/scores/db.php"
git commit -m "feat: add scores PHP infrastructure (schema + db helper)"
```

---

## Task 2: `submit.php` — Score Upsert + Rank Response

**Files:**
- Create: `Assets/_Project/PHP/scores/submit.php`

- [ ] **Step 1: Create `submit.php`**

```php
<?php
require_once 'db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') { errorResponse('POST required', 405); }

$body     = json_decode(file_get_contents('php://input'), true) ?? [];
$steamId  = isset($body['steamId'])   ? (string)$body['steamId']                   : '';
$steamName = isset($body['steamName']) ? substr((string)$body['steamName'], 0, 128) : '';
$levelId  = isset($body['levelId'])   ? substr((string)$body['levelId'], 0, 64)    : '';
$score    = isset($body['score'])     ? (int)$body['score']                        : 0;

if (!$steamId || !$levelId || $score <= 0) {
    errorResponse('steamId, levelId, and score > 0 required');
}

$db = getDb();

// Upsert: one row per (player, level, UTC day) — keep best score for the day.
$db->prepare("
    INSERT INTO level_scores (level_id, steam_id, steam_name, score, score_date)
    VALUES (:levelId, :steamId, :steamName, :score, UTC_DATE())
    ON DUPLICATE KEY UPDATE
        score        = GREATEST(score, VALUES(score)),
        steam_name   = VALUES(steam_name),
        submitted_at = UTC_TIMESTAMP()
")->execute([
    ':levelId'   => $levelId,
    ':steamId'   => $steamId,
    ':steamName' => $steamName,
    ':score'     => $score,
]);

// Daily rank: count distinct players with a higher score today for this level + 1.
$stmt = $db->prepare("
    SELECT COUNT(DISTINCT steam_id) + 1 AS rnk
    FROM level_scores
    WHERE level_id = :levelId
      AND score_date = UTC_DATE()
      AND score > :score
");
$stmt->execute([':levelId' => $levelId, ':score' => $score]);
$dailyRank = (int)$stmt->fetchColumn();

// Weekly rank: count distinct players whose MAX(score) this week exceeds the submitted score + 1.
$stmt = $db->prepare("
    SELECT COUNT(*) + 1 AS rnk
    FROM (
        SELECT steam_id, MAX(score) AS best
        FROM level_scores
        WHERE level_id  = :levelId
          AND YEARWEEK(score_date, 0) = YEARWEEK(UTC_DATE(), 0)
        GROUP BY steam_id
    ) w
    WHERE w.best > :score
");
$stmt->execute([':levelId' => $levelId, ':score' => $score]);
$weeklyRank = (int)$stmt->fetchColumn();

jsonResponse([
    'success'    => true,
    'dailyRank'  => $dailyRank  <= 3 ? $dailyRank  : 0,
    'weeklyRank' => $weeklyRank <= 3 ? $weeklyRank : 0,
]);
```

Note: ranks > 3 are returned as `0` (not in top 3). `0` is also the value on error — Unity treats 0 as "no bonus."

- [ ] **Step 2: Upload `submit.php` to `/home3/dubrycom/purrbricks-api/scores/`**

- [ ] **Step 3: Smoke-test with curl**

```bash
curl -s -X POST https://dubry.com/purrbricks-api/scores/submit.php \
  -H "Content-Type: application/json" \
  -d '{"steamId":"76561198000000001","steamName":"TestCat","levelId":"alien_invasion","score":95000}'
```

Expected response: `{"success":true,"dailyRank":1,"weeklyRank":1}` (first submission today = rank 1).

Submit a second, lower score from the same player:
```bash
curl -s -X POST https://dubry.com/purrbricks-api/scores/submit.php \
  -H "Content-Type: application/json" \
  -d '{"steamId":"76561198000000001","steamName":"TestCat","levelId":"alien_invasion","score":50000}'
```

Expected: still rank 1 (lower score doesn't overwrite). Verify in phpMyAdmin that the row still shows `score=95000`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Project/PHP/scores/submit.php"
git commit -m "feat: add scores/submit.php endpoint"
```

---

## Task 3: `list.php` — Leaderboard Display

**Files:**
- Create: `Assets/_Project/PHP/scores/list.php`

- [ ] **Step 1: Create `list.php`**

```php
<?php
require_once 'db.php';

$levelId = isset($_GET['levelId']) ? substr((string)$_GET['levelId'], 0, 64) : '';
$scope   = isset($_GET['scope'])   ? (string)$_GET['scope']                  : '';
$limit   = min(max((int)($_GET['limit'] ?? 10), 1), 50);
$steamId = isset($_GET['steamId']) ? (string)$_GET['steamId']                : '';

if (!$levelId || !in_array($scope, ['daily', 'weekly'], true)) {
    errorResponse('levelId and scope (daily|weekly) required');
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
} else {
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
        } else {
            $s = $db->prepare("SELECT MAX(score) FROM level_scores WHERE level_id=:l AND steam_id=:s AND YEARWEEK(score_date,0)=YEARWEEK(UTC_DATE(),0)");
            $s->execute([':l' => $levelId, ':s' => $steamId]);
            $myBest = $s->fetchColumn();
            if ($myBest !== false && $myBest !== null) {
                $s2 = $db->prepare("SELECT COUNT(*)+1 FROM (SELECT steam_id,MAX(score) AS best FROM level_scores WHERE level_id=:l AND YEARWEEK(score_date,0)=YEARWEEK(UTC_DATE(),0) GROUP BY steam_id) w WHERE w.best>:sc");
                $s2->execute([':l' => $levelId, ':sc' => $myBest]);
                $playerRank = (int)$s2->fetchColumn();
            }
        }
    }
}

jsonResponse(['scores' => $scores, 'playerRank' => $playerRank]);
```

- [ ] **Step 2: Upload `list.php` to `/home3/dubrycom/purrbricks-api/scores/`**

- [ ] **Step 3: Smoke-test with curl**

```bash
# Fetch daily leaderboard (after running Task 2 tests which inserted a row)
curl -s "https://dubry.com/purrbricks-api/scores/list.php?levelId=alien_invasion&scope=daily&limit=10&steamId=76561198000000001"
```

Expected: `{"scores":[{"rank":1,"steamId":"76561198000000001","steamName":"TestCat","score":95000}],"playerRank":1}`

```bash
# Fetch weekly
curl -s "https://dubry.com/purrbricks-api/scores/list.php?levelId=alien_invasion&scope=weekly&limit=10"
```

Expected: same single entry with `playerRank:0` (no steamId param provided).

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Project/PHP/scores/list.php"
git commit -m "feat: add scores/list.php endpoint"
```

---

## Task 4: `LevelScoreService.cs` — Unity HTTP Client

**Files:**
- Create: `Assets/_Project/Scripts/Services/LevelScoreService.cs`

- [ ] **Step 1: Create `LevelScoreService.cs`**

```csharp
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles MySQL-backed daily and weekly per-level leaderboards.
/// All-time leaderboards remain on Steam — this service is for Daily/Weekly only.
/// </summary>
public class LevelScoreService : MonoBehaviour
{
    public static LevelScoreService Instance { get; private set; }

    [SerializeField] private string _apiBaseUrl = "https://dubry.com/purrbricks-api/scores";

    private const int TIMEOUT_SECONDS = 8;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public types ───────────────────────────────────────────────────────────

    public struct SubmitResult
    {
        /// <summary>1, 2, or 3 if player is top-3 today; 0 otherwise.</summary>
        public int DailyRank;
        /// <summary>1, 2, or 3 if player is top-3 this week; 0 otherwise.</summary>
        public int WeeklyRank;
    }

    public struct ScoreEntry
    {
        public int    Rank;
        public ulong  SteamId;
        public string SteamName;
        public int    Score;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Submit a score. Callback receives daily and weekly rank (0 = not top-3 or error).
    /// Times out after 8 seconds and returns rank 0 on failure — game continues normally.
    /// </summary>
    public void SubmitScore(string levelId, ulong steamId, string steamName, int score,
                            Action<SubmitResult> callback)
    {
        StartCoroutine(SubmitScoreRoutine(levelId, steamId, steamName, score, callback));
    }

    /// <summary>
    /// Fetch leaderboard entries for HighScoresUI. Only Daily or Weekly scope is valid —
    /// AllTime must use Steam. playerRank in callback is 0 if the player has no score this period.
    /// </summary>
    public void FetchScores(string levelId, LeaderboardTimeScope scope, int limit,
                            ulong steamId, Action<ScoreEntry[], int> callback)
    {
        if (scope == LeaderboardTimeScope.AllTime)
        {
            Debug.LogError("[LevelScoreService] FetchScores called with AllTime scope — use Steam.");
            callback?.Invoke(Array.Empty<ScoreEntry>(), 0);
            return;
        }
        StartCoroutine(FetchScoresRoutine(levelId, scope, limit, steamId, callback));
    }

    // ── Coroutines ─────────────────────────────────────────────────────────────

    private IEnumerator SubmitScoreRoutine(string levelId, ulong steamId, string steamName,
                                           int score, Action<SubmitResult> callback)
    {
        var bodyObj = new SubmitRequestBody
        {
            steamId   = steamId.ToString(),
            steamName = steamName,
            levelId   = levelId,
            score     = score
        };
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(bodyObj));

        using (var req = new UnityWebRequest(_apiBaseUrl + "/submit.php", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TIMEOUT_SECONDS;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LevelScoreService] SubmitScore failed: {req.error}");
                callback?.Invoke(new SubmitResult());
                yield break;
            }

            var response = JsonUtility.FromJson<SubmitResponse>(req.downloadHandler.text);
            callback?.Invoke(new SubmitResult
            {
                DailyRank  = response?.dailyRank  ?? 0,
                WeeklyRank = response?.weeklyRank ?? 0
            });
        }
    }

    private IEnumerator FetchScoresRoutine(string levelId, LeaderboardTimeScope scope,
                                           int limit, ulong steamId,
                                           Action<ScoreEntry[], int> callback)
    {
        string scopeStr = scope == LeaderboardTimeScope.Daily ? "daily" : "weekly";
        string url = $"{_apiBaseUrl}/list.php" +
                     $"?levelId={UnityWebRequest.EscapeURL(levelId)}" +
                     $"&scope={scopeStr}&limit={limit}&steamId={steamId}";

        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = TIMEOUT_SECONDS;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LevelScoreService] FetchScores failed: {req.error}");
                callback?.Invoke(Array.Empty<ScoreEntry>(), 0);
                yield break;
            }

            var response = JsonUtility.FromJson<ListResponse>(req.downloadHandler.text);
            if (response?.scores == null)
            {
                callback?.Invoke(Array.Empty<ScoreEntry>(), 0);
                yield break;
            }

            var entries = new ScoreEntry[response.scores.Length];
            for (int i = 0; i < response.scores.Length; i++)
            {
                var s = response.scores[i];
                entries[i] = new ScoreEntry
                {
                    Rank      = s.rank,
                    SteamId   = ulong.TryParse(s.steamId, out ulong sid) ? sid : 0UL,
                    SteamName = s.steamName ?? "",
                    Score     = s.score
                };
            }

            callback?.Invoke(entries, response.playerRank);
        }
    }

    // ── JSON DTOs ──────────────────────────────────────────────────────────────

    [Serializable] private class SubmitRequestBody
    {
        public string steamId;
        public string steamName;
        public string levelId;
        public int    score;
    }

    [Serializable] private class SubmitResponse
    {
        public bool success;
        public int  dailyRank;
        public int  weeklyRank;
    }

    [Serializable] private class ScoreEntryDto
    {
        public int    rank;
        public string steamId;
        public string steamName;
        public int    score;
    }

    [Serializable] private class ListResponse
    {
        public ScoreEntryDto[] scores;
        public int             playerRank; // 0 = no score this period
    }
}
```

- [ ] **Step 2: Verify it compiles in Unity**

Open Unity, wait for recompile. Confirm no errors in the Console. The new `LevelScoreService` class should appear in the Add Component menu.

- [ ] **Step 3: Commit**

```bash
git add "Assets/_Project/Scripts/Services/LevelScoreService.cs"
git commit -m "feat: add LevelScoreService for MySQL daily/weekly leaderboards"
```

---

## Task 5: Update `PurrBucksManager.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/Services/PurrBucksManager.cs:169-274`

Changes: add `dailyRank`/`weeklyRank` params to `AwardLevelComplete`; apply daily/weekly bonuses from those params directly; simplify `FetchRankAndAward` to only fetch AllTime from Steam.

- [ ] **Step 1: Replace `AwardLevelComplete` (lines 169–216)**

Replace the entire method from line 169 to the closing `}` of the method (line 216) with:

```csharp
public void AwardLevelComplete(string levelId, int levelIndex, bool perfectClear, int livesLost,
                               int dailyRank = 0, int weeklyRank = 0)
{
    _pendingAward = 0;

    bool isFirstTime = PlayerPrefs.GetInt(KEY_CLEARED_PREFIX + levelId, 0) == 0;
    if (isFirstTime) PlayerPrefs.SetInt(KEY_CLEARED_PREFIX + levelId, 1);

    // ── Immediate awards ──────────────────────────────────────────────────
    int immediate = PurrBucksConfig.REWARD_PARTICIPATION;
    if (perfectClear) immediate += PurrBucksConfig.REWARD_PERFECT_CLEAR;
    if (isFirstTime)  immediate += PurrBucksConfig.REWARD_FIRST_TIME;

    _pendingAward = immediate;
    AddCurrency(immediate);
    OnRankAwardResolved?.Invoke(_pendingAward);

    // Daily/Weekly rank bonuses come from LevelScoreService (already resolved).
    int weeklyBonus = Mathf.RoundToInt(GetRankBonus(weeklyRank) * 0.60f);
    int dailyBonus  = Mathf.RoundToInt(GetRankBonus(dailyRank)  * 0.40f);

    // ── AllTime rank bonus (from Steam, async) ────────────────────────────
    if (LeaderboardTestData.Enabled)
    {
        string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(levelIndex);
        int    allTimeRank  = LeaderboardTestData.GetOrRollDesiredRank(allTimeBoard);
        int    allTimeBonus = GetRankBonus(allTimeRank);

        int rankBonus = allTimeBonus + weeklyBonus + dailyBonus;
        if (rankBonus > 0) { _pendingAward += rankBonus; AddCurrency(rankBonus); }
        OnRankAwardResolved?.Invoke(_pendingAward);
        return;
    }

    StartCoroutine(FetchRankAndAward(levelIndex, weeklyBonus, dailyBonus));
}
```

- [ ] **Step 2: Replace `FetchRankAndAward` (lines 218–274)**

Replace the entire `FetchRankAndAward` coroutine with:

```csharp
private IEnumerator FetchRankAndAward(int levelIndex, int weeklyBonus, int dailyBonus)
{
    // Give Steam time to finish uploading before querying rank.
    yield return new WaitForSecondsRealtime(1.5f);

    string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(levelIndex);
    int    allTimeRank  = 0;

    if (SteamLeaderboardManager.Instance != null)
    {
        bool done = false;
        SteamLeaderboardManager.Instance.FetchAroundMe(allTimeBoard, 0, entries =>
        {
            if (entries != null && entries.Count > 0) allTimeRank = entries[0].Rank;
            done = true;
        });

        float elapsed = 0f;
        while (!done && elapsed < 8f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    int allTimeBonus = GetRankBonus(allTimeRank);
    int rankBonus    = allTimeBonus + weeklyBonus + dailyBonus;
    if (rankBonus > 0) { _pendingAward += rankBonus; AddCurrency(rankBonus); }

    OnRankAwardResolved?.Invoke(_pendingAward);
}
```

- [ ] **Step 3: Verify it compiles in Unity**

No errors in Console.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Project/Scripts/Services/PurrBucksManager.cs"
git commit -m "refactor: AwardLevelComplete accepts daily/weekly rank params from LevelScoreService"
```

---

## Task 6: Update `VictoryUI.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/VictoryUI.cs:468-491`

Remove Steam Daily/Weekly submits and `RerollForBoard` calls for those boards. Add `LevelScoreService.SubmitScore` call; pass rank result to `AwardLevelComplete`.

- [ ] **Step 1: Add `using Steamworks;` to `VictoryUI.cs`**

`VictoryUI.cs` currently has no `using Steamworks;` directive. The new code calls `SteamUser.GetSteamID()` and `SteamFriends.GetPersonaName()` which require it. Add it after the existing using block at line 4:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Steamworks;
```

- [ ] **Step 2: Replace the score-submit block in `ShowVictory` (lines 468–491)**

Find and replace this exact block:

```csharp
        // Submit to Steam per-level leaderboards immediately (KeepBest)
        if (levelScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(levelIndex);
            string weeklyBoard = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
            string dailyBoard = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);

            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, levelScore);
            SteamLeaderboardManager.Instance?.SubmitScore(weeklyBoard, levelScore);
            SteamLeaderboardManager.Instance?.SubmitScore(dailyBoard, levelScore);

            LeaderboardTestData.RerollForBoard(allTimeBoard);
            LeaderboardTestData.RerollForBoard(weeklyBoard);
            LeaderboardTestData.RerollForBoard(dailyBoard);
        }

        // Award Purr Bucks (OnEnable already subscribed above)
        if (PurrBucksManager.Instance != null && !string.IsNullOrEmpty(levelId))
        {
            int livesLost = (GameManager.Instance?.LivesAtLevelStart ?? 0) - (GameManager.Instance?.GetLives() ?? 0);
            bool perfectClear = livesLost <= 0;
            _purrBucksText?.gameObject.SetActive(false);
            PurrBucksManager.Instance.AwardLevelComplete(levelId, levelIndex, perfectClear, livesLost);
        }
```

With:

```csharp
        // Submit to Steam per-level all-time leaderboard (KeepBest) — daily/weekly now use MySQL.
        if (levelScore > 0)
        {
            string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(levelIndex);
            SteamLeaderboardManager.Instance?.SubmitScore(allTimeBoard, levelScore);
            LeaderboardTestData.RerollForBoard(allTimeBoard);
        }

        // Submit to MySQL daily leaderboard; use the returned ranks to award Purr Bucks.
        int livesLost     = (GameManager.Instance?.LivesAtLevelStart ?? 0) - (GameManager.Instance?.GetLives() ?? 0);
        bool perfectClear = livesLost <= 0;
        _purrBucksText?.gameObject.SetActive(false);

        if (PurrBucksManager.Instance != null && !string.IsNullOrEmpty(levelId))
        {
            if (levelScore > 0 && LevelScoreService.Instance != null)
            {
                ulong  steamId   = SteamworksBootstrap.Instance?.IsSteamAvailable == true
                                   ? SteamUser.GetSteamID().m_SteamID : 0UL;  // Steamworks.SteamUser
                string steamName = SteamworksBootstrap.Instance?.IsSteamAvailable == true
                                   ? SteamFriends.GetPersonaName() : "Player"; // Steamworks.SteamFriends

                LevelScoreService.Instance.SubmitScore(levelId, steamId, steamName, levelScore, result =>
                {
                    PurrBucksManager.Instance?.AwardLevelComplete(
                        levelId, levelIndex, perfectClear, livesLost,
                        result.DailyRank, result.WeeklyRank);
                });
            }
            else
            {
                // Fallback: no score or service unavailable — award without rank bonus.
                PurrBucksManager.Instance.AwardLevelComplete(levelId, levelIndex, perfectClear, livesLost);
            }
        }
```

**Timing note:** In the old code, the base Purr Bucks amount (`+50 Purr Bucks`) appeared on screen immediately when the Victory screen opened. With this change, `AwardLevelComplete` is called from inside the `LevelScoreService` callback, so the display is delayed by the MySQL round-trip (typically 1–3 seconds, up to 8s timeout). This is an accepted trade-off per the spec — the star animation plays during this window and the delay is not jarring in practice.

- [ ] **Step 3: Verify it compiles in Unity**

No errors.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Project/Scripts/UI/VictoryUI.cs"
git commit -m "feat: VictoryUI submits daily/weekly scores via LevelScoreService"
```

---

## Task 7: Update `HighScoresUI.cs`

**Files:**
- Modify: `Assets/_Project/Scripts/UI/HighScoresUI.cs:96-108` (PrewarmCurrentBoardScopes)
- Modify: `Assets/_Project/Scripts/UI/HighScoresUI.cs:309-346` (Fetch — add MySQL branch)

- [ ] **Step 1: Replace `PrewarmCurrentBoardScopes` (lines 96–108)**

```csharp
    private void PrewarmCurrentBoardScopes()
    {
        // Per-level Daily/Weekly boards now use MySQL — only prewarm Steam for the OVERALL board.
        if (_boardIndex != 0) return;
        if (SteamLeaderboardManager.Instance == null) return;

        string allTime = PurrbricksLeaderboards.OverallAllTime;
        SteamLeaderboardManager.Instance.PrewarmBoard(
            PurrbricksLeaderboards.Scoped(allTime, LeaderboardTimeScope.Weekly));
        SteamLeaderboardManager.Instance.PrewarmBoard(
            PurrbricksLeaderboards.Scoped(allTime, LeaderboardTimeScope.Daily));
    }
```

**Pre-condition:** `GameManager.GetAllLevelIds()` is called inside `FetchFromMySQL`. Confirm it exists — it is at `Assets/_Project/Scripts/GameManager.cs:1273`: `public string[] GetAllLevelIds() => _levelIds;`. No changes needed there.

**Note on `playerRank`:** The spec describes this as `int?` (nullable). The implementation uses plain `int` with `0` as a sentinel meaning "no score this period or error." The PHP returns `0` (not JSON `null`) for the unranked case, so `JsonUtility` deserialises it correctly as `0`. This is an intentional deviation from the spec for C# simplicity — `0` is never a valid rank (ranks are 1-based).

**Note on `using Steamworks;`:** `HighScoresUI.cs` already has `using Steamworks;` at line 4, so `new Steamworks.CSteamID(...)` can be written as just `new CSteamID(...)`. The fully-qualified form shown below also compiles fine.

- [ ] **Step 2: Add `FetchFromMySQL` method — insert it directly after the closing `}` of `FetchTimeout` (after line ~356)**

```csharp
    // ── MySQL fetch (per-level Daily / Weekly) ────────────────────────────────

    private void FetchFromMySQL(int token)
    {
        if (LevelScoreService.Instance == null)
        {
            _fetching = false;
            SetStatus("Score service unavailable.\nRun Purrbricks > Setup Scene.");
            return;
        }

        int    levelIndex = _boardIndex - 1;
        var    ids        = GameManager.Instance?.GetAllLevelIds();
        string levelId    = (ids != null && levelIndex >= 0 && levelIndex < ids.Length)
                            ? ids[levelIndex] : "";

        if (string.IsNullOrEmpty(levelId))
        {
            _fetching = false;
            SetStatus("Level not found.");
            return;
        }

        ulong steamId = SteamworksBootstrap.Instance?.IsSteamAvailable == true
                        ? SteamUser.GetSteamID().m_SteamID : 0UL;

        LevelScoreService.Instance.FetchScores(levelId, _scope, 50, steamId, (entries, playerRank) =>
        {
            if (token != _fetchToken) return; // stale — a newer fetch has started

            _fetching = false;

            if (entries == null || entries.Length == 0)
            {
                SetStatus("No scores yet — be the first!");
                return;
            }

            var models = new System.Collections.Generic.List<LeaderboardEntryModel>();
            foreach (var e in entries)
                models.Add(new LeaderboardEntryModel(e.Rank, e.Score,
                    new Steamworks.CSteamID(e.SteamId), e.SteamName));

            PopulateRows(models, highlightMe: true);
        });
    }
```

- [ ] **Step 3: Add MySQL branch at the top of `Fetch()` (line 309)**

Insert the new branch after `ClearRows();` and before `string board = BoardName();`. The existing `LeaderboardTestData.Enabled` block below it is **kept as-is** — it handles the OVERALL board in debug builds. The new MySQL branch returns early for per-level boards so the debug block only ever fires for `_boardIndex == 0`.

The full top of `Fetch()` after the edit (lines 309–333) should read exactly:

```csharp
    private void Fetch()
    {
        _fetchToken++;
        int token = _fetchToken;
        _fetching = true;
        SetStatus("Loading...");
        ClearRows();

        // Per-level Daily/Weekly → MySQL path (Steam no longer has these boards).
        // LevelScoreService manages its own 8s timeout; no FetchTimeout coroutine needed here.
        if (_boardIndex > 0 && _scope != LeaderboardTimeScope.AllTime)
        {
            FetchFromMySQL(token);
            return;
        }

        string board = BoardName();
        if (Debug.isDebugBuild)
            Debug.Log($"HighScoresUI: Fetch board='{board}' scope={_scope}");

        // In debug builds, Weekly/Daily boards are generated locally so we never block on a
        // slow FindOrCreateLeaderboard round-trip for date-encoded board names.
        // (Only fires for OVERALL board now — per-level Daily/Weekly went through FetchFromMySQL above.)
        if (LeaderboardTestData.Enabled && _scope != LeaderboardTimeScope.AllTime)
        {
            _fetching = false;
            GenerateTestData(board);
            return;
        }

        // ... rest of existing Steam code unchanged from here ...
```

- [ ] **Step 4: Verify it compiles in Unity**

No errors.

- [ ] **Step 5: Commit**

```bash
git add "Assets/_Project/Scripts/UI/HighScoresUI.cs"
git commit -m "feat: HighScoresUI routes per-level Daily/Weekly tabs to LevelScoreService"
```

---

## Task 8: Wire Up + Comment Cleanup

**Files:**
- Modify: `Assets/_Project/Scripts/Services/PurrbricksLeaderboards.cs`
- Modify: `Assets/_Project/Scripts/Editor/PurrbricksSetup.cs`

- [ ] **Step 1: Add usage comment to `PurrbricksLeaderboards.cs`**

Add this comment above the `Scoped()` method:

```csharp
    /// <summary>
    /// Returns a date-scoped board name for Daily or Weekly scope.
    /// NOTE: Only call this for the OVERALL board (OverallAllTime) and community boards.
    /// Per-level Daily/Weekly boards now use MySQL via LevelScoreService — do NOT call
    /// Scoped() with LevelAllTime() board names.
    /// </summary>
```

- [ ] **Step 2: Add `LevelScoreService` to `PurrbricksSetup.cs`**

Find this block (around line 443):

```csharp
        var clsGO = EnsureGO("CommunityLevelService");
        if (clsGO.GetComponent<CommunityLevelService>() == null)
        {
            clsGO.AddComponent<CommunityLevelService>();
            Debug.Log("Added CommunityLevelService.");
        }
```

Add immediately after it:

```csharp
        var lssGO = EnsureGO("LevelScoreService");
        if (lssGO.GetComponent<LevelScoreService>() == null)
        {
            lssGO.AddComponent<LevelScoreService>();
            Debug.Log("Added LevelScoreService.");
        }
```

- [ ] **Step 3: Run "Purrbricks > Setup Scene" in the Unity editor**

Confirm a `LevelScoreService` GameObject appears in the scene hierarchy. Select it and verify the `_apiBaseUrl` Inspector field shows `https://dubry.com/purrbricks-api/scores`.

- [ ] **Step 4: Commit**

```bash
git add "Assets/_Project/Scripts/Services/PurrbricksLeaderboards.cs" \
        "Assets/_Project/Scripts/Editor/PurrbricksSetup.cs"
git commit -m "chore: wire LevelScoreService into setup, clarify Scoped() usage"
```

---

## Task 9: End-to-End Verification

This task has no code changes — it is purely manual verification before the feature is considered done.

- [ ] **Step 1: Play a level through to the Victory screen**

Start the game in the Unity editor. Play through any level to completion. On the Victory screen:
- Confirm "+N Purr Bucks" appears (may take up to ~10 seconds while MySQL responds)
- Open phpMyAdmin and check `level_scores` table — a row should exist for today's UTC date for the level you just played

- [ ] **Step 2: Verify rank bonuses for top-3**

Using the curl commands from Task 2, insert two additional fake scores that are *lower* than your score for the same `levelId`. Your in-game score should now be rank 1 daily.

Play the level again (or use the "Replay" button). On the next Victory screen, the Purr Bucks award should be higher than the base participation reward (rank 1 daily bonus = 40% of first-place bonus).

- [ ] **Step 3: Verify HighScoresUI Daily tab**

From the Victory screen, click "High Scores". Navigate to a per-level board using the arrow buttons, then click the DAILY tab. Confirm:
- Scores appear (the row you submitted in Step 1)
- Your name is highlighted
- The ALL TIME tab still shows Steam data (unchanged)

- [ ] **Step 4: Verify HighScoresUI Weekly tab**

Click the WEEKLY tab on the same per-level board. Confirm scores appear and your entry is highlighted.

- [ ] **Step 5: Verify OVERALL board Daily/Weekly tabs still use Steam**

Navigate to the OVERALL board (leftmost board). Click DAILY and WEEKLY tabs. These should still hit Steam (existing behavior, unchanged).

- [ ] **Step 6: Verify no new Steam leaderboards are being created**

Wait 24 hours, then check the Steamworks Partner Portal leaderboard list. No new `_Daily_YYYYMMDD` or `_Weekly_YYYYMMDD` leaderboards should have been created for per-level boards.

- [ ] **Step 7: Final commit**

```bash
git add .
git commit -m "feat: MySQL daily/weekly per-level leaderboards — verification complete"
```
