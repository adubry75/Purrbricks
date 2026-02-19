using UnityEngine;

[CreateAssetMenu(menuName = "Purrbricks/Powerup Definition", fileName = "PowerupDef_")]
public class PowerupDefinition : ScriptableObject
{
    [Header("ID")]
    public string id;
    public string displayName;

    [Header("Visuals")]
    public Sprite pickupSprite;
    public GameObject pickupPrefab;

    [Header("Gameplay")]
    public float duration = 10f;
}
