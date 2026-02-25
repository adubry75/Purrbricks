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
    public List<PrismGateData> prismGates = new List<PrismGateData>();
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
public class BrickMovement
{
    public string type = "horizontal"; // "horizontal" | "vertical" | "circular"
    public float amplitude = 1.5f;
    public float period = 2.5f;
    public float phaseOffset = 0f;
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
    public string requiredBallColor;
    public int? hp;
    public int? points;
    public bool isIndestructible;
    public BrickMovement movement; // null = static brick
}

[Serializable]
public class PrismGateData
{
    public float x;
    public float y;
    public float width = 4.0f;
    public float height = 0.35f;
    public float postThickness = 0.35f;
    public string color = "blue";
}
