using NUnit.Framework;
using Newtonsoft.Json;

public class LevelDataJsonTests
{
    // ── Field defaults ────────────────────────────────────────────────────────

    [Test]
    public void MinimalJson_UsesFieldDefaults()
    {
        const string json = @"{""id"":""test_00"",""displayName"":""Test Level""}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.AreEqual("test_00", data.id);
        Assert.AreEqual("Test Level", data.displayName);
        Assert.AreEqual(8.5f, data.ballSpeed, 0.001f);
        Assert.IsNotNull(data.grid);
        Assert.AreEqual(12, data.grid.cols);
        Assert.AreEqual(6, data.grid.rows);
        Assert.IsNotNull(data.bricks);
        Assert.AreEqual(0, data.bricks.Count);
        Assert.IsFalse(data.unlocked);
        Assert.IsFalse(data.nativeLevel);
        Assert.AreEqual(-1, data.levelOrder);
    }

    // ── Grid config ───────────────────────────────────────────────────────────

    [Test]
    public void GridConfig_CustomValues_ParseCorrectly()
    {
        const string json = @"{""id"":""t"",""grid"":{""cols"":8,""rows"":10,""brickWidth"":1.5,""brickHeight"":0.5,""gapX"":0.1,""gapY"":0.2}}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.AreEqual(8, data.grid.cols);
        Assert.AreEqual(10, data.grid.rows);
        Assert.AreEqual(1.5f, data.grid.brickWidth, 0.001f);
        Assert.AreEqual(0.5f, data.grid.brickHeight, 0.001f);
        Assert.AreEqual(0.1f, data.grid.gapX, 0.001f);
        Assert.AreEqual(0.2f, data.grid.gapY, 0.001f);
    }

    // ── Brick nullable fields ─────────────────────────────────────────────────

    [Test]
    public void Brick_ExplicitHp_ParsesValue()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":2,""row"":3,""templateId"":""steel"",""hp"":4}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.AreEqual(1, data.bricks.Count);
        Assert.AreEqual("steel", data.bricks[0].templateId);
        Assert.AreEqual(4, data.bricks[0].hp);
    }

    [Test]
    public void Brick_NoHp_HpIsNull()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""standard""}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.IsNull(data.bricks[0].hp);
    }

    [Test]
    public void Brick_ExplicitPoints_ParsesValue()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""gem"",""points"":10000}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.AreEqual(10000, data.bricks[0].points);
    }

    [Test]
    public void Brick_NoPoints_PointsIsNull()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""standard""}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.IsNull(data.bricks[0].points);
    }

    [Test]
    public void Brick_Indestructible_ParsesFlag()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""steel"",""isIndestructible"":true}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.IsTrue(data.bricks[0].isIndestructible);
    }

    // ── Brick movement ────────────────────────────────────────────────────────

    [Test]
    public void Brick_WithMovement_ParsesAllFields()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":1,""row"":1,""templateId"":""standard"",
            ""movement"":{""type"":""circular"",""amplitude"":2.5,""period"":3.0,""phaseOffset"":1.57}}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);
        var m = data.bricks[0].movement;

        Assert.IsNotNull(m);
        Assert.AreEqual("circular", m.type);
        Assert.AreEqual(2.5f, m.amplitude, 0.001f);
        Assert.AreEqual(3.0f, m.period, 0.001f);
        Assert.AreEqual(1.57f, m.phaseOffset, 0.001f);
    }

    [Test]
    public void Brick_NoMovement_MovementIsNull()
    {
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""standard""}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.IsNull(data.bricks[0].movement);
    }

    [Test]
    public void Brick_MovementDefaults_HorizontalType()
    {
        // Only amplitude provided — type and period should use BrickMovement defaults
        const string json = @"{""id"":""t"",""bricks"":[{""col"":0,""row"":0,""templateId"":""standard"",
            ""movement"":{""amplitude"":1.0}}]}";
        var data = JsonConvert.DeserializeObject<LevelData>(json);
        var m = data.bricks[0].movement;

        Assert.IsNotNull(m);
        Assert.AreEqual("horizontal", m.type);
        Assert.AreEqual(1.0f, m.amplitude, 0.001f);
        Assert.AreEqual(2.5f, m.period, 0.001f); // BrickMovement default
    }

    // ── Round-trip ────────────────────────────────────────────────────────────

    [Test]
    public void RoundTrip_SerializeDeserialize_PreservesAllData()
    {
        var original = new LevelData
        {
            id          = "roundtrip_test",
            displayName = "Round Trip",
            ballSpeed   = 9.0f,
            unlocked    = true,
            nativeLevel = true,
            levelOrder  = 42,
            levelGuid   = "abc-123"
        };
        original.bricks.Add(new BrickEntryData
        {
            col        = 3,
            row        = 2,
            templateId = "gem",
            hp         = 1,
            points     = 10000
        });
        original.bricks.Add(new BrickEntryData
        {
            col              = 5,
            row              = 4,
            templateId       = "steel",
            isIndestructible = true,
            movement         = new BrickMovement { type = "vertical", amplitude = 1.5f, period = 2.0f }
        });

        string json  = JsonConvert.SerializeObject(original);
        var    clone = JsonConvert.DeserializeObject<LevelData>(json);

        Assert.AreEqual(original.id, clone.id);
        Assert.AreEqual(original.displayName, clone.displayName);
        Assert.AreEqual(original.ballSpeed, clone.ballSpeed, 0.001f);
        Assert.AreEqual(original.unlocked, clone.unlocked);
        Assert.AreEqual(original.nativeLevel, clone.nativeLevel);
        Assert.AreEqual(original.levelOrder, clone.levelOrder);
        Assert.AreEqual(original.levelGuid, clone.levelGuid);

        Assert.AreEqual(2, clone.bricks.Count);
        Assert.AreEqual("gem", clone.bricks[0].templateId);
        Assert.AreEqual(10000, clone.bricks[0].points);

        Assert.IsTrue(clone.bricks[1].isIndestructible);
        Assert.IsNotNull(clone.bricks[1].movement);
        Assert.AreEqual("vertical", clone.bricks[1].movement.type);
    }
}
