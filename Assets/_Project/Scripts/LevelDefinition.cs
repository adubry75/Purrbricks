using UnityEngine;

[CreateAssetMenu(menuName = "Purrbricks/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Grid")]
    public int rows = 5;
    public int cols = 12;

    [Header("Layout (rows lines, top to bottom). Use chars: . = empty, 1-9 = brick HP")]
    [TextArea(5, 20)]
    public string layout =
@"111111111111
111111111111
111111111111
111111111111
111111111111";
}
