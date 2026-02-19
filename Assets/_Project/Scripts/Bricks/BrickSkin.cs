using UnityEngine;

[CreateAssetMenu(menuName = "Purrbricks/Brick Skin", fileName = "BrickSkin_")]
public class BrickSkin : ScriptableObject
{
    [Header("ID")]
    public string id;

    [Header("Sprites")]
    public Sprite sprite;
    public Sprite[] damageSpriteStages;

    [Header("Colors")]
    public Color defaultTint = Color.white;
    public Color shimmerColor = Color.white;

    [Header("VFX")]
    public GameObject breakParticlePrefab;
}
