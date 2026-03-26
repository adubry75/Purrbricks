# MySQL Daily/Weekly Leaderboards — Verification Checklist

Before marking this feature complete, verify the following manually.

---

## Server Setup

- [ ] `schema.sql` run in BlueHost phpMyAdmin — `level_scores` table created with correct columns and indexes
- [ ] `db.php` uploaded to `/home3/dubrycom/purrbricks-api/scores/` with real DB credentials filled in
- [ ] `submit.php` uploaded to `/home3/dubrycom/purrbricks-api/scores/`
- [ ] `list.php` uploaded to `/home3/dubrycom/purrbricks-api/scores/`

### Smoke test submit.php
```bash
curl -s -X POST https://dubry.com/purrbricks-api/scores/submit.php \
  -H "Content-Type: application/json" \
  -d '{"steamId":"76561198000000001","steamName":"TestCat","levelId":"alien_invasion","score":95000}'
```
Expected: `{"success":true,"dailyRank":1,"weeklyRank":1}`

### Smoke test list.php
```bash
curl -s "https://dubry.com/purrbricks-api/scores/list.php?levelId=alien_invasion&scope=daily&limit=10&steamId=76561198000000001"
```
Expected: `{"scores":[{"rank":1,"steamId":"76561198000000001","steamName":"TestCat","score":95000}],"playerRank":1}`

---

## Unity — Scene Wiring

- [ ] Run **Purrbricks > Setup Scene** in the Unity editor
- [ ] `LevelScoreService` GameObject appears in scene hierarchy
- [ ] Inspector field `_apiBaseUrl` shows `https://dubry.com/purrbricks-api/scores`

---

## Unity — Victory Screen

- [ ] Play any level to completion
- [ ] Victory screen shows "+N Purr Bucks" (may take 1–3s for MySQL round-trip)
- [ ] phpMyAdmin shows a row in `level_scores` for today's UTC date for the level just played
- [ ] AllTime Steam leaderboard submit still fires (check `SteamLeaderboardManager` debug logs)

### Rank bonus test
- Insert two lower scores for the same level via curl (use different steamIds)
- Play the level again — Purr Bucks award should include a daily rank bonus (rank 1 = 40% of first-place bonus)

---

## Unity — HighScoresUI

- [ ] Open High Scores from the Victory screen; navigate to a per-level board
- [ ] **DAILY tab** shows MySQL scores — your entry appears and is highlighted
- [ ] **WEEKLY tab** shows MySQL scores — your entry appears and is highlighted
- [ ] **ALL TIME tab** still shows Steam data (unchanged)
- [ ] Navigate to the OVERALL board — DAILY and WEEKLY tabs still use Steam (unchanged)

---

## Regression Check

- [ ] Community levels: Victory screen and HighScoresUI still work (no daily/weekly boards added for community)
- [ ] OVERALL board DAILY/WEEKLY tabs still work on Steam
- [ ] No new `_Daily_YYYYMMDD` or `_Weekly_YYYYMMDD` Steam leaderboards created after a day passes

---

## Done

Once all boxes are checked, the MySQL daily/weekly leaderboard feature is complete.
