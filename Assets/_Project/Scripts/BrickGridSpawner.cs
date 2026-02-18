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

    [SerializeField] private LevelDefinition _level;
    public void SetLevel(LevelDefinition level) => _level = level;

    public void Spawn()
    {
        // Clear old bricks (children)
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        Bounds b = _brickArea.bounds;

        int rows = _level != null ? _level.rows : _rows;
        int cols = _level != null ? _level.cols : _cols;

        float stepX = _brickSize.x + _gap.x;
        float stepY = _brickSize.y + _gap.y;

        float gridW = (cols * _brickSize.x) + ((cols - 1) * _gap.x);
        float gridH = (rows * _brickSize.y) + ((rows - 1) * _gap.y);

        // available area inside margins (not strictly needed unless you want to validate fit)
        float availW = b.size.x - (_innerMargin.x * 2f);
        float availH = b.size.y - (_innerMargin.y * 2f);

        if (gridW > availW || gridH > availH)
        {
            Debug.LogWarning($"Brick grid ({gridW:F2}x{gridH:F2}) does not fit inside BrickArea ({availW:F2}x{availH:F2}). Reduce brick size/gap or increase BrickArea.");
        }


        // center horizontally, top-align vertically (within margin)
        float left = b.center.x - (gridW * 0.5f);
        float top = b.max.y - _innerMargin.y;


        string[] lines = null;
        if (_level != null && !string.IsNullOrWhiteSpace(_level.layout))
        {
            lines = _level.layout.Replace("\r", "").Split('\n');
        }


        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                char ch = '1';

                if (lines != null && r < lines.Length && c < lines[r].Length)
                    ch = lines[r][c];

                if (ch == '.' || ch == '0' || ch == ' ')
                    continue;

                int hp = 1;
                if (ch >= '1' && ch <= '9') hp = ch - '0';

                // Calculate brick position
                float x = left + c * stepX + (_brickSize.x * 0.5f);
                float y = top - r * stepY - (_brickSize.y * 0.5f);


                // Spawn brick
                Brick brick = Instantiate(_brickPrefab, new Vector3(x, y, 0f), Quaternion.identity, transform);
                brick.transform.localScale = Vector3.one;

                brick.SetHitPoints(hp);

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

        LevelManager.Instance?.BeginLevel(GetComponentsInChildren<Brick>().Length);


    }


    private void Start()
    {
        //Spawn();
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
