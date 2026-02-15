using UnityEngine;

public class BrickGridSpawner : MonoBehaviour
{
    [SerializeField] private BoxCollider2D _brickArea;
    [SerializeField] private Brick _brickPrefab;

    [Header("Grid")]
    [SerializeField] private int _rows = 6;
    [SerializeField] private int _cols = 10;

    [Header("Spacing")]
    [SerializeField] private float _paddingX = 0.2f;
    [SerializeField] private float _paddingY = 0.2f;

    [SerializeField] private float _innerMarginX = 0.5f;
    [SerializeField] private float _innerMarginY = 0.5f;

    public void Spawn()
    {
        if (_brickArea == null || _brickPrefab == null) return;

        // Clear old bricks (simple approach for now)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        Bounds b = _brickArea.bounds;

        float left = b.min.x + _innerMarginX;
        float right = b.max.x - _innerMarginX;
        float top = b.max.y - _innerMarginY;
        float bottom = b.min.y + _innerMarginY;

        float width = right - left;
        float height = top - bottom;

        float cellW = width / _cols;
        float cellH = height / _rows;

        // Use prefab collider size as baseline
        var prefabCol = _brickPrefab.GetComponent<BoxCollider2D>();
        float brickW = prefabCol != null ? prefabCol.bounds.size.x : cellW - _paddingX;
        float brickH = prefabCol != null ? prefabCol.bounds.size.y : cellH - _paddingY;

        // If prefab is too big for the grid, shrink by scaling on spawn
        float targetW = cellW - _paddingX;
        float targetH = cellH - _paddingY;

        float scaleX = brickW > 0 ? targetW / brickW : 1f;
        float scaleY = brickH > 0 ? targetH / brickH : 1f;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                float x = left + (c + 0.5f) * cellW;
                float y = top - (r + 0.5f) * cellH;

                Brick brick = Instantiate(_brickPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
                brick.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        LevelManager.Instance?.BeginLevel(_rows * _cols);

    }

    private void Start()
    {
        Spawn();
    }

    [ContextMenu("Spawn Test Level (1x5)")]
    private void SpawnTest_1x5()
    {
        _rows = 1;
        _cols = 5;
        Spawn();
    }

    [ContextMenu("Spawn Test Level (3x8)")]
    private void SpawnTest_3x8()
    {
        _rows = 3;
        _cols = 8;
        Spawn();
    }

}
