using UnityEngine;

[CreateAssetMenu(menuName = "Purrbricks/Brick Template", fileName = "BrickTemplate_")]
public class BrickTemplate : ScriptableObject
{
    [Header("ID")]
    public string id;
    public string displayName;

    [Header("Gameplay")]
    public int defaultHp = 1;
    public int defaultPoints = 100;

    [Header("Visuals")]
    public string defaultSkinId;

    [Header("Visuals")]
    public Color defaultTint = Color.white;

    [Header("Behavior")]
    public bool isIndestructible;
}
