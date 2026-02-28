using UnityEngine;

/// <summary>
/// Stores optional art sprites for powerup types.
/// Assign sprites in the Inspector — one slot per PowerupType (22 entries, same index order as the enum).
/// Leave a slot null to fall back to the procedural orb / ring visuals.
/// </summary>
public class PowerupIconRegistry : MonoBehaviour
{
    public static PowerupIconRegistry Instance { get; private set; }

    [Tooltip("Sprite icons indexed by PowerupType enum value (22 entries).\n" +
             "0=WidePaddle  1=MultiBall  2=StickyBall  3=SpeedBall  4=ExtraLife\n" +
             "5=Laser  6=Fireball  7=BombBrick  8=ShieldWall  9=BigBall  10=ScoreFrenzy\n" +
             "11=ShrinkPaddle  12=ZipBall  13=FlipControls  14=CursedBall  15=TinyBall\n" +
             "16=InvisiBall  17=DrunkenPaddle  18=PermanentStickyBall  19=DrunkVision\n" +
             "20=GremlinBounces  21=FlipScreen")]
    [SerializeField] public Sprite[] Icons = new Sprite[22];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Returns the art sprite for the given type, or null if none is assigned.</summary>
    public Sprite GetIcon(PowerupType type)
    {
        int i = (int)type;
        return (Icons != null && i < Icons.Length) ? Icons[i] : null;
    }
}
