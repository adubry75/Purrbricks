# Design: MySQL Daily/Weekly Per-Level Leaderboards

**Date:** 2026-03-25
**Status:** Approved
**Scope:** Replace Steam daily/weekly per-level leaderboards with a MySQL-backed system hosted on BlueHost. All-time leaderboards (overall and per-level) remain on Steam unchanged.

---

## Problem

Steam leaderboards are permanent — they cannot be deleted via API or easily via the Partner Portal. The current daily/weekly system appends a UTC date to board names (e.g. `Purrbricks_level_00_Daily_20260325`), creating a new Steam board for every level every day/week. With 100+ levels this generates thousands of stale boards per year.

---

## Solution

Move daily and weekly per-level leaderboards to a MySQL database on the existing BlueHost server (already used for community levels). All-time per-level boards and the global `Purrbricks_HighScores` board stay on Steam.

---

## Server Details

- **Community API base:** `/home3/dubrycom/purrbricks-api/community/`
- **Scores API base:** `/home3/dubrycom/purrbricks-api/scores/` *(new directory, already created)*
- Same MySQL credentials/connection pattern as community endpoints
- Target MySQL version: **5.7** (BlueHost shared hosting default — no MySQL 8.0 window functions assumed)

---

## Database

### New table: `level_scores`

```sql
CREATE TABLE level_scores (
  id           INT AUTO_INCREMENT PRIMARY KEY,
  level_id     VARCHAR(64)      NOT NULL,
  steam_id     BIGINT UNSIGNED  NOT NULL,
  steam_name   VARCHAR(128)     NOT NULL DEFAULT '',
  score        INT UNSIGNED     NOT NULL,
  score_date   DATE             NOT NULL,
  submitted_at DATETIME         NOT NULL DEFAULT UTC_TIMESTAMP(),
  UNIQUE KEY uq_player_level_date (steam_id, level_id, score_date),
  INDEX idx_level_date_score (level_id, score_date, score)
);
```

- One row per *(player, level, UTC day)*
- `score_date` uses `UTC_DATE()` on insert — natural daily reset at 00:00 UTC
- On re-submit the same day: `ON DUPLICATE KEY UPDATE score = GREATEST(score, VALUES(score)), steam_name = VALUES(steam_name), submitted_at = UTC_TIMESTAMP()` — keeps best score; `steam_name` always updated to the latest submitted value (reflects current display name)
- Weekly rank is derived by querying across all rows in the current UTC week (Sunday–Saturday) — no separate table needed
- `YEARWEEK(score_date, 0)` used for week grouping (mode 0 = Sunday start, matches existing `GetUtcWeekStart` in `PurrbricksLeaderboards.cs`)
- Index does not use `DESC` (not supported on MySQL 5.7 non-functional indexes; ignored silently)

---

## PHP Endpoints

All files live in `/home3/dubrycom/purrbricks-api/scores/`.
All share a `db.php` (identical pattern to `community/db.php`).
All return `Content-Type: application/json` with CORS headers.

### `db.php`
Singleton PDO connection helper. Copied/adapted from `community/db.php` with same DB credentials.

---

### `submit.php` — `POST`

**Purpose:** Submit a score for a level. Returns the player's rank (1–3) for both daily and weekly scopes in a single round trip. Called from Unity on Victory screen.

**Request body (JSON):**
```json
{ "steamId": 76561198000000000, "steamName": "PlayerName", "levelId": "alien_invasion", "score": 95000 }
```

**Behaviour:**
1. Validate required fields; reject (HTTP 400) if missing or score ≤ 0
2. Upsert: `INSERT INTO level_scores (level_id, steam_id, steam_name, score, score_date) VALUES (?, ?, ?, ?, UTC_DATE()) ON DUPLICATE KEY UPDATE score = GREATEST(score, VALUES(score)), steam_name = VALUES(steam_name), submitted_at = UTC_TIMESTAMP()`
3. Query **daily rank**: count distinct `steam_id`s with any score today for this level that exceeds the submitted score, + 1
4. Query **weekly rank**: count distinct `steam_id`s whose `MAX(score)` this week for this level exceeds the submitted score, + 1. The sub-query must explicitly `GROUP BY steam_id` and take `MAX(score)` per player before comparing, so a player with one great score earlier in the week and a bad score today is counted correctly
5. Return ranks: only 1, 2, or 3 are returned as integers; anything higher returns `null`

**Rank queries (reference SQL):**
```sql
-- Daily rank
SELECT COUNT(DISTINCT steam_id) + 1
FROM level_scores
WHERE level_id = :levelId
  AND score_date = UTC_DATE()
  AND score > :score

-- Weekly rank
SELECT COUNT(*) + 1
FROM (
  SELECT steam_id, MAX(score) AS best
  FROM level_scores
  WHERE level_id = :levelId
    AND YEARWEEK(score_date, 0) = YEARWEEK(UTC_DATE(), 0)
  GROUP BY steam_id
) w
WHERE w.best > :score
```

**Response:**
```json
{ "success": true, "dailyRank": 1, "weeklyRank": null }
```
(`null` = not in top 3)

---

### `list.php` — `GET`

**Purpose:** Fetch leaderboard entries for display in HighScoresUI Daily/Weekly tabs.

**Query params:**
```
?levelId=alien_invasion&scope=daily|weekly&limit=10&steamId=76561198000000000
```
- `steamId` is optional — if provided, also returns the player's own rank in `playerRank`
- `limit` capped at 50

**Behaviour:**
- `scope=daily`: `WHERE level_id = ? AND score_date = UTC_DATE()`, ordered by `score DESC`; one row per player (enforced by unique key)
- `scope=weekly`: `GROUP BY steam_id`, take `MAX(score)` per player within `YEARWEEK(score_date, 0) = YEARWEEK(UTC_DATE(), 0)`, ordered by `MAX(score) DESC`
- `steam_name` for weekly results: use `ANY_VALUE(steam_name)` to satisfy MySQL `ONLY_FULL_GROUP_BY` mode (acceptable — same player, name rarely changes; always updated on latest submit)
- Assign sequential rank 1..N after ordering
- If `steamId` provided: if the player appears in the top-N results their rank is read from those results; if not in top-N, run a separate rank sub-query (same as `submit.php` rank queries above) to compute their rank. If the player has no score in this period, `playerRank` is returned as `null`

**Response:**
```json
{
  "scores": [
    { "rank": 1, "steamId": "76561198000000001", "steamName": "TopCat", "score": 120000 },
    { "rank": 2, "steamId": "76561198000000002", "steamName": "Whiskers", "score": 95000 }
  ],
  "playerRank": 2
}
```
- `playerRank` is an integer (1-based) or `null` if the player has no score this period

---

## Unity — New File

### `LevelScoreService.cs` — `Assets/_Project/Scripts/Services/`

Singleton `MonoBehaviour`, `DontDestroyOnLoad`. Inspector field `_apiBaseUrl` (e.g. `https://dubry.com/purrbricks-api/scores`).

**Public API:**

```csharp
public struct SubmitResult {
    public int DailyRank;   // 1, 2, or 3; 0 = not in top 3
    public int WeeklyRank;  // 1, 2, or 3; 0 = not in top 3
}

public struct ScoreEntry {
    public int    Rank;
    public ulong  SteamId;
    public string SteamName;
    public int    Score;
}

// Submit a score. Callback receives DailyRank and WeeklyRank (0 = not in top 3).
// Times out after 8 seconds; on timeout or error calls back with rank 0 (no bonus, no crash).
public void SubmitScore(string levelId, ulong steamId, string steamName, int score,
                        Action<SubmitResult> callback)

// Fetch leaderboard entries for HighScoresUI display.
// Only valid for Daily or Weekly scope — asserts/returns empty if AllTime is passed.
// playerRank in callback is null if the player has no score this period; 0 on error/guard path.
public void FetchScores(string levelId, LeaderboardTimeScope scope, int limit,
                        ulong steamId, Action<ScoreEntry[], int?> callback)
```

- Uses `UnityWebRequest` coroutines with explicit `timeout = 8` seconds (same pattern as `CommunityLevelService`)
- Gracefully handles network errors and timeouts: `SubmitScore` calls back with `SubmitResult{0,0}`; `FetchScores` calls back with empty array and rank 0
- `FetchScores` must guard against `LeaderboardTimeScope.AllTime` being passed (log error, return empty — the AllTime tab remains on Steam and must never call this service)

Auto-created by `PurrbricksSetup.cs`.

---

## Unity — Modified Files

### `VictoryUI.cs`

**Remove:**
- `SteamLeaderboardManager.SubmitScore` calls for Daily and Weekly Steam boards
- `LeaderboardTestData.RerollForBoard(weeklyBoard)` and `RerollForBoard(dailyBoard)` calls (these were used to simulate fake Steam weekly/daily entries in debug builds; no longer needed for per-level boards)

**Keep:**
- `SteamLeaderboardManager.SubmitScore(LevelAllTime(...))` — all-time Steam submit unchanged

**Add:**
- `LevelScoreService.Instance.SubmitScore(levelId, steamId, steamName, score, OnScoreSubmitted)` call
- `OnScoreSubmitted(SubmitResult result)` passes the `result` directly to `PurrBucksManager.AwardLevelComplete` as new parameters (see below)

---

### `PurrBucksManager.cs`

**`AwardLevelComplete` signature change:**
```csharp
// Before
public void AwardLevelComplete(string levelId, int levelIndex, bool perfectClear, int livesLost)

// After
public void AwardLevelComplete(string levelId, int levelIndex, bool perfectClear, int livesLost,
                               int dailyRank = 0, int weeklyRank = 0)
```

The daily and weekly ranks are passed in directly from the `LevelScoreService.SubmitScore` callback in `VictoryUI`, rather than being fetched inside `AwardLevelComplete`. This eliminates the round trip.

**`FetchRankAndAward` coroutine:**
- **Remove:** `FetchAroundMe` Steam calls for Daily and Weekly boards
- **Remove:** the `LeaderboardTestData.Enabled` branch that called `PurrbricksLeaderboards.Scoped(..., Daily)` and `Scoped(..., Weekly)` — daily/weekly ranks now come from `dailyRank`/`weeklyRank` parameters directly, so no simulation is needed
- **Keep:** `FetchAroundMe` Steam call for All Time board (unchanged)
- **Keep:** `LeaderboardTestData.Enabled` branch for the **All Time** board only
- Rank bonus calculation unchanged: AllTime 100%, Weekly 60%, Daily 40%

**`AwardCommunityLevelComplete`:**
- Community levels have no daily/weekly leaderboards — no change needed here

---

### `HighScoresUI.cs`

**`Fetch()` method — branching between Steam and MySQL:**

`Fetch()` currently constructs a board name via `BoardName()` and passes it to Steam. After this change, `Fetch()` must branch based on board type and scope:

```
if _boardIndex == 0 (OVERALL) OR _boardIndex == -1 (community/custom):
    → always use Steam path (Scoped() → SteamLeaderboardManager) — UNCHANGED
if _boardIndex > 0 (per-level board):
    if scope == AllTime → Steam path (LevelAllTime(index)) — UNCHANGED
    if scope == Daily or Weekly → MySQL path (LevelScoreService.FetchScores)
```

The MySQL path populates rows using `ScoreEntry[]` mapped to `LeaderboardEntryModel` (same fields: rank, score, steamId, steamName).

**`PrewarmCurrentBoardScopes()`:**
- Currently prewarms Daily and Weekly Steam boards for the currently-displayed level
- After this change: skip prewarming for Daily and Weekly when `_boardIndex > 0` (per-level boards) — those boards no longer exist on Steam
- Continue prewarming Daily and Weekly for `_boardIndex == 0` (OVERALL board, still on Steam)

**`BoardName()`:**
- Currently calls `PurrbricksLeaderboards.Scoped(allTime, _scope)` unconditionally
- After this change: `BoardName()` is only used for the Steam path; when the MySQL path is taken in `Fetch()`, `BoardName()` is not called at all

**OVERALL board tabs (Daily/Weekly) — unchanged:**
When `_boardIndex == 0`, Daily and Weekly tabs continue to use `PurrbricksLeaderboards.Scoped(OverallAllTime, scope)` → Steam. No change.

**Community board (`_boardIndex == -1`) — unchanged:**
All scopes for custom/community boards route through the Steam path. No change.

---

### `PurrbricksLeaderboards.cs`

- **`Scoped()` function remains intact** — it is still used for the OVERALL Steam board (Daily/Weekly) and community boards
- No cases removed from `Scoped()`
- Add a code comment clarifying that `Scoped()` should only be called for the overall board and community boards — per-level daily/weekly now use `LevelScoreService`

---

### `PurrbricksSetup.cs`

- Add `LevelScoreService` to the auto-create singleton list alongside other singletons

---

## Data Flow

```
[Victory screen clears a native level]
  │
  ├─► SteamLeaderboardManager.SubmitScore(LevelAllTime(index))   ← unchanged
  │
  └─► LevelScoreService.SubmitScore(levelId, steamId, name, score)
        │   (8s timeout; on failure → rank 0)
        ├─ POST /scores/submit.php
        │    └─ returns { dailyRank, weeklyRank }
        │
        └─► VictoryUI.OnScoreSubmitted(result)
              └─► PurrBucksManager.AwardLevelComplete(
                      levelId, levelIndex, perfectClear, livesLost,
                      dailyRank: result.DailyRank,
                      weeklyRank: result.WeeklyRank)
                    │
                    ├─ base + first-clear + perfect-clear rewards (immediate)
                    ├─ FetchAroundMe(LevelAllTime) via Steam  ← unchanged
                    ├─ dailyRank / weeklyRank from parameters (no extra round trip)
                    └─ fires OnRankAwardResolved with total Purr Bucks

[Player opens HighScoresUI → per-level Daily or Weekly tab]
  └─► LevelScoreService.FetchScores(levelId, scope, limit, steamId)
        └─ GET /scores/list.php?levelId=X&scope=daily|weekly&limit=10&steamId=Y
             └─ returns { scores[], playerRank } → populates leaderboard rows

[Player opens HighScoresUI → OVERALL Daily or Weekly tab]
  └─► Steam path unchanged (PurrbricksLeaderboards.Scoped → SteamLeaderboardManager)
```

---

## Error Handling

- Network failure / timeout on `SubmitScore`: log warning, callback with `SubmitResult{0,0}` — player gets no rank bonus but game continues normally
- Network failure on `FetchScores`: show "Could not load scores" via existing `SetStatus()` pattern in `HighScoresUI`
- Server-side validation errors: PHP returns HTTP 4xx with `{ "error": "message" }` — Unity logs and treats as rank 0 / empty list
- `FetchScores` called with `AllTime` scope: log error, return empty array immediately (guard in `LevelScoreService`)

---

## Out of Scope

- Community level daily/weekly boards — community levels use Steam for all scopes; no change
- Overall `Purrbricks_HighScores` Steam board — stays on Steam, unchanged
- Per-level All Time Steam boards — stays on Steam, unchanged
- `PurrbricksLeaderboards.Scoped()` — function is NOT removed; still used for OVERALL and community boards
- Score anti-cheat / server-side score validation beyond field presence and `score > 0` check
- `AwardCommunityLevelComplete` — community levels have no daily/weekly boards; no change needed
