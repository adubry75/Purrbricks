using UnityEngine;

public class BrickGridSpawner : MonoBehaviour
{
    [SerializeField] private BoxCollider2D _brickArea;
    [SerializeField] private Brick _brickPrefab;

    [Header("Grid")]
    [SerializeField] private int _rows = 5;
    [SerializeField] private int _cols = 12;

    [Header("Brick Size + Gap (world units)")]
    [SerializeField] private Vector2 _brickSize = new Vector2(1.35f, 0.45f);
    [SerializeField] private Vector2 _gap = new Vector2(0.08f, 0.16f);

    [Header("Margins inside BrickArea")]
    [SerializeField] private Vector2 _innerMargin = new Vector2(0.25f, 0.25f);


    public void Spawn()
    {
        // Clear old bricks (children)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        Bounds b = _brickArea.bounds;

        float left = b.min.x + _innerMargin.x;
        float top = b.max.y - _innerMargin.y;

        float stepX = _brickSize.x + _gap.x;
        float stepY = _brickSize.y + _gap.y;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                float x = left + c * stepX + _brickSize.x * 0.5f;
                float y = top - r * stepY - _brickSize.y * 0.5f;

                Brick brick = Instantiate(_brickPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);

                // Make the visual/collider match the chosen brick size
                var box = brick.GetComponent<BoxCollider2D>();
                if (box != null) box.size = _brickSize;

                var sr = brick.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.drawMode = SpriteDrawMode.Sliced; // or Tiled
                    sr.size = _brickSize;
                }
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

    [ContextMenu("Spawn Test Level (5x12)")]
    private void SpawnTest_5x12()
    {
        _rows = 5;
        _cols = 12;
        Spawn();
    }


}
