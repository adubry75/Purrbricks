using UnityEngine;

[CreateAssetMenu(menuName = "Purrbricks/Brick Definition", fileName = "BrickDef_")]
public class BrickDefinition : ScriptableObject
{
    [Header("ID used in Level layouts")]
    public char symbol = '1';   // e.g. 'R', 'G', '4', '$', etc.

    [Header("Gameplay")]
    public int hitPoints = 1;
    public int points = 100;

    [Header("Look")]
    public Color tint = Color.white;

    [Header("Future: Powerups / Special Behaviors")]
    public bool dropsPowerup = false;
    public string powerupId = ""; // later we’ll replace with a real PowerupDefinition asset
}
