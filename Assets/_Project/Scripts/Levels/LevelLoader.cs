using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance { get; private set; }

    [Header("Spawn Setup")]
    [SerializeField] private BoxCollider2D _brickArea;
    [SerializeField] private Brick _brickPrefab;

    [Header("Optional: Powerup Pickup Prefab")]
    [SerializeField] private PowerupPickup _powerupPickupPrefab;

    private List<GameObject> _spawnedBricks = new List<GameObject>();
    private List<GameObject> _spawnedGates = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public LevelData CurrentLevel { get; private set; }

    public void LoadLevel(string levelId)
    {
        Debug.Log($"LevelLoader: Loading '{levelId}'");
        ClearBricks();

        TextAsset json = Resources.Load<TextAsset>($"Levels/{levelId}");
        if (json == null)
        {
            Debug.LogError($"LevelLoader: Could not find Resources/Levels/{levelId}.json");
            return;
        }

        LevelData data;
        try
        {
            data = JsonConvert.DeserializeObject<LevelData>(json.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LevelLoader: Failed to parse {levelId}.json — {e.Message}");
            return;
        }

        CurrentLevel = data;
        SpawnBricks(data);
        SpawnPrismGates(data);
    }

    public void ClearLevel()
    {
        CurrentLevel = null;
        ClearBricks();
    }

    private void ClearBricks()
    {
        foreach (var go in _spawnedBricks)
        {
            if (go != null) Destroy(go);
        }
        _spawnedBricks.Clear();

        foreach (var go in _spawnedGates)
        {
            if (go != null) Destroy(go);
        }
        _spawnedGates.Clear();

        // Also clear any remaining children (e.g. from editor tests)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void SpawnPrismGates(LevelData data)
    {
        if (data == null || data.prismGates == null) return;

        foreach (var g in data.prismGates)
        {
            if (g == null) continue;

            if (!PrismColorUtil.TryParse(g.color, out var color))
                color = PrismColor.Blue;

            var go = new GameObject("PrismGate_" + color.ToString());
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(g.x, g.y, 0f);

            var gate = go.AddComponent<PrismGate>();
            gate.Init(color, new Vector2(g.width, g.height), g.postThickness);

            _spawnedGates.Add(go);
        }
    }

    private void SpawnBricks(LevelData data)
    {
        if (_brickPrefab == null)
        {
            Debug.LogError("LevelLoader: _brickPrefab is not assigned.");
            return;
        }

        GridConfig g = data.grid ?? new GridConfig();

        // Compute grid anchor from brickArea (same as old BrickGridSpawner)
        Vector2 areaCenter = Vector2.zero;
        float areaTopY = 4f; // fallback if no brickArea assigned

        if (_brickArea != null)
        {
            Bounds b = _brickArea.bounds;
            areaCenter.x = b.center.x;
            areaTopY = b.max.y - 0.25f; // inner margin
        }

        float stepX = g.brickWidth + g.gapX;
        float stepY = g.brickHeight + g.gapY;
        float gridW = g.cols * g.brickWidth + (g.cols - 1) * g.gapX;

        float left = areaCenter.x - gridW * 0.5f;
        float top = areaTopY;

        int spawned = 0;

        foreach (var entry in data.bricks)
        {
            float x = left + entry.col * stepX + g.brickWidth * 0.5f;
            float y = top - entry.row * stepY - g.brickHeight * 0.5f;

            Brick brick = Instantiate(_brickPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
            brick.transform.localScale = Vector3.one;

            // Optional rotation: add BrickRotator to either the brick itself (center pivot),
            // or a pivot/root object (offset pivot). This keeps rotation composable with movement.
            Transform motionRoot = brick.transform;
            if (entry.rotation != null)
            {
                Vector2 pivotOffset = new Vector2(entry.rotation.pivotOffsetX, entry.rotation.pivotOffsetY);
                if (pivotOffset.sqrMagnitude > 0.000001f)
                {
                    var pivotGO = new GameObject("BrickPivot");
                    pivotGO.transform.SetParent(transform, false);
                    pivotGO.transform.position = brick.transform.position + (Vector3)pivotOffset;

                    // Parent the brick under the pivot, preserving its world position.
                    brick.transform.SetParent(pivotGO.transform, true);
                    motionRoot = pivotGO.transform;

                    var rot = pivotGO.AddComponent<BrickRotator>();
                    rot.Init(entry.rotation);
                }
                else
                {
                    var rot = brick.gameObject.AddComponent<BrickRotator>();
                    rot.Init(entry.rotation);
                }
            }

            // Special behaviors keyed off templateId (works even if no ScriptableObject template exists).
            bool isBumper = false;
            if (!string.IsNullOrEmpty(entry.templateId))
            {
                string tid = entry.templateId.Trim();
                if (tid.Equals("ghost", System.StringComparison.OrdinalIgnoreCase) ||
                    tid.Equals("ghostbrick", System.StringComparison.OrdinalIgnoreCase) ||
                    tid.Equals("ghost_brick", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (brick.GetComponent<GhostBrick>() == null)
                        brick.gameObject.AddComponent<GhostBrick>();
                }
                else if (tid.Equals("bumper", System.StringComparison.OrdinalIgnoreCase) ||
                         tid.Equals("bumperbrick", System.StringComparison.OrdinalIgnoreCase) ||
                         tid.Equals("bumper_brick", System.StringComparison.OrdinalIgnoreCase))
                {
                    isBumper = true;
                    if (brick.GetComponent<BumperBrick>() == null)
                        brick.gameObject.AddComponent<BumperBrick>();
                }
            }

            // Size the collider and sprite renderer
            var box = brick.GetComponent<BoxCollider2D>();
            if (box != null) box.size = new Vector2(g.brickWidth, g.brickHeight);

            var sr = brick.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = new Vector2(g.brickWidth, g.brickHeight);
            }

            // Bumper brick: make it round + indestructible.
            if (isBumper)
            {
                brick.SetIndestructible(true);
                brick.SetPoints(0);

                // Use a diameter that visually reads as "round bumper" inside a grid cell.
                float diameter = Mathf.Max(0.45f, g.brickHeight * 1.6f);
                brick.GetComponent<BumperBrick>()?.Configure(diameter);

                // Prevent the standard brick visual controller from overriding the bumper sprite/color.
                var bumperVisual = brick.GetComponent<BrickVisualController>();
                if (bumperVisual != null) bumperVisual.enabled = false;
            }

            // Resolve template
            BrickTemplate template = null;
            if (!string.IsNullOrEmpty(entry.templateId) && BrickTemplateRegistry.Instance != null)
                template = BrickTemplateRegistry.Instance.Get(entry.templateId);

            int hp = entry.hp ?? (template != null ? template.defaultHp : 1);
            int pts = entry.points ?? (template != null ? template.defaultPoints : 100);
            bool indestructible = isBumper || entry.isIndestructible || (template != null && template.isIndestructible);

            brick.SetHitPoints(hp);
            // If this is a bumper, keep it indestructible/0 points regardless of template/json.
            if (!isBumper)
                brick.SetPoints(pts);
            brick.SetIndestructible(indestructible);

            if (!string.IsNullOrEmpty(entry.requiredBallColor))
                brick.SetRequiredBallColor(entry.requiredBallColor);

            // Resolve skin and tint — priority: JSON tint > skin > template > white
            string skinId = entry.skinId ?? template?.defaultSkinId;
            BrickSkin skin = null;
            if (!string.IsNullOrEmpty(skinId) && BrickSkinRegistry.Instance != null)
                skin = BrickSkinRegistry.Instance.Get(skinId);

            Color tint = Color.white;
            if (!string.IsNullOrEmpty(entry.tint))
            {
                if (!ColorUtility.TryParseHtmlString(entry.tint, out tint))
                    tint = Color.white;
            }
            else if (skin != null)
                tint = skin.defaultTint;
            else if (template != null)
                tint = template.defaultTint;

            if (string.IsNullOrEmpty(entry.tint) && brick.RequiredBallColor != PrismColor.None)
                tint = PrismColorUtil.ToUnityColor(brick.RequiredBallColor);

            if (!isBumper)
                brick.SetTemplate(template, skin, tint);

            // Multi-hit bricks shimmer faster and more intensely so they stand out
            var visual = brick.GetComponent<BrickVisualController>();
            if (!isBumper && visual != null && hp > 1)
                visual.SetShimmer(speed: 3.5f, amount: 0.14f);

            // SetPowerupId AFTER SetTemplate so the visual knows the base tint
            if (!isBumper && !string.IsNullOrEmpty(entry.powerupId))
                brick.SetPowerupId(entry.powerupId);

            // Attach movement component for animated bricks
            if (entry.movement != null)
            {
                var mover = motionRoot.gameObject.AddComponent<BrickMover>();
                mover.Init(entry.movement);
            }

            _spawnedBricks.Add(motionRoot.gameObject);

            if (!indestructible) spawned++;
        }

        LevelManager.Instance?.BeginLevel(spawned);
    }

    // Kept for backward compatibility — Brick.cs now spawns pickups directly
    public void SpawnPowerupPickup(string powerupId, Vector3 worldPos) { }
}
