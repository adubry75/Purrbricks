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
    public bool unlocked;
    /// <summary>True for levels shipped with the game. Editable only when AdminMode is on in GameManager.</summary>
    public bool nativeLevel = false;
    /// <summary>Display order in the editor browser. -1 = sort after all levels with an explicit order.</summary>
    public int levelOrder = -1;
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
public class BrickRotation
{
    // Degrees per second. Use negative values to spin the opposite direction.
    public float speed = 180f;

    // Optional pivot offset in world units, relative to the brick's initial position.
    // (0,0) = rotate around the brick center.
    public float pivotOffsetX = 0f;
    public float pivotOffsetY = 0f;

    // Optional starting angle (degrees) applied when the brick spawns.
    public float startAngle = 0f;
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
    public BrickRotation rotation; // null = not rotating
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
