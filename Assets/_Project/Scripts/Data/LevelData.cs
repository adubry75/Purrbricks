using System;
using System.Collections.Generic;

[Serializable]
public class LevelData
{
    public string id;
    public string displayName;
    public float ballSpeed = 8.5f;
    public GridConfig grid = new GridConfig();
    public List<BrickEntryData> bricks = new List<BrickEntryData>();
}

[Serializable]
public class GridConfig
{
    public int cols = 12;
    public int rows = 6;
    public float brickWidth = 1.35f;
    public float brickHeight = 0.45f;
    public float gapX = 0.08f;
    public float gapY = 0.16f;
}

[Serializable]
public class BrickEntryData
{
    public int col;
    public int row;
    public string templateId;
    public string skinId;
    public string powerupId;
    public string tint;
    public int? hp;
    public int? points;
    public bool isIndestructible;
}
