using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Steamworks;
using UnityEngine;

/// <summary>
/// One-shot Steam → MySQL migration tool.
///
/// HOW TO USE:
///   1. Enter Play Mode with Steam running.
///   2. Add this component to any active GameObject in the scene.
///   3. Watch the Console — it logs progress per level.
///   4. When done, find  steam_export.sql  in the project root (next to Assets/).
///   5. Run that SQL file on your Bluehost MySQL database.
///   6. Remove this component when finished.
///
/// WHAT IT EXPORTS:
///   - Current-week weekly boards (date-encoded) — stored with this week's
///     Sunday date so they appear under the Weekly tab.
///   - Today's daily boards (date-encoded) — stored with today's date.
///   Per-level AllTime stays on Steam, so we only migrate Weekly/Daily here.
/// </summary>
public class SteamScoreExporter : MonoBehaviour
{
    private const int PAGE_SIZE = 100; // Steam max per request

    private void Start() => StartCoroutine(ExportAll());

    // ── Main coroutine ────────────────────────────────────────────────────────

    private IEnumerator ExportAll()
    {
        var gm = GameManager.Instance;
        if (gm == null)             { Debug.LogError("[Exporter] GameManager not found.");             yield break; }
        if (SteamLeaderboardManager.Instance == null) { Debug.LogError("[Exporter] SteamLeaderboardManager not found."); yield break; }
        if (!SteamworksBootstrap.Instance?.IsSteamAvailable == true) { Debug.LogError("[Exporter] Steam not available."); yield break; }

        // Give Steam a moment to finish initialising.
        yield return new WaitForSecondsRealtime(2f);

        int levelCount = gm.LevelCount;
        Debug.Log($"[Exporter] Starting export for {levelCount} levels...");

        var sb = new StringBuilder();
        sb.AppendLine("-- Purrbricks Steam → MySQL leaderboard export");
        sb.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- Run this on your Bluehost MySQL database.");
        sb.AppendLine("-- AllTime rows use score_date='2000-01-01' (historical, won't affect daily/weekly tabs).");
        sb.AppendLine();

        DateTime utcNow     = DateTime.UtcNow;
        string   weeklyDate  = PurrbricksLeaderboards.GetUtcWeekStart(utcNow).ToString("yyyy-MM-dd");
        string   dailyDate   = utcNow.ToString("yyyy-MM-dd");

        string[] slugs = gm.GetAllLevelIds();
        int totalRows  = 0;

        for (int i = 0; i < levelCount; i++)
        {
            string slug      = (slugs != null && i < slugs.Length) ? slugs[i] : $"level_{i:D2}";
            string guid      = "";
            string levelName = slug;
            try
            {
                var asset = Resources.Load<TextAsset>("Levels/" + slug);
                if (asset != null)
                {
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelData>(asset.text);
                    guid      = !string.IsNullOrEmpty(data?.levelGuid)    ? data.levelGuid    : slug;
                    levelName = !string.IsNullOrEmpty(data?.displayName)  ? data.displayName  : slug;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(guid)) guid = slug;

            if (guid == slug)
                Debug.LogWarning($"[Exporter] Level {i} '{slug}' has no levelGuid — using slug as fallback.");

            string allTimeBoard = PurrbricksLeaderboards.LevelAllTime(i);
            string weeklyBoard  = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Weekly);
            string dailyBoard   = PurrbricksLeaderboards.Scoped(allTimeBoard, LeaderboardTimeScope.Daily);

            sb.AppendLine($"-- Level {i}: {levelName} ({guid})");

            int rows = 0;
            yield return FetchAndAppend(weeklyBoard, guid, levelName, weeklyDate, sb, n => rows += n);
            yield return FetchAndAppend(dailyBoard,  guid, levelName, dailyDate,  sb, n => rows += n);

            totalRows += rows;
            if (rows > 0)
                Debug.Log($"[Exporter] Level {i + 1}/{levelCount} '{levelName}': {rows} rows");
        }

        sb.AppendLine();
        sb.AppendLine($"-- Total rows: {totalRows}");

        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "steam_export.sql"));
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[Exporter] Done! {totalRows} total rows → {path}");
    }

    // ── Fetch one board, all pages ────────────────────────────────────────────

    private IEnumerator FetchAndAppend(string boardName, string guid, string levelName,
                                        string scoreDate, StringBuilder sb, Action<int> addRows)
    {
        int page  = 1;
        int total = 0;

        while (true)
        {
            int start = (page - 1) * PAGE_SIZE + 1;
            int end   = page * PAGE_SIZE;

            List<LeaderboardEntryModel> entries = null;
            bool done = false;

            SteamLeaderboardManager.Instance.FetchRange(boardName, start, end, result =>
            {
                entries = result;
                done    = true;
            });

            // Wait for callback (10s timeout per page).
            float elapsed = 0f;
            while (!done && elapsed < 10f) { elapsed += Time.unscaledDeltaTime; yield return null; }

            if (!done || entries == null || entries.Count == 0) break;

            foreach (var e in entries)
            {
                sb.AppendLine(
                    $"INSERT INTO level_scores (level_id, level_name, steam_id, steam_name, score, score_date) " +
                    $"VALUES ('{Esc(guid)}', '{Esc(levelName)}', '{e.SteamId.m_SteamID}', '{Esc(e.DisplayName)}', {e.Score}, '{scoreDate}') " +
                    $"ON DUPLICATE KEY UPDATE score = GREATEST(score, VALUES(score)), steam_name = VALUES(steam_name);");
                total++;
            }

            if (entries.Count < PAGE_SIZE) break; // reached the last page
            page++;
        }

        addRows(total);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Esc(string s) =>
        s?.Replace("\\", "\\\\").Replace("'", "\\'") ?? "";
}
